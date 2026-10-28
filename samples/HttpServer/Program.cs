using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<BasicTools>();

var app = builder.Build();

app.MapMcp("/mcp");

app.Run();

public sealed class BasicTools
{
    [McpServerTool]
    [System.ComponentModel.Description("Returns a greeting from HttpServer.")]
    public string Hello()
    {
        return "Hello from HttpServer over Streamable HTTP!";
    }
}