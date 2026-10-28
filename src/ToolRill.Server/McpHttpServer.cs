using ToolRill.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ToolRill.Server;

public static class McpHttpServer
{
    public static async Task RunAsync(
        McpToolRegistry registry,
        McpHostTools hostTools,
        string url = "http://127.0.0.1:3001",
        CancellationToken cancellationToken = default,
        McpDiagnostics? diagnostics = null)
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseUrls(url);

        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new McpDiagnosticLoggerProvider(
            diagnostics ?? new McpDiagnostics(new McpDiagnosticsOptions())));

        builder.Services
            .AddMcpServer()
            .WithHttpTransport()
            .WithGatewayTools(registry, hostTools);
            
        var app = builder.Build();

        app.MapMcp("/mcp");
        app.MapGet("/health", () => TypedResults.Ok());

        await app.RunAsync(cancellationToken);
    }
}