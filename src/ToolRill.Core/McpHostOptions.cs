namespace ToolRill.Core;

public sealed class McpHostOptions
{
    public List<McpServerOptions> Servers { get; set; } = [];

    public McpDiagnosticsOptions Diagnostics { get; set; } = new();
}

public sealed class McpServerOptions
{
    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public McpToolFilterOptions? Tools { get; set; }

    public string Transport { get; set; } = "stdio";

    public string Command { get; set; } = string.Empty;

    public List<string> Arguments { get; set; } = [];

    public List<string> Args { get; set; } = [];

    public Dictionary<string, string> Env { get; set; } = [];

    public string ProjectPath { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public Dictionary<string, string> Headers { get; set; } = [];

    public int ConnectionTimeoutSeconds { get; set; } = 30;

    public int ToolCallTimeoutSeconds { get; set; } = 60;
}

public sealed class McpToolFilterOptions
{
    public List<string> Include { get; set; } = [];

    public List<string> Exclude { get; set; } = [];
}