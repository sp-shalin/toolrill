namespace ToolRill.Core;

public sealed class McpDiagnosticsOptions
{
    public bool Enabled { get; set; } = true;

    public string Level { get; set; } = "INFO";
}