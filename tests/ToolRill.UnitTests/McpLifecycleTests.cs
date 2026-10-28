using ToolRill.Core;
using Xunit;

namespace ToolRill.UnitTests;

public sealed class McpLifecycleTests
{
    private static McpDiagnostics Diagnostics() => new(new() { Enabled = false });

    [Fact]
    public async Task NewConnectionRejectsDiscoveryAndCallsUntilConnected()
    {
        await using var connection = new McpServerConnection("server", Diagnostics());
        Assert.Equal(McpServerState.Created, connection.State);
        var discovery = await Assert.ThrowsAsync<InvalidOperationException>(() => connection.ListToolsAsync());
        var call = await Assert.ThrowsAsync<InvalidOperationException>(() => connection.CallToolAsync("echo", new Dictionary<string, object?>()));
        Assert.Contains("not connected", discovery.Message);
        Assert.Contains("not connected", call.Message);
        await connection.DisconnectAsync();
        await connection.DisconnectAsync();
        Assert.Equal(McpServerState.Disconnected, connection.State);
    }

    [Fact]
    public async Task UnsupportedTransportSetsFailedStateAndCanBeDisposed()
    {
        await using var connection = new McpServerConnection("server", Diagnostics());
        await Assert.ThrowsAsync<InvalidOperationException>(() => connection.ConnectAsync(new()
        {
            Name = "server", Transport = "unknown"
        }));
        Assert.Equal(McpServerState.Failed, connection.State);
        await connection.DisconnectAsync();
        Assert.Equal(McpServerState.Disconnected, connection.State);
    }

    [Fact]
    public async Task PreCancelledConnectionOperationsDoNotStart()
    {
        await using var connection = new McpServerConnection("server", Diagnostics());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connection.ConnectAsync(new(), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connection.ListToolsAsync(cancellation.Token));
        Assert.Equal(McpServerState.Created, connection.State);
    }

    [Fact]
    public async Task PreCancelledManagerDoesNotCreateConnections()
    {
        await using var manager = new McpServerManager(Diagnostics());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.ConnectAsync([
            new() { Name = "server" }
        ], cancellation.Token));
        Assert.Empty(manager.Connections);
        Assert.Empty(manager.GetServerStates());
    }

    [Fact]
    public async Task DisabledServersAndRepeatedShutdownAreHandledWithoutConnections()
    {
        await using var manager = new McpServerManager(Diagnostics());
        var snapshot = manager.GetServerStates();
        await manager.ConnectAsync([new() { Name = "disabled", Enabled = false }]);
        Assert.Empty(manager.Connections);
        Assert.Empty(snapshot);
        Assert.Equal("Disabled", manager.GetServerStates()["DISABLED"]);
        await manager.DisconnectAllAsync();
        await manager.DisconnectAllAsync();
        Assert.True(manager.ShutdownRequested);
        Assert.Equal("Stopped", manager.GetStatus(1, 0).State);
    }

    [Fact]
    public async Task UnknownServerDisconnectAndReconnectAreRejected()
    {
        await using var manager = new McpServerManager(Diagnostics());
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.DisconnectAsync("missing"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.ReconnectAsync("missing"));
    }
}