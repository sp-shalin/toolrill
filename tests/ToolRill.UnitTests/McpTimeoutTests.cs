using System.Text.Json;
using ToolRill.Core;
using ToolRill.TestSupport;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Xunit;

namespace ToolRill.UnitTests;

public sealed class McpTimeoutTests
{
    private static McpDiagnostics Diagnostics() => new(new() { Enabled = false });

    [Fact]
    public async Task ConnectionTimeoutExpiresDuringInitialization()
    {
        await using var server = await TestHttpServer.StartAsync("remote", blockedMethod: "initialize");
        await using var connection = new McpServerConnection("remote", Diagnostics());
        var options = server.Options("remote");
        options.ConnectionTimeoutSeconds = 1;
        using var harnessDeadline = new CancellationTokenSource(TestLimits.Http);
        var pending = connection.ConnectAsync(options);

        await server.Gate.WaitForArrivalsAsync();
        await Assert.ThrowsAsync<TimeoutException>(() => pending.WaitAsync(harnessDeadline.Token));
        Assert.Equal(McpServerState.Failed, connection.State);
        await server.Gate.WaitForCancellationAsync();
    }

    [Fact]
    public async Task DiscoveryUsesTheConnectionTimeoutBudget()
    {
        await using var server = await TestHttpServer.StartAsync("remote", blockedMethod: "tools/list");
        await using var connection = new McpServerConnection("remote", Diagnostics());
        var options = server.Options("remote");
        options.ConnectionTimeoutSeconds = 1;
        await connection.ConnectAsync(options);
        using var harnessDeadline = new CancellationTokenSource(TestLimits.Http);
        var pending = connection.ListToolsAsync();

        await server.Gate.WaitForArrivalsAsync();
        await Assert.ThrowsAsync<TimeoutException>(() => pending.WaitAsync(harnessDeadline.Token));
        await server.Gate.WaitForCancellationAsync();
        Assert.Equal(McpServerState.Connected, connection.State);
    }

    [Theory]
    [InlineData("initialize")]
    [InlineData("tools/list")]
    public async Task StartupCallerCancellationReachesDownstream(string method)
    {
        await using var server = await TestHttpServer.StartAsync("remote", blockedMethod: method);
        await using var connection = new McpServerConnection("remote", Diagnostics());
        using var cancellation = new CancellationTokenSource(TestLimits.Http);
        var options = server.Options("remote");
        options.ConnectionTimeoutSeconds = 60;
        if (method == "tools/list")
        {
            await connection.ConnectAsync(options, cancellation.Token);
        }
        var pending = method == "initialize"
            ? connection.ConnectAsync(options, cancellation.Token)
            : connection.ListToolsAsync(cancellation.Token);

        await server.Gate.WaitForArrivalsAsync();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TestLimits.Http));
        await server.Gate.WaitForCancellationAsync();
    }

    [Fact]
    public async Task ToolTimeoutCancelsDownstreamAndLeavesConnectionUsable()
    {
        await using var server = await TestHttpServer.StartAsync("remote");
        var options = server.Options("remote");
        options.ToolCallTimeoutSeconds = 1;
        await using var gateway = await TestGateway.StartAsync(options);
        using var harnessDeadline = new CancellationTokenSource(TestLimits.Http);
        var pending = gateway.Tools.CallToolAsync("remote/wait", ToolResults.Arguments("timeout", 1));

        await server.Gate.WaitForArrivalsAsync();
        await Assert.ThrowsAsync<TimeoutException>(() => pending.WaitAsync(harnessDeadline.Token));
        await server.Gate.WaitForCancellationAsync();
        Assert.Equal("remote:healthy:2",
            ToolResults.Text(await gateway.CallAsync("remote/echo", ToolResults.Arguments("healthy", 2))));
    }

    [Fact]
    public async Task HostToolCallerCancellationIsNotReportedAsTimeout()
    {
        await using var server = await TestHttpServer.StartAsync("remote");
        await using var gateway = await TestGateway.StartAsync(server.Options("remote"));
        using var cancellation = new CancellationTokenSource(TestLimits.Http);
        var pending = gateway.Tools.CallToolAsync(
            "remote/wait", ToolResults.Arguments("cancel", 1), cancellation.Token);

        await server.Gate.WaitForArrivalsAsync();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TestLimits.Http));
        await server.Gate.WaitForCancellationAsync();
    }

    [Theory]
    [InlineData("streamable-http")]
    [InlineData("stdio")]
    public async Task InboundRequestCancellationReachesDownstreamTool(string transport)
    {
        await using var server = await TestHttpServer.StartAsync("remote");
        await using var gateway = await TestProcess.StartAsync(transport, server.Options("remote"));
        using var cancellation = new CancellationTokenSource(TestLimits.Http);
        var requestId = new RequestId("downstream-cancellation");
        Task pending = transport == "stdio"
            ? gateway.Client.SendRequestAsync(new JsonRpcRequest
            {
                Id = requestId,
                Method = RequestMethods.ToolsCall,
                Params = JsonSerializer.SerializeToNode(new
                {
                    name = "remote/wait",
                    arguments = ToolResults.Arguments("cancel", 1)
                })
            }, cancellation.Token)
            : gateway.Client.CallToolAsync("remote/wait",
                ToolResults.Arguments("cancel", 1), cancellationToken: cancellation.Token).AsTask();

        await server.Gate.WaitForArrivalsAsync();
        if (transport == "stdio")
        {
            using var notificationDeadline = new CancellationTokenSource(TestLimits.Http);
            await gateway.Client.SendMessageAsync(new JsonRpcNotification
            {
                Method = NotificationMethods.CancelledNotification,
                Params = JsonSerializer.SerializeToNode(new CancelledNotificationParams { RequestId = requestId })
            }, notificationDeadline.Token);
        }
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TestLimits.Http));
        await server.Gate.WaitForCancellationAsync();
    }

    [Fact]
    public async Task PreCancelledToolRequestDoesNotReachDownstream()
    {
        await using var server = await TestHttpServer.StartAsync("remote");
        await using var gateway = await TestGateway.StartAsync(server.Options("remote"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gateway.Tools.CallToolAsync(
            "remote/wait", ToolResults.Arguments("cancel", 1), cancellation.Token));
        Assert.Equal(0, server.Gate.Calls);
    }
}