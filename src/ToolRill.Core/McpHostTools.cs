namespace ToolRill.Core;

public sealed class McpHostTools
{
    private readonly McpToolRegistry _toolRegistry;
    private readonly McpToolRouter _toolRouter;
    private readonly McpServerManager _serverManager;
    private readonly McpDiagnostics _diagnostics;

    public McpHostTools(
        McpToolRegistry toolRegistry,
        McpToolRouter toolRouter,
        McpServerManager serverManager,
        McpDiagnostics diagnostics)
    {
        _toolRegistry = toolRegistry;
        _toolRouter = toolRouter;
        _serverManager = serverManager;
        _diagnostics = diagnostics;
    }

    public IReadOnlyList<McpHostToolDefinition> ListTools()
    {
        _diagnostics.Info("Listing host tools.");

        var tools = _toolRegistry.Tools
                        .Select(tool => new McpHostToolDefinition(
                            tool.QualifiedName,
                            tool.ServerName,
                            tool.Description))
                        .ToList();

        _diagnostics.Info($"Host tool count: {tools.Count}.");

        return tools;
    }

    public async Task<dynamic> CallToolAsync(
        string qualifiedToolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        _diagnostics.Info($"Host tool invocation: '{qualifiedToolName}'.");

        try
        {
            var result = await _toolRouter.CallAsync(qualifiedToolName, arguments, cancellationToken);

            _diagnostics.Info($"Host tool invocation completed: '{qualifiedToolName}'.");

            return result;
        }
        catch (Exception ex)
        {
            _diagnostics.Error($"Host tool invocation failed for '{qualifiedToolName}': {ex.Message}");

            throw;
        }
    }

    public IReadOnlyDictionary<string, McpServerState> GetServerStates()
    {
        return _serverManager.Connections
            .ToDictionary(
                connection => connection.Key,
                connection => connection.Value.State,
                StringComparer.OrdinalIgnoreCase);
    }
}