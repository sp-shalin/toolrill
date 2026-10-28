namespace ToolRill.Core;

public sealed class McpHostToolDefinition
{
    public string Name { get; }

    public string ServerName { get; }

    public string Description { get; }

    public McpHostToolDefinition(
        string name,
        string serverName,
        string description)
    {
        Name = name;
        ServerName = serverName;
        Description = description;
    }
}