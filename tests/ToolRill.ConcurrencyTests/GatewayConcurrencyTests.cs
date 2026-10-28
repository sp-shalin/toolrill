using ToolRill.Core;
using ToolRill.TestSupport;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace ToolRill.ConcurrencyTests;

public sealed class GatewayConcurrencyTests
{
    [Fact]
    public async Task TwoHttpClientsCompleteEightOverlappingCallsAcrossTwoDownstreams()
    {
        await using var first = await TestHttpServer.StartAsync("one", expectedCalls: 4);
        await using var second = await TestHttpServer.StartAsync("two", expectedCalls: 4);
        await using var gateway = await TestProcess.StartAsync("streamable-http", first.Options("first"), second.Options("second"));
        await using var other = await gateway.ConnectAnotherClientAsync();
        using var deadline = new CancellationTokenSource(TestLimits.Http);

        async Task<int> CountToolsAsync(McpClient client)
            => (await client.ListToolsAsync(cancellationToken: deadline.Token)).Count;

        var counts = await Task.WhenAll(CountToolsAsync(gateway.Client), CountToolsAsync(other));
        Assert.Equal([6, 6], counts);
        var calls = Enumerable.Range(0, 8).Select(index =>
        {
            var client = index < 4 ? gateway.Client : other;
            var server = index % 2 == 0 ? "first" : "second";
            return client.CallToolAsync($"{server}/wait", ToolResults.Arguments($"request-{index}", index),
                cancellationToken: deadline.Token).AsTask();
        }).ToArray();
        try
        {
            await Task.WhenAll(first.Gate.WaitForArrivalsAsync(), second.Gate.WaitForArrivalsAsync());
            Assert.Equal(4, first.Gate.Calls);
            Assert.Equal(4, second.Gate.Calls);
            Assert.All(calls, call => Assert.False(call.IsCompleted));
            first.Gate.Release();
            second.Gate.Release();
            var results = await Task.WhenAll(calls).WaitAsync(TestLimits.Http);
            for (var index = 0; index < results.Length; index++)
            {
                var identity = index % 2 == 0 ? "one" : "two";
                Assert.Equal($"{identity}:request-{index}:{index}", ToolResults.Text(ToolResults.Downstream(results[index])));
            }
        }
        finally
        {
            deadline.Cancel();
            first.Gate.Release();
            second.Gate.Release();
        }
    }

    [Fact]
    public async Task ThreeClientConnectionCyclesDoNotInterruptAnExistingClientCall()
    {
        await using var downstream = await TestHttpServer.StartAsync("remote");
        await using var gateway = await TestProcess.StartAsync("streamable-http", downstream.Options("remote"));
        using var deadline = new CancellationTokenSource(TestLimits.Http);
        var stable = gateway.Client.CallToolAsync("remote/wait", ToolResults.Arguments("stable", 0), cancellationToken: deadline.Token).AsTask();
        try
        {
            await downstream.Gate.WaitForArrivalsAsync();
            for (var cycle = 0; cycle < 3; cycle++)
            {
                await using var transient = await gateway.ConnectAnotherClientAsync();
                var tools = await transient.ListToolsAsync(cancellationToken: deadline.Token);
                Assert.Equal(3, tools.Count);
                var result = await transient.CallToolAsync("remote/echo", ToolResults.Arguments("cycle", cycle), cancellationToken: deadline.Token);
                Assert.Equal($"remote:cycle:{cycle}", ToolResults.Text(ToolResults.Downstream(result)));
                Assert.False(stable.IsCompleted);
            }
            downstream.Gate.Release();
            Assert.Equal("remote:stable:0", ToolResults.Text(ToolResults.Downstream(await stable.WaitAsync(TestLimits.Http))));
        }
        finally
        {
            deadline.Cancel();
            downstream.Gate.Release();
        }
    }

    [Fact]
    public async Task ThreeDownstreamReconnectCyclesDoNotInterruptTheOtherDownstream()
    {
        await using var primary = await TestHttpServer.StartAsync("primary");
        await using var secondary = await TestHttpServer.StartAsync("secondary");
        await using var gateway = await TestGateway.StartAsync(primary.Options("primary"), secondary.Options("secondary"));
        var stable = gateway.CallAsync("primary/wait", ToolResults.Arguments("stable", 0));
        try
        {
            await primary.Gate.WaitForArrivalsAsync();
            for (var cycle = 0; cycle < 3; cycle++)
            {
                await gateway.Manager.DisconnectAsync("secondary").WaitAsync(TestLimits.Cleanup);
                Assert.Equal(McpServerState.Disconnected, gateway.Manager.Connections["secondary"].State);
                await gateway.Manager.ReconnectAsync("secondary").WaitAsync(TestLimits.Http);
                Assert.Equal(McpServerState.Connected, gateway.Manager.Connections["secondary"].State);
                Assert.Equal($"secondary:cycle:{cycle}", ToolResults.Text(await gateway.CallAsync("secondary/echo", ToolResults.Arguments("cycle", cycle))));
                Assert.False(stable.IsCompleted);
            }
            primary.Gate.Release();
            Assert.Equal("primary:stable:0", ToolResults.Text(await stable));
        }
        finally
        {
            primary.Gate.Release();
        }
    }

    [Fact]
    public async Task ConcurrentToolFailureDoesNotChangeTheHealthyClientResult()
    {
        await using var healthy = await TestHttpServer.StartAsync("healthy");
        await using var failing = await TestHttpServer.StartAsync("failing");
        await using var gateway = await TestProcess.StartAsync("streamable-http", healthy.Options("healthy"), failing.Options("failing"));
        await using var other = await gateway.ConnectAnotherClientAsync();
        using var deadline = new CancellationTokenSource(TestLimits.Http);
        Task<CallToolResult>[] calls = [
            gateway.Client.CallToolAsync("healthy/echo", ToolResults.Arguments("request", 7), cancellationToken: deadline.Token).AsTask(),
            other.CallToolAsync("failing/fail", cancellationToken: deadline.Token).AsTask()
        ];
        var results = await Task.WhenAll(calls).WaitAsync(TestLimits.Http);
        Assert.False(results[0].IsError == true);
        var success = ToolResults.Downstream(results[0]);
        Assert.False(success.IsError == true);
        Assert.Equal("healthy:request:7", ToolResults.Text(success));
        var failure = ToolResults.Downstream(results[1]);
        Assert.True(failure.IsError);
        Assert.Equal("failing:controlled failure", ToolResults.Text(failure));
    }
}