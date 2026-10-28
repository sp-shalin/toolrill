using ModelContextProtocol.Client;
using ToolRill.Client;

namespace ToolRill.Core;

public sealed class McpServerConnection : IAsyncDisposable
{
    private McpClient? _client;
    private readonly McpDiagnostics _diagnostics;
    private TimeSpan _connectionTimeout = TimeSpan.FromSeconds(30);
    private TimeSpan _toolCallTimeout = TimeSpan.FromSeconds(60);

    public string ServerName { get; }
    public McpServerState State { get; private set; }

    public McpServerConnection(string serverName, McpDiagnostics diagnostics)
    {
        ServerName = serverName;
        _diagnostics = diagnostics;

        State = McpServerState.Created;

        _diagnostics.ServerInfo(ServerName, "Connection created.");
    }

    public async Task ConnectAsync(
        McpServerOptions server,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (State == McpServerState.Connected)
        {
            _diagnostics.ServerWarning(ServerName, "Connect requested but server is already connected.");
            return;
        }

        McpConfigurationValidator.ValidateTimeouts(server);
        _connectionTimeout = TimeSpan.FromSeconds(server.ConnectionTimeoutSeconds);
        _toolCallTimeout = TimeSpan.FromSeconds(server.ToolCallTimeoutSeconds);
        State = McpServerState.Connecting;

        _diagnostics.ServerInfo(ServerName, $"Connecting using {server.Transport} transport...");

        try
        {
            _client = await TimeoutOperation.RunAsync(async token =>
            {
                IClientTransport transport;
                if (server.Transport.Equals("stdio", StringComparison.OrdinalIgnoreCase))
                {
                    transport = await StdioTransport.CreateAsync(
                        ServerName, server.Command, server.ProjectPath, token, server.Args, server.Env);
                }
                else if (server.Transport.Equals("streamable-http", StringComparison.OrdinalIgnoreCase))
                {
                    transport = await HttpTransport.CreateAsync(ServerName, server.Url, token, server.Headers);
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported MCP transport: '{server.Transport}'.");
                }

                return await McpClient.CreateAsync(transport,
                    new() { InitializationTimeout = _connectionTimeout }, cancellationToken: token);
            }, _connectionTimeout, cancellationToken);

            State = McpServerState.Connected;

            _diagnostics.ServerInfo(ServerName, "Connected.");
        }
        catch (Exception ex)
        {
            State = McpServerState.Failed;

            _diagnostics.ServerError(ServerName, $"Connection failed: {ex.Message}");

            throw;
        }
    }

    public async Task<IReadOnlyList<McpClientTool>> ListToolsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        _diagnostics.ServerInfo(ServerName, "Discovering tools...");

        var tools = await TimeoutOperation.RunAsync(
            async token => await _client!.ListToolsAsync(cancellationToken: token),
            _connectionTimeout, cancellationToken);

        _diagnostics.ServerInfo(ServerName, $"Discovered {tools.Count} tool(s).");

        return tools.ToList();
    }

    public async Task<dynamic> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        _diagnostics.ServerInfo(ServerName, $"Calling tool '{toolName}'.");

        try
        {
            var result = await TimeoutOperation.RunAsync(
                async token => await _client!.CallToolAsync(toolName, arguments, cancellationToken: token),
                _toolCallTimeout, cancellationToken);

            _diagnostics.ServerInfo(ServerName, $"Tool '{toolName}' completed.");

            return result;
        }
        catch (Exception ex)
        {
            _diagnostics.ServerError(ServerName, $"Tool '{toolName}' failed: {ex.Message}");

            throw;
        }
    }

    public async ValueTask DisconnectAsync()
    {
        if (_client is null)
        {
            State = McpServerState.Disconnected;

            _diagnostics.ServerInfo(ServerName, "Already disconnected.");

            return;
        }

        if (State == McpServerState.Disconnected)
        {
            return;
        }

        State = McpServerState.Disconnecting;

        _diagnostics.ServerInfo(ServerName, "Disconnecting...");

        try
        {
            await _client.DisposeAsync();

            _client = null;

            State = McpServerState.Disconnected;

            _diagnostics.ServerInfo(ServerName, "Disconnected.");
        }
        catch (Exception ex)
        {
            State = McpServerState.Failed;

            _diagnostics.ServerError(ServerName, $"Disconnect failed: {ex.Message}");

            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    private void EnsureConnected()
    {
        if (_client is null || State != McpServerState.Connected)
        {
            throw new InvalidOperationException($"MCP server '{ServerName}' is not connected. Current state: {State}.");
        }
    }
}