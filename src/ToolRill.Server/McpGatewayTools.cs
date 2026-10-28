using System.Text.Json;
using ToolRill.Core;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace ToolRill.Server;

internal static class McpGatewayTools
{
    public static IMcpServerBuilder WithGatewayTools(
        this IMcpServerBuilder builder,
        McpToolRegistry registry,
        McpHostTools hostTools)
    {
        return builder
            .WithListToolsHandler((request, cancellationToken) =>
                ValueTask.FromResult(new ListToolsResult
                {
                    Tools = registry.Tools.Select(tool => new Tool
                    {
                        Name = tool.QualifiedName,
                        Description = tool.Description,
                        InputSchema = tool.Tool.ProtocolTool.InputSchema
                    }).ToList()
                }))
            .WithCallToolHandler(async (request, cancellationToken) =>
            {
                var parameters = request.Params
                    ?? throw new McpProtocolException("Tool call parameters are required.", McpErrorCode.InvalidParams);
                if (!registry.Tools.Any(tool => string.Equals(
                    tool.QualifiedName, parameters.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new McpProtocolException($"Tool '{parameters.Name}' was not found.", McpErrorCode.InvalidParams);
                }

                var arguments = parameters.Arguments?.ToDictionary(
                    argument => argument.Key,
                    argument => (object?)argument.Value.Clone()) ?? [];
                var result = await hostTools.CallToolAsync(parameters.Name, arguments, cancellationToken);
                return new CallToolResult
                {
                    Content = [new TextContentBlock { Text = JsonSerializer.Serialize((object?)result) }]
                };
            });
    }
}