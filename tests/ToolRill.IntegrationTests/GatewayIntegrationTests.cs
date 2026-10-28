using System.Text.Json;
using ToolRill.Core;
using ToolRill.TestSupport;
using ModelContextProtocol;
using Xunit;

namespace ToolRill.IntegrationTests;

public sealed class GatewayIntegrationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task StdioLaunchesForwardEnvironmentAndPreserveArgumentBoundaries(bool projectLaunch, bool argumentOverride)
    {
        var variable = "TEST_TOOLRILL_" + Guid.NewGuid().ToString("N");
        var greeting = "env greeting " + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, greeting);
        try
        {
            var options = TestProcess.SampleOptions();
            options.Env = new() { ["Greeting"] = "prefix:${" + variable + "}" };
            if (!projectLaunch)
            {
                var output = new DirectoryInfo(AppContext.BaseDirectory);
                var assembly = Path.Combine(Path.GetDirectoryName(options.ProjectPath)!, "bin",
                    output.Parent!.Name, output.Name, "StdioServer.dll");
                Assert.True(File.Exists(assembly), "Build the matching sample configuration before running tests.");
                options.ProjectPath = "";
                options.Args = argumentOverride ? [assembly, "--Greeting", "argument greeting with spaces"] : [assembly];
            }

            await using var gateway = await TestProcess.StartAsync("stdio", options);
            using var deadline = new CancellationTokenSource(TestLimits.Http);
            var tools = await gateway.Client.ListToolsAsync(cancellationToken: deadline.Token);
            Assert.Contains(tools, tool => tool.Name == "local/hello");
            var hello = await gateway.Client.CallToolAsync("local/hello", cancellationToken: deadline.Token);
            Assert.Equal(argumentOverride ? "argument greeting with spaces" : "prefix:" + greeting,
                ToolResults.Text(ToolResults.Downstream(hello)));
            var goodbye = await gateway.Client.CallToolAsync("local/goodbye", cancellationToken: deadline.Token);
            Assert.Equal("Goodbye from Server A!", ToolResults.Text(ToolResults.Downstream(goodbye)));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CliPersistsDirectStdioCommandsAndArgsWithoutProject(bool withArgs)
    {
        using var configuration = TestConfiguration.Create("stdio", []);
        var arguments = new List<string>
        {
            "server", "add", "direct", "--transport", "stdio", "--command", "standalone-server",
            "--config", configuration.Path, "--host", "selected"
        };
        if (withArgs)
        {
            arguments.AddRange(["--args", "[\"path with spaces.dll\",\"--flag\",\"\"]"]);
        }
        var added = await TestProcess.RunCommandAsync(arguments.ToArray());
        Assert.Equal(0, added.ExitCode);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(configuration.Path));
        var server = document.RootElement.GetProperty("hosts")[0].GetProperty("outgoing")[0];
        Assert.Equal("standalone-server", server.GetProperty("command").GetString());
        Assert.False(server.TryGetProperty("projectPath", out _));
        if (withArgs)
        {
            Assert.Equal(["path with spaces.dll", "--flag", ""],
                server.GetProperty("args").EnumerateArray().Select(argument => argument.GetString()));
        }
        var validated = await TestProcess.RunCommandAsync("config", "validate", "--config", configuration.Path);
        Assert.Equal(0, validated.ExitCode);
    }

    [Theory]
    [InlineData("stdio", "not-json", false)]
    [InlineData("stdio", "{}", false)]
    [InlineData("stdio", "null", false)]
    [InlineData("stdio", "[null]", false)]
    [InlineData("stdio", "[42]", false)]
    [InlineData("stdio", "[\"extra\"]", true)]
    [InlineData("streamable-http", "[]", false)]
    public async Task CliRejectsInvalidOrConflictingStdioArgsWithoutSaving(string transport, string args, bool project)
    {
        using var configuration = TestConfiguration.Create("stdio", []);
        var original = await File.ReadAllTextAsync(configuration.Path);
        var arguments = new List<string>
        {
            "server", "add", "invalid", "--transport", transport, "--args", args,
            "--config", configuration.Path, "--host", "selected"
        };
        if (transport == "streamable-http")
        {
            arguments.AddRange(["--url", "https://example.com/mcp"]);
        }
        else
        {
            arguments.AddRange(["--command", "dotnet"]);
            if (project)
            {
                arguments.AddRange(["--project", "sample.csproj"]);
            }
        }
        var result = await TestProcess.RunCommandAsync(arguments.ToArray());
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("args", result.Error);
        Assert.Equal(original, await File.ReadAllTextAsync(configuration.Path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpHeadersReachDownstreamDuringDiscoveryAndCalls(bool expandEnvironment)
    {
        var variable = "TOOLRILL_TEST_" + Guid.NewGuid().ToString("N");
        var tokenVariable = variable + "_TOKEN";
        var secret = "header-" + Guid.NewGuid().ToString("N");
        await using var remote = await TestHttpServer.StartAsync("authenticated", requiredHeaders: new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer " + secret,
            ["X-Custom"] = "custom=value"
        });
        Environment.SetEnvironmentVariable(variable, remote.Endpoint.AbsoluteUri);
        Environment.SetEnvironmentVariable(tokenVariable, secret);
        try
        {
            var options = remote.Options("remote");
            options.Url = expandEnvironment ? "${" + variable + "}" : remote.Endpoint.AbsoluteUri;
            options.Headers = new()
            {
                ["Authorization"] = "Bearer " + (expandEnvironment ? "${" + tokenVariable + "}" : secret),
                ["X-Custom"] = "custom=value"
            };
            McpConfigurationValidator.Validate([options]);
            await using var gateway = await TestGateway.StartAsync(options);
            Assert.Contains(gateway.Registry.Tools, tool => tool.QualifiedName == "remote/echo");
            Assert.Equal("authenticated:request:7", ToolResults.Text(
                await gateway.CallAsync("remote/echo", ToolResults.Arguments("request", 7))));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
            Environment.SetEnvironmentVariable(tokenVariable, null);
        }
    }

    [Fact]
    public async Task MixedTransportsDiscoverFilterRouteAndDisconnect()
    {
        await using var remote = await TestHttpServer.StartAsync("http");
        var local = TestProcess.SampleOptions();
        local.Tools = new() { Include = ["hello"], Exclude = ["goodbye"] };
        await using var gateway = await TestGateway.StartAsync(local,
            remote.Options("remote", new() { Include = ["echo"] }),
            new() { Name = "disabled", Enabled = false, Command = "does-not-exist" });

        Assert.Equal(2, gateway.Manager.Connections.Count);
        Assert.Equal(["local/hello", "remote/echo"],
            gateway.Registry.Tools.Select(tool => tool.QualifiedName).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal("Hello from Server A!", ToolResults.Text(await gateway.CallAsync("local/hello")));
        Assert.Equal("http:request:7", ToolResults.Text(await gateway.CallAsync("remote/echo", ToolResults.Arguments("request", 7))));
        Assert.Equal("Disabled", gateway.Manager.GetServerStates()["disabled"]);

        await gateway.Manager.DisconnectAllAsync().WaitAsync(TestLimits.Cleanup);
        Assert.True(gateway.Manager.ShutdownRequested);
        Assert.All(gateway.Manager.Connections.Values, connection => Assert.Equal(McpServerState.Disconnected, connection.State));
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.CallAsync("local/hello"));
    }

    [Fact]
    public async Task ExplicitReconnectRestoresCallsWithoutRefreshingTheRegistry()
    {
        await using var remote = await TestHttpServer.StartAsync("remote");
        await using var gateway = await TestGateway.StartAsync(remote.Options("remote"));
        var connection = gateway.Manager.Connections["remote"];
        var registered = gateway.Registry.Tools.ToArray();

        await gateway.Manager.DisconnectAsync("REMOTE").WaitAsync(TestLimits.Cleanup);
        Assert.Equal(McpServerState.Disconnected, connection.State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.CallAsync("remote/echo", ToolResults.Arguments("before", 1)));
        await gateway.Manager.ReconnectAsync("remote").WaitAsync(TestLimits.Http);
        Assert.Equal(McpServerState.Connected, connection.State);
        Assert.Equal("remote:after:2", ToolResults.Text(await gateway.CallAsync("remote/echo", ToolResults.Arguments("after", 2))));
        Assert.Equal(registered, gateway.Registry.Tools);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connection.ListToolsAsync(cancellation.Token));
        Assert.Equal(McpServerState.Connected, connection.State);
    }

    [Fact]
    public async Task StartupStopsAtTheFirstFailureAndDisposalCleansEarlierConnections()
    {
        await using var remote = await TestHttpServer.StartAsync("healthy");
        await using var manager = new McpServerManager(new(new() { Enabled = false }));
        using var deadline = new CancellationTokenSource(TestLimits.Http);
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.ConnectAsync([
            remote.Options("healthy"),
            new() { Name = "failed", Transport = "streamable-http", Url = "relative/mcp" },
            remote.Options("unattempted")
        ], deadline.Token));

        Assert.Equal("Failed", manager.GetServerStates()["failed"]);
        Assert.Equal(McpServerState.Connected, manager.Connections["healthy"].State);
        Assert.Equal(McpServerState.Disconnected, manager.Connections["failed"].State);
        Assert.False(manager.Connections.ContainsKey("unattempted"));
        await manager.DisposeAsync().AsTask().WaitAsync(TestLimits.Cleanup);
        Assert.All(manager.Connections.Values, connection => Assert.Equal(McpServerState.Disconnected, connection.State));
    }

    [Fact]
    public async Task StoppedDownstreamFailsWhileHealthyDownstreamStillServesCalls()
    {
        await using var failing = await TestHttpServer.StartAsync("failing");
        await using var healthy = await TestHttpServer.StartAsync("healthy");
        await using var gateway = await TestGateway.StartAsync(failing.Options("failing"), healthy.Options("healthy"));
        Assert.Equal("failing:before:1", ToolResults.Text(await gateway.CallAsync("failing/echo", ToolResults.Arguments("before", 1))));
        await failing.StopAsync();

        var error = await Record.ExceptionAsync(() => gateway.CallAsync("failing/echo", ToolResults.Arguments("after", 2)));
        Assert.NotNull(error);
        Assert.True(error is McpException or HttpRequestException or IOException or InvalidOperationException,
            $"Unexpected failure type: {error.GetType().FullName}");
        Assert.Equal("healthy:after:2", ToolResults.Text(await gateway.CallAsync("healthy/echo", ToolResults.Arguments("after", 2))));
    }

    [Fact]
    public async Task CliValidatesTheSelectedHostWithoutValidatingUnselectedIncomingSettings()
    {
        using var configuration = TestConfiguration.Create("stdio", [TestProcess.SampleOptions()], includeInvalidHost: true);
        var selected = await TestProcess.RunCommandAsync("config", "validate", "--config", configuration.Path, "--host", "selected");
        Assert.Equal(0, selected.ExitCode);
        Assert.Contains("Configuration is valid", selected.Output);

        var all = await TestProcess.RunCommandAsync("config", "validate", "--config", configuration.Path);
        Assert.Equal(1, all.ExitCode);
        Assert.Contains("incoming transport", all.Error);
        var missing = await TestProcess.RunCommandAsync("config", "validate", "--config", configuration.Path, "--host", "missing");
        Assert.Equal(1, missing.ExitCode);
        Assert.Contains("was not found", missing.Error);
    }

    [Fact]
    public async Task CliPersistsRepeatedHeadersAndPlaceholdersAndValidatesEffectiveConfiguration()
    {
        var variable = "TEST_TOOLRILL_" + Guid.NewGuid().ToString("N");
        var secret = "cli-secret-" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, secret);
        try
        {
            using var configuration = TestConfiguration.Create("stdio", []);
            var placeholder = "${" + variable + "}";
            var added = await TestProcess.RunCommandAsync("server", "add", "authenticated",
                "--transport", "streamable-http", "--url", "https://example.com/mcp",
                "--header", "Authorization=Bearer " + placeholder, "--HEADER", "X-Custom=first=second",
                "--config", configuration.Path, "--host", "selected");
            Assert.Equal(0, added.ExitCode);
            var json = await File.ReadAllTextAsync(configuration.Path);
            Assert.DoesNotContain(secret, json);
            using var document = JsonDocument.Parse(json);
            var headers = document.RootElement.GetProperty("hosts")[0]
                .GetProperty("outgoing")[0].GetProperty("headers");
            Assert.Equal("Bearer " + placeholder, headers.GetProperty("Authorization").GetString());
            Assert.Equal("first=second", headers.GetProperty("X-Custom").GetString());

            var validated = await TestProcess.RunCommandAsync("config", "validate", "--config", configuration.Path);
            Assert.Equal(0, validated.ExitCode);
            Environment.SetEnvironmentVariable(variable, null);
            var missing = await TestProcess.RunCommandAsync("config", "validate", "--config", configuration.Path);
            Assert.Equal(1, missing.ExitCode);
            Assert.Contains(variable, missing.Error);
            Assert.Contains("missing or empty", missing.Error);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Theory]
    [InlineData("streamable-http", "invalid-private-value", "X-Other=value")]
    [InlineData("streamable-http", "X-Custom=private-value", "x-custom=other-private-value")]
    [InlineData("stdio", "X-Custom=private-value", "X-Other=other-private-value")]
    public async Task CliRejectsInvalidDuplicateOrStdioHeadersWithoutSaving(string transport, string first, string second)
    {
        using var configuration = TestConfiguration.Create("stdio", []);
        var original = await File.ReadAllTextAsync(configuration.Path);
        var endpointOption = transport == "stdio" ? "--project" : "--url";
        var endpoint = transport == "stdio" ? "sample.csproj" : "https://example.com/mcp";
        var result = await TestProcess.RunCommandAsync("server", "add", "remote", "--transport", transport,
            endpointOption, endpoint, "--header", first, "--header", second,
            "--config", configuration.Path, "--host", "selected");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("--header", result.Error);
        Assert.DoesNotContain("private-value", result.Error);
        Assert.Equal(original, await File.ReadAllTextAsync(configuration.Path));
    }
}