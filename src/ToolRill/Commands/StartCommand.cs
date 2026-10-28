using ToolRill.Cli.Services;
using ToolRill.Core;
using ToolRill.Server;
using Microsoft.Extensions.Configuration;

namespace ToolRill.Cli.Commands;

internal static class StartCommand
{
    public static Task ExecuteAsync(
        string? mode,
        string configPath,
        string? url,
        CancellationToken cancellationToken,
        string hostName = CliConfiguration.DefaultHost)
        => RunHostAsync(
            configPath,
            hostName,
            mode,
            url,
            cancellationToken);

    private static async Task RunHostAsync(
        string configPath,
        string hostName,
        string? mode,
        string? url,
        CancellationToken cancellationToken)
    {
        var configuration = CliConfiguration.Load(configPath);
        using var configurationLifetime = configuration as IDisposable;
        McpServerManager? manager = null;
        var servers = new List<McpServerOptions>();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var host = CliConfiguration.GetHost(configuration, hostName);
            var incoming = host.Incoming!;
            var configuredMode = incoming.Transport?.ToLowerInvariant() switch
            {
                "stdio" => "stdio",
                "streamable-http" => "serve-http",
                _ => throw new InvalidOperationException(
                    $"Host '{host.Name}' incoming transport must be stdio or streamable-http.")
            };

            mode = mode?.ToLowerInvariant() ?? configuredMode;

            if (mode is not ("stdio" or "serve-http"))
            {
                throw new ArgumentException(
                    $"Unsupported start mode '{mode}'. Use stdio or serve-http.");
            }

            if (mode == "stdio" && url is not null)
            {
                throw new ArgumentException("--url is only supported for serve-http.");
            }

            if (mode == "serve-http")
            {
                url ??= incoming.Url;
                if (configuredMode == "stdio")
                {
                    url ??= "http://127.0.0.1:3001";
                }

                CliConfiguration.ValidateHttpUrl(host.Name, url);
            }

            servers = host.Outgoing!;

            McpConfigurationValidator.Validate(servers);

            var diagnosticsOptions = configuration
                .GetSection("diagnostics")
                .Get<McpDiagnosticsOptions>() ?? new McpDiagnosticsOptions();

            var diagnostics = new McpDiagnostics(diagnosticsOptions);

            manager = new McpServerManager(diagnostics);

            diagnostics.ServerInfo("host", $"Configured servers: {servers.Count}.");
            diagnostics.ServerInfo("host", "Connecting to MCP servers.");

            await manager.ConnectAsync(servers, cancellationToken);

            var toolRegistry = new McpToolRegistry();

            foreach (var server in manager.Connections)
            {
                var tools = await server.Value.ListToolsAsync(cancellationToken);
                var serverOptions = servers.First(options =>
                    string.Equals(options.Name, server.Key, StringComparison.OrdinalIgnoreCase));

                toolRegistry.Add(server.Key, tools, serverOptions.Tools);
            }

            diagnostics.Info($"Aggregated tools: {toolRegistry.Tools.Count}.");
            diagnostics.ServerInfo(
                "host",
                $"Aggregated tools: {toolRegistry.Tools.Count}.");

            var toolRouter = new McpToolRouter(manager, toolRegistry);

            var hostTools = new McpHostTools(
                toolRegistry,
                toolRouter,
                manager,
                diagnostics);

            // In stdio mode, stdout is reserved for MCP protocol messages.
            void PrintStatus()
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("Available host tools:");

                foreach (var tool in hostTools.ListTools())
                {
                    McpLogRedactor.WriteError($"- {tool.Name}");
                    McpLogRedactor.WriteError($"  Server: {tool.ServerName}");
                    McpLogRedactor.WriteError($"  Description: {tool.Description}");
                    Console.Error.WriteLine();
                }

                var status = manager.GetStatus(
                    servers.Count,
                    toolRegistry.Tools.Count);

                Console.Error.WriteLine("Server status:");

                foreach (var server in manager.GetServerStates())
                {
                    McpLogRedactor.WriteError($"- {server.Key}: {server.Value}");
                }

                Console.Error.WriteLine();
                Console.Error.WriteLine($"Configured servers: {status.ConfiguredServers}");
                Console.Error.WriteLine($"Connected servers: {status.ConnectedServers}");
                Console.Error.WriteLine($"Failed servers: {status.FailedServers}");
                Console.Error.WriteLine($"Aggregated tools: {status.AggregatedTools}");
            }

            PrintStatus();

            if (mode == "stdio")
            {
                await McpStdioServer.RunAsync(
                    toolRegistry,
                    hostTools,
                    cancellationToken,
                    diagnostics);
            }
            else
            {
                Console.Error.WriteLine();
                McpLogRedactor.WriteError($"MCP HTTP server starting at {url}/mcp");

                await McpHttpServer.RunAsync(
                    toolRegistry,
                    hostTools,
                    url!,
                    cancellationToken,
                    diagnostics);
            }
        }
        finally
        {
            if (manager is not null)
            {
                foreach (var server in servers)
                {
                    if (!server.Enabled)
                    {
                        continue;
                    }

                    try
                    {
                        await manager.DisconnectAsync(server.Name);
                    }
                    catch (Exception ex)
                    {
                        McpLogRedactor.WriteError(
                            $"Failed to disconnect '{server.Name}': {ex.Message}");
                    }
                }

                await manager.DisposeAsync();
            }

        }
    }

    public static async Task RunManagerAsync(
        string configPath,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(configPath);
        var configuration = CliConfiguration.Load(fullPath);
        using var configurationLifetime = configuration as IDisposable;
        var hosts = CliConfiguration.GetHosts(configuration)
            .Where(host => string.Equals(host.Incoming!.Transport,
                "streamable-http", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (hosts.Count == 0)
        {
            throw new InvalidOperationException(
                "No HTTP hosts are configured.");
        }

        foreach (var host in hosts)
        {
            CliConfiguration.ValidateHost(host);
        }

        var instances = hosts
            .Select(host => new McpHttpInstance(host.Name, fullPath, host.Incoming!.Url!))
            .ToList();

        var duplicateUrls = instances
            .GroupBy(
                instance => new Uri(instance.Url).GetLeftPart(UriPartial.Authority),
                StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateUrls.Count > 0)
        {
            throw new InvalidOperationException(
            $"Duplicate HTTP listening addresses: {string.Join(", ", duplicateUrls)}");
        }

        var runner = new HttpInstanceRunner();

        try
        {
            Console.WriteLine(
                $"Starting {instances.Count} ToolRill HTTP host(s).");

            await runner.StartAsync(instances, cancellationToken);

            foreach (var instance in instances)
            {
                Console.WriteLine(McpLogRedactor.Redact($"{instance.Name}: {instance.Url}/mcp"));
            }

            Console.WriteLine("Press Ctrl+C to stop all hosts.");

            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        finally
        {
            await runner.StopAsync();
        }
    }
}