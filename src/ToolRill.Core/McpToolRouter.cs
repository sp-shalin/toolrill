namespace ToolRill.Core;

public sealed class McpToolRouter
{
    private readonly McpServerManager _serverManager;
    private readonly McpToolRegistry _toolRegistry;

    public McpToolRouter(
        McpServerManager serverManager,
        McpToolRegistry toolRegistry)
    {
        _serverManager = serverManager;
        _toolRegistry = toolRegistry;
    }

    public async Task<dynamic> CallAsync(
        string qualifiedToolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var separatorIndex = qualifiedToolName.IndexOf('/');

        if (separatorIndex <= 0 || separatorIndex == qualifiedToolName.Length - 1)
        {
            throw new InvalidOperationException(
                $"Invalid tool name: '{qualifiedToolName}'. Expected format 'server/tool'.");
        }

        var serverName = qualifiedToolName[..separatorIndex];
        var toolName = qualifiedToolName[(separatorIndex + 1)..];
        var tool = _toolRegistry.Find(serverName, toolName);

        if (tool is null)
        {
            throw new InvalidOperationException(
                $"Tool '{qualifiedToolName}' was not found.");
        }

        if (!_serverManager.Connections.TryGetValue(serverName, out var connection))
        {
            throw new InvalidOperationException(
                $"MCP server '{serverName}' is not connected.");
        }

        return await connection.CallToolAsync(toolName, arguments, cancellationToken);
    }
}