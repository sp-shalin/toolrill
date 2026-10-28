using ModelContextProtocol.Client;

namespace ToolRill.Core;

public sealed class McpToolInfo
{
    public string ServerName { get; }

    public McpClientTool Tool { get; }

    public string Name => Tool.Name;

    public string Description => Tool.Description ?? string.Empty;

    public string QualifiedName => $"{ServerName}/{Tool.Name}";

    public McpToolInfo(
        string serverName,
        McpClientTool tool)
    {
        ServerName = serverName;
        Tool = tool;
    }
}