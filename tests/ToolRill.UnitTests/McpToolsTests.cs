using ToolRill.Core;
using ToolRill.TestSupport;
using Xunit;

namespace ToolRill.UnitTests;

public sealed class McpToolsTests
{
    [Theory]
    [InlineData(null, null, "echo,fail,wait")]
    [InlineData("E*", null, "echo")]
    [InlineData("*", "WAIT", "echo,fail")]
    [InlineData("echo", "E*", "")]
    [InlineData("ch", null, "")]
    [InlineData("e.h*", null, "")]
    public async Task FiltersAreAnchoredCaseInsensitiveAndExclusionWins(string? include,
        string? exclude, string expected)
    {
        await using var server = await TestHttpServer.StartAsync("remote");
        await using var gateway = await TestGateway.StartAsync(server.Options("remote", new()
        {
            Include = include is null ? [] : [include],
            Exclude = exclude is null ? [] : [exclude]
        }));
        Assert.Equal(expected.Split(',', StringSplitOptions.RemoveEmptyEntries),
            gateway.Registry.Tools.Select(tool => tool.Name).OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public async Task AggregationKeepsServerIdentityAndRejectsDuplicateQualifiedNames()
    {
        await using var server = await TestHttpServer.StartAsync("source");
        await using var gateway = await TestGateway.StartAsync(server.Options("first"), server.Options("second"));
        Assert.Equal(6, gateway.Tools.ListTools().Count);
        var echo = Assert.IsType<McpToolInfo>(gateway.Registry.Find("FIRST", "ECHO"));
        Assert.Equal("first/echo", echo.QualifiedName);
        Assert.Null(gateway.Registry.Find("first", "missing"));
        var listed = Assert.Single(gateway.Tools.ListTools(), tool => tool.Name == "first/echo");
        Assert.Equal("first", listed.ServerName);
        Assert.Equal(echo.Description, listed.Description);
        Assert.Throws<InvalidOperationException>(() => gateway.Registry.Add("FIRST", [echo.Tool]));
        Assert.Equal(6, gateway.Registry.Tools.Count);
    }

    [Theory]
    [InlineData("echo")]
    [InlineData("/echo")]
    [InlineData("remote/")]
    [InlineData("")]
    public async Task MalformedToolNamesAreRejectedBeforeLookup(string name)
    {
        await using var manager = new McpServerManager(new(new() { Enabled = false }));
        var router = new McpToolRouter(manager, new());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => router.CallAsync(name, new Dictionary<string, object?>()));
        Assert.Contains("Expected format", error.Message);
    }

    [Fact]
    public async Task MissingToolAndMissingOwningConnectionHaveDistinctErrors()
    {
        await using var server = await TestHttpServer.StartAsync("remote");
        await using var gateway = await TestGateway.StartAsync(server.Options("remote"));
        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.CallAsync("remote/missing"));
        Assert.Contains("was not found", missing.Message);

        await using var emptyManager = new McpServerManager(new(new() { Enabled = false }));
        var router = new McpToolRouter(emptyManager, gateway.Registry);
        var disconnected = await Assert.ThrowsAsync<InvalidOperationException>(() => router.CallAsync("remote/echo", ToolResults.Arguments("value", 1)));
        Assert.Contains("not connected", disconnected.Message);
    }

    [Fact]
    public async Task RoutingUsesTheOwningServerAndRejectsCallsAfterDisconnection()
    {
        await using var first = await TestHttpServer.StartAsync("one");
        await using var second = await TestHttpServer.StartAsync("two");
        await using var gateway = await TestGateway.StartAsync(first.Options("first"), second.Options("second"));
        Assert.Equal("one:value:1", ToolResults.Text(await gateway.CallAsync("FIRST/echo", ToolResults.Arguments("value", 1))));
        Assert.Equal("two:value:2", ToolResults.Text(await gateway.CallAsync("second/echo", ToolResults.Arguments("value", 2))));
        await gateway.Manager.DisconnectAsync("first");
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.CallAsync("first/echo", ToolResults.Arguments("value", 1)));
    }

    [Fact]
    public async Task DownstreamToolErrorsRemainTypedResults()
    {
        await using var server = await TestHttpServer.StartAsync("remote");
        await using var gateway = await TestGateway.StartAsync(server.Options("remote"));
        var result = await gateway.CallAsync("remote/fail");
        Assert.True(result.IsError);
        Assert.Equal("remote:controlled failure", ToolResults.Text(result));
    }
}