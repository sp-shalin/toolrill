using System.Text.RegularExpressions;

namespace ToolRill.Core;

public sealed class McpToolRegistry
{
    private readonly List<McpToolInfo> _tools = [];

    public IReadOnlyList<McpToolInfo> Tools => _tools;

    public void Add(
        string serverName,
        IEnumerable<ModelContextProtocol.Client.McpClientTool> tools,
        McpToolFilterOptions? filter = null)
    {
        foreach (var tool in tools)
        {
            if (filter is not null)
            {
                var included = filter.Include is null
                    || filter.Include.Count == 0
                    || filter.Include.Any(pattern => Matches(tool.Name, pattern));
                var excluded = filter.Exclude?.Any(pattern => Matches(tool.Name, pattern)) == true;

                if (!included || excluded)
                {
                    continue;
                }
            }

            var toolInfo = new McpToolInfo(serverName, tool);

            if (_tools.Any(existing =>
                    string.Equals(
                        existing.QualifiedName,
                        toolInfo.QualifiedName,
                        StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Duplicate tool: '{toolInfo.QualifiedName}'.");
            }

            _tools.Add(toolInfo);
        }
    }

    private static bool Matches(string toolName, string pattern)
    {
        var expression = "\\A" + Regex.Escape(pattern).Replace("\\*", ".*") + "\\z";

        return Regex.IsMatch(
            toolName,
            expression,
            RegexOptions.IgnoreCase
                | RegexOptions.CultureInvariant
                | RegexOptions.Singleline
                | RegexOptions.NonBacktracking);
    }

    public McpToolInfo? Find(string serverName, string toolName)
    {
        return _tools.FirstOrDefault(tool =>
            string.Equals(
                tool.ServerName,
                serverName,
                StringComparison.OrdinalIgnoreCase)
            &&
            string.Equals(
                tool.Name,
                toolName,
                StringComparison.OrdinalIgnoreCase));
    }
}