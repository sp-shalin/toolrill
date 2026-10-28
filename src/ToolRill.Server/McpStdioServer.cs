using ToolRill.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ToolRill.Server;

public static class McpStdioServer
{
    public static async Task RunAsync(
        McpToolRegistry registry,
        McpHostTools hostTools,
        CancellationToken cancellationToken = default,
        McpDiagnostics? diagnostics = null)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new McpDiagnosticLoggerProvider(
            diagnostics ?? new McpDiagnostics(new McpDiagnosticsOptions())));

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithGatewayTools(registry, hostTools);

        await builder.Build().RunAsync(cancellationToken);
    }
}