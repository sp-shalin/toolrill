using System.Text.Json;
using System.Text.Json.Nodes;
using ToolRill.TestSupport;
using Xunit;

namespace ToolRill.IntegrationTests;

public sealed class CliDiscoveryIntegrationTests
{
    [Theory]
    [InlineData("streamable-http")]
    [InlineData("project")]
    [InlineData("direct")]
    public async Task AddDiscoversRawNamesAndInspectListsBothTransports(string transport)
    {
        await using var remote = transport == "streamable-http" ? await TestHttpServer.StartAsync("discovery",
            requiredHeaders: new Dictionary<string, string> { ["X-Discovery"] = "required" }) : null;
        using var configuration = TestConfiguration.Create("stdio", [], includeInvalidHost: true);
        var arguments = new List<string>
        {
            "server", "add", "live", "--DISCOVER", "--config", configuration.Path, "--host", "SELECTED"
        };
        string[] expected;
        if (remote is not null)
        {
            arguments.AddRange(["--transport", "streamable-http", "--url", remote.Endpoint.AbsoluteUri,
                "--header", "X-Discovery=required"]);
            expected = ["echo", "fail", "wait"];
        }
        else
        {
            var options = TestProcess.SampleOptions();
            arguments.AddRange(["--transport", "stdio", "--command", "dotnet"]);
            if (transport == "project")
            {
                arguments.AddRange(["--project", options.ProjectPath]);
            }
            else
            {
                var output = new DirectoryInfo(AppContext.BaseDirectory);
                var assembly = Path.Combine(Path.GetDirectoryName(options.ProjectPath)!, "bin",
                    output.Parent!.Name, output.Name, "StdioServer.dll");
                Assert.True(File.Exists(assembly));
                arguments.AddRange(["--args", JsonSerializer.Serialize(new[] { assembly })]);
            }
            expected = ["goodbye", "hello"];
        }

        var added = await TestProcess.RunCommandAsync(arguments.ToArray());
        Assert.Equal(0, added.ExitCode);
        var saved = await File.ReadAllTextAsync(configuration.Path);
        var document = JsonNode.Parse(saved)!;
        var entry = document["hosts"]![0]!["outgoing"]![0]!;
        Assert.Equal(expected, entry["tools"]!["include"]!.AsArray()
            .Select(name => name!.GetValue<string>()).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Empty(document["hosts"]![1]!["outgoing"]!.AsArray());

        var inspected = await TestProcess.RunCommandAsync("server", "inspect", "LIVE",
            "--config", configuration.Path, "--host", "selected");
        Assert.Equal(0, inspected.ExitCode);
        foreach (var name in expected)
        {
            Assert.Contains(name + "\t", inspected.Output);
        }
        Assert.DoesNotContain("live/", inspected.Output);
        Assert.Equal(saved, await File.ReadAllTextAsync(configuration.Path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InspectUsesEffectiveConnectionAndSyncChangesOnlyBaseIncludes(bool sync)
    {
        await using var remote = await TestHttpServer.StartAsync("effective");
        var options = remote.Options("remote", new() { Include = ["old"], Exclude = ["echo"] });
        options.Url = "http://127.0.0.1:1/mcp";
        using var configuration = TestConfiguration.Create("stdio", [options], includeInvalidHost: true);
        var original = await File.ReadAllTextAsync(configuration.Path);
        var sibling = Path.Combine(Path.GetDirectoryName(configuration.Path)!, "toolrill.Testing.json");
        var overrides = JsonSerializer.Serialize(new
        {
            hosts = new[] { new { outgoing = new[] { new { url = remote.Endpoint.AbsoluteUri } } } }
        });
        await File.WriteAllTextAsync(sibling, overrides);
        var arguments = new List<string>
        {
            "server", "inspect", "REMOTE", "--host", "SELECTED", "--config", configuration.Path
        };
        if (sync)
        {
            arguments.Add("--SYNC");
        }

        var result = await TestProcess.RunCommandAsync(arguments.ToArray());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("echo\tEcho text and number.", result.Output);
        Assert.Contains("wait\tWait for the test gate.", result.Output);
        Assert.Contains("fail\tReturn a controlled tool error.", result.Output);
        var saved = await File.ReadAllTextAsync(configuration.Path);
        if (sync)
        {
            var expected = JsonNode.Parse(original)!;
            expected["hosts"]![0]!["outgoing"]![0]!["tools"]!["include"] = new JsonArray("echo", "fail", "wait");
            var actual = JsonNode.Parse(saved)!;
            var actualIncludes = actual["hosts"]![0]!["outgoing"]![0]!["tools"]!["include"]!.AsArray();
            Assert.Equal(["echo", "fail", "wait"], actualIncludes.Select(name => name!.GetValue<string>())
                .OrderBy(name => name, StringComparer.Ordinal));
            actual["hosts"]![0]!["outgoing"]![0]!["tools"]!["include"] = new JsonArray("echo", "fail", "wait");
            Assert.True(JsonNode.DeepEquals(expected, actual));
        }
        else
        {
            Assert.Equal(original, saved);
        }
        Assert.Equal(overrides, await File.ReadAllTextAsync(sibling));
    }

    [Theory]
    [InlineData(false, "initialize")]
    [InlineData(true, "initialize")]
    [InlineData(false, "tools/list")]
    [InlineData(true, "tools/list")]
    public async Task DiscoveryFailureReportsStderrAndNeverSaves(bool inspect, string rejectedMethod)
    {
        await using var remote = await TestHttpServer.StartAsync("failure", rejectedMethod: rejectedMethod);
        using var configuration = TestConfiguration.Create("stdio", [remote.Options("existing")]);
        var original = await File.ReadAllTextAsync(configuration.Path);
        var arguments = inspect
            ? new[] { "server", "inspect", "existing", "--sync", "--host", "selected", "--config", configuration.Path }
            : new[] { "server", "add", "new", "--discover", "--transport", "streamable-http", "--url",
                remote.Endpoint.AbsoluteUri, "--host", "selected", "--config", configuration.Path };

        var result = await TestProcess.RunCommandAsync(arguments);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("ToolRill command failed", result.Error);
        Assert.Empty(result.Output);
        Assert.Equal(original, await File.ReadAllTextAsync(configuration.Path));
    }

    [Fact]
    public async Task InspectDiscoveryTimeoutLeavesBaseUnchanged()
    {
        await using var remote = await TestHttpServer.StartAsync("timeout", blockedMethod: "tools/list");
        var options = remote.Options("remote");
        options.ConnectionTimeoutSeconds = 1;
        using var configuration = TestConfiguration.Create("stdio", [options]);
        var original = await File.ReadAllTextAsync(configuration.Path);

        var result = await TestProcess.RunCommandAsync("server", "inspect", "remote", "--sync",
            "--host", "selected", "--config", configuration.Path);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Error:", result.Error);
        Assert.Empty(result.Output);
        Assert.Equal(original, await File.ReadAllTextAsync(configuration.Path));
    }

    [Fact]
    public async Task StdioDiscoveryLaunchFailureDoesNotSave()
    {
        using var configuration = TestConfiguration.Create("stdio", []);
        var original = await File.ReadAllTextAsync(configuration.Path);

        var result = await TestProcess.RunCommandAsync("server", "add", "failed", "--discover",
            "--transport", "stdio", "--command", "toolrill-nonexistent-test-executable",
            "--host", "selected", "--config", configuration.Path);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("ToolRill command failed", result.Error);
        Assert.Empty(result.Output);
        Assert.Equal(original, await File.ReadAllTextAsync(configuration.Path));
    }

    [Fact]
    public async Task AddWithoutDiscoveryRemainsOffline()
    {
        using var configuration = TestConfiguration.Create("stdio", []);
        var result = await TestProcess.RunCommandAsync("server", "add", "offline", "--transport", "stdio",
            "--command", "toolrill-nonexistent-test-executable", "--host", "selected", "--config", configuration.Path);

        Assert.Equal(0, result.ExitCode);
        var document = JsonNode.Parse(await File.ReadAllTextAsync(configuration.Path))!;
        Assert.Null(document["hosts"]![0]!["outgoing"]![0]!["tools"]);
    }
}