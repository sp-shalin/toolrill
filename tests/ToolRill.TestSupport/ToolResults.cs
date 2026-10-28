using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace ToolRill.TestSupport;

public static class ToolResults
{
    public static string Text(CallToolResult result)
        => result.Content.Single() is TextContentBlock content
            ? content.Text
            : throw new InvalidOperationException("Expected exactly one text content block.");

    public static CallToolResult Downstream(CallToolResult upstream)
        => JsonSerializer.Deserialize<CallToolResult>(Text(upstream), McpJsonUtilities.DefaultOptions)
            ?? throw new InvalidOperationException("Expected a serialized downstream result.");

    public static Dictionary<string, object?> Arguments(string text, int number)
        => new() { ["text"] = text, ["number"] = number };
}