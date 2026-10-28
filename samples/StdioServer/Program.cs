using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

[McpServerToolType]
public static class BasicTools
{
    [McpServerTool]
    [Description("A - Returns a simple hello message.")]
    public static string Hello(IConfiguration configuration)
    {
        return configuration["Greeting"] ?? "Hello from Server A!";
    }

    [McpServerTool]
    [Description("A - Returns a simple goodbye message.")]
    public static string Goodbye()
    {
        return "Goodbye from Server A!";
    }
}
