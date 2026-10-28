namespace ToolRill.Core;

public sealed class McpServerManager : IAsyncDisposable
{
    private bool _shutdownRequested;
    private readonly Dictionary<string, McpServerConnection> _connections = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, McpServerOptions> _serverOptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly McpDiagnostics _diagnostics;
    private readonly Dictionary<string, string> _serverStates = new(StringComparer.OrdinalIgnoreCase);

    public bool ShutdownRequested => _shutdownRequested;
    public IReadOnlyDictionary<string, McpServerConnection> Connections => _connections;

    public McpServerManager(McpDiagnostics diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public async Task ConnectAsync(
        IEnumerable<McpServerOptions> servers,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var server in servers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!server.Enabled)
            {
                _serverStates[server.Name] = "Disabled";
                continue;
            }

            if (_connections.ContainsKey(server.Name))
            {
                _diagnostics.Error($"Duplicate MCP server name: '{server.Name}'.");

                throw new InvalidOperationException($"Duplicate MCP server name: '{server.Name}'.");
            }

            _serverOptions[server.Name] = server;

            _diagnostics.Info($"Creating connection for '{server.Name}'.");

            var connection = new McpServerConnection(server.Name, _diagnostics);

            _connections.Add(server.Name, connection);

            try
            {
                _serverStates[server.Name] = "Connecting";

                await connection.ConnectAsync(server, cancellationToken);

                _serverStates[server.Name] = "Connected";
            }
            catch
            {
                _diagnostics.ServerError(server.Name, "Server connection failed.");

                await connection.DisconnectAsync();

                _serverStates[server.Name] = "Failed";

                throw;
            }
        }
    }

    public async Task DisconnectAsync(string serverName)
    {
        if (!_connections.TryGetValue(serverName, out var connection))
        {
            _diagnostics.Error($"MCP server '{serverName}' is not configured.");

            throw new InvalidOperationException($"MCP server '{serverName}' is not configured.");
        }

        _diagnostics.ServerInfo(serverName, "Disconnect requested.");

        await connection.DisconnectAsync();
    }

    public async Task ReconnectAsync(string serverName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_connections.TryGetValue(serverName, out var connection))
        {
            _diagnostics.Error($"MCP server '{serverName}' is not configured.");

            throw new InvalidOperationException($"MCP server '{serverName}' is not configured.");
        }

        var server = GetServerOptions(serverName);

        _diagnostics.ServerInfo(serverName, "Reconnect requested.");

        await connection.DisconnectAsync();

        await connection.ConnectAsync(server, cancellationToken);

        _diagnostics.ServerInfo(serverName, "Reconnect completed.");
    }

    public async Task DisconnectAllAsync()
    {
        if (_shutdownRequested)
        {
            return;
        }

        _shutdownRequested = true;

        _diagnostics.Info("Disconnecting all MCP servers.");

        foreach (var connection in _connections.Values)
        {
            await connection.DisconnectAsync();
        }

        _diagnostics.Info("All MCP servers disconnected.");
    }

    public async ValueTask DisposeAsync()
    {
        _diagnostics.Info("MCP server manager shutting down.");

        await DisconnectAllAsync();

        _diagnostics.Info("MCP server manager shutdown complete.");
    }

    private McpServerOptions GetServerOptions(string serverName)
    {
        if (!_serverOptions.TryGetValue(serverName, out var server))
        {
            throw new InvalidOperationException(
                $"MCP server '{serverName}' configuration was not found.");
        }

        return server;
    }

    public IReadOnlyDictionary<string, string> GetServerStates()
    {
        return new Dictionary<string, string>(_serverStates, StringComparer.OrdinalIgnoreCase);
    }

    public McpHostStatus GetStatus(int configuredServers, int aggregatedTools)
    {
        var connected = _serverStates.Values.Count(
                            state => state.Equals("Connected", StringComparison.OrdinalIgnoreCase)
                            );

        var failed = _serverStates.Values.Count(
                        state => state.Equals("Failed", StringComparison.OrdinalIgnoreCase)
                        );

        return new McpHostStatus
        {
            State = _shutdownRequested ? "Stopped" : "Running",
            ConfiguredServers = configuredServers,
            ConnectedServers = connected,
            FailedServers = failed,
            AggregatedTools = aggregatedTools
        };
    }
}