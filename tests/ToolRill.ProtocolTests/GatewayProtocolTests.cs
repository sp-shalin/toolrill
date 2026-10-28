using System.Text.Json;
using ToolRill.TestSupport;
using ModelContextProtocol;
using Xunit;

namespace ToolRill.ProtocolTests;

public sealed class GatewayProtocolTests
{
    [Theory]
    [InlineData("streamable-http")]
    [InlineData("stdio")]
    public async Task OfficialSdkInitializesListsSchemasAndForwardsArguments(string transport)
    {
        await using var downstream = await TestHttpServer.StartAsync("remote");
        await using var sourceClient = await TestGateway.ConnectClientAsync(downstream.Endpoint);
        using var discoveryDeadline = new CancellationTokenSource(TestLimits.Http);
        var sourceTools = await sourceClient.ListToolsAsync(cancellationToken: discoveryDeadline.Token);
        await using var gateway = await TestProcess.StartAsync(transport, downstream.Options("remote"));
        using var deadline = new CancellationTokenSource(TestLimits.Http);
        var tools = await gateway.Client.ListToolsAsync(cancellationToken: deadline.Token);

        Assert.Equal(["remote/echo", "remote/fail", "remote/wait"],
            tools.Select(tool => tool.Name).OrderBy(name => name, StringComparer.Ordinal));
        var source = Assert.Single(sourceTools, tool => tool.Name == "echo");
        var exposed = Assert.Single(tools, tool => tool.Name == "remote/echo");
        Assert.Equal(source.Description, exposed.Description);
        Assert.True(JsonElement.DeepEquals(source.ProtocolTool.InputSchema, exposed.ProtocolTool.InputSchema));
        Assert.Equal("string", exposed.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("text").GetProperty("type").GetString());
        Assert.Equal("integer", exposed.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("number").GetProperty("type").GetString());

        const string text = "quotes:\" slash:\\ newline:\n <>&";
        var result = await gateway.Client.CallToolAsync("remote/echo", ToolResults.Arguments(text, 42), cancellationToken: deadline.Token);
        Assert.False(result.IsError == true);
        var nested = ToolResults.Downstream(result);
        Assert.False(nested.IsError == true);
        Assert.Equal($"remote:{text}:42", ToolResults.Text(nested));
    }

    [Theory]
    [InlineData("streamable-http")]
    [InlineData("stdio")]
    public async Task ToolExecutionErrorsArePreservedInsideTheSerializedDownstreamResult(string transport)
    {
        await using var downstream = await TestHttpServer.StartAsync("remote");
        await using var gateway = await TestProcess.StartAsync(transport, downstream.Options("remote"));
        using var deadline = new CancellationTokenSource(TestLimits.Http);
        var result = await gateway.Client.CallToolAsync("remote/fail", cancellationToken: deadline.Token);
        Assert.False(result.IsError == true);
        var nested = ToolResults.Downstream(result);
        Assert.True(nested.IsError);
        Assert.Equal("remote:controlled failure", ToolResults.Text(nested));
    }

    [Theory]
    [InlineData("streamable-http")]
    [InlineData("stdio")]
    public async Task UnknownToolsProduceSdkProtocolErrors(string transport)
    {
        await using var downstream = await TestHttpServer.StartAsync("remote");
        await using var gateway = await TestProcess.StartAsync(transport, downstream.Options("remote"));
        using var deadline = new CancellationTokenSource(TestLimits.Http);
        await Assert.ThrowsAnyAsync<McpException>(() => gateway.Client.CallToolAsync("remote/missing", cancellationToken: deadline.Token).AsTask());
    }

    [Theory]
    [InlineData("streamable-http")]
    [InlineData("stdio")]
    public async Task InvalidNumericArgumentsProduceToolErrors(string transport)
    {
        await using var downstream = await TestHttpServer.StartAsync("remote");
        await using var gateway = await TestProcess.StartAsync(transport, downstream.Options("remote"));
        using var deadline = new CancellationTokenSource(TestLimits.Http);
        var result = await gateway.Client.CallToolAsync("remote/echo", new Dictionary<string, object?>
        {
            ["text"] = "invalid", ["number"] = "not-an-integer"
        }, cancellationToken: deadline.Token);
        Assert.True(result.IsError == true || ToolResults.Downstream(result).IsError == true);
    }

    [Theory]
    [InlineData("streamable-http")]
    [InlineData("stdio")]
    public async Task UpstreamSdkCancellationEndsTheCallerWait(string transport)
    {
        await using var downstream = await TestHttpServer.StartAsync("remote");
        await using var gateway = await TestProcess.StartAsync(transport, downstream.Options("remote"));
        using var cancellation = new CancellationTokenSource(TestLimits.Http);
        var pending = gateway.Client.CallToolAsync("remote/wait", ToolResults.Arguments("cancelled", 1), cancellationToken: cancellation.Token).AsTask();
        try
        {
            await downstream.Gate.WaitForArrivalsAsync();
            Assert.Equal(1, downstream.Gate.Calls);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TestLimits.Http));
        }
        finally
        {
            cancellation.Cancel();
            downstream.Gate.Release();
        }
    }
}