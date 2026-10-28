using System.Text.Json.Nodes;
using ToolRill.Cli.Commands;
using ToolRill.Cli.Services;
using ToolRill.Core;
using ToolRill.TestSupport;
using Xunit;

namespace ToolRill.UnitTests;

public sealed class CliDiscoveryTests
{
    [Fact]
    public void ProductIdentityUsesToolRillDefaults()
    {
        using var configuration = TestConfiguration.Create("stdio", []);
        var key = $"BrandingProbe_{Guid.NewGuid():N}";
        var variable = $"TOOLRILL_{key}";
        Environment.SetEnvironmentVariable(variable, "active");
        try
        {
            Assert.Equal("toolrill.json", CliConfiguration.DefaultPath);
            Assert.Equal("ToolRill", typeof(CliConfiguration).Assembly.GetName().Name);
            var effective = CliConfiguration.Load(configuration.Path);
            using var configurationLifetime = effective as IDisposable;
            Assert.Equal("active", effective[key]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public void SyncReplacesOnlyIncludesWithRawNames()
    {
        var entry = JsonNode.Parse("""
            { "name": "remote", "url": "${MCP_URL}",
              "tools": { "include": ["old"], "exclude": ["private*"], "custom": true } }
            """)!.AsObject();

        ServerCommand.SetToolIncludes(entry, ["echo", "CaseSensitive", "raw/name"]);

        Assert.Equal(["echo", "CaseSensitive", "raw/name"],
            entry["tools"]!["include"]!.AsArray().Select(name => name!.GetValue<string>()));
        Assert.Equal("private*", entry["tools"]!["exclude"]![0]!.GetValue<string>());
        Assert.True(entry["tools"]!["custom"]!.GetValue<bool>());
        Assert.Equal("${MCP_URL}", entry["url"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyDiscoveryCreatesAnEmptyIncludeArray(bool existingTools)
    {
        var entry = new JsonObject();
        if (existingTools)
        {
            entry["tools"] = new JsonObject { ["include"] = new JsonArray("old") };
        }

        ServerCommand.SetToolIncludes(entry, []);

        Assert.Empty(entry["tools"]!["include"]!.AsArray());
    }

    [Theory]
    [InlineData("server", "add", "remote", "--discover", "--discover")]
    [InlineData("server", "inspect", "remote", "--sync", "--SYNC")]
    [InlineData("server", "inspect", "remote", "--sync", "true")]
    [InlineData("server", "add", "remote", "--discover", "true")]
    [InlineData("server", "inspect", "remote", "--discover")]
    [InlineData("server", "add", "remote", "--sync")]
    [InlineData("server", "list", "--sync")]
    [InlineData("server", "inspect")]
    public async Task FlagsRejectDuplicatesValuesAndWrongCommands(params string[] arguments)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            CliCommandDispatcher.ExecuteAsync(arguments, CancellationToken.None));
    }

    [Fact]
    public async Task DisabledAndMissingServersFailBeforeConnectingOrSaving()
    {
        using var configuration = TestConfiguration.Create("stdio", [new McpServerOptions
        {
            Name = "disabled", Enabled = false, Command = "not-an-executable"
        }]);
        var original = await File.ReadAllTextAsync(configuration.Path);

        var disabled = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ServerCommand.InspectAsync("DISABLED", configuration.Path, "selected", sync: true));
        Assert.Contains("disabled", disabled.Message);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ServerCommand.InspectAsync("missing", configuration.Path, "selected", sync: true));
        Assert.Equal(original, await File.ReadAllTextAsync(configuration.Path));
    }

    [Fact]
    public async Task DuplicateAddWithDiscoveryDoesNotSaveOrConnect()
    {
        using var configuration = TestConfiguration.Create("stdio", [new McpServerOptions
        {
            Name = "remote", Transport = "streamable-http", Url = "http://127.0.0.1:1/mcp"
        }]);
        var original = await File.ReadAllTextAsync(configuration.Path);

        await Assert.ThrowsAsync<InvalidOperationException>(() => ServerCommand.AddAsync(
            "REMOTE", "streamable-http", null, null, "http://127.0.0.1:1/mcp",
            configuration.Path, "selected", discover: true));

        Assert.Equal(original, await File.ReadAllTextAsync(configuration.Path));
    }

    [Fact]
    public async Task PreCancelledDiscoveryLeavesConfigurationUnchanged()
    {
        using var configuration = TestConfiguration.Create("stdio", []);
        var original = await File.ReadAllTextAsync(configuration.Path);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CliCommandDispatcher.ExecuteAsync(
            ["server", "add", "remote", "--discover", "--config", configuration.Path], cancellation.Token));

        Assert.Equal(original, await File.ReadAllTextAsync(configuration.Path));
    }
}