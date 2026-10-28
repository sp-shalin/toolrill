namespace ToolRill.Core;

public sealed class McpHostStatus
{
    public int ConfiguredServers { get; init; }

    public int ConnectedServers { get; init; }

    public int FailedServers { get; init; }

    public int AggregatedTools { get; init; }

    public string State { get; init; } = string.Empty;
}