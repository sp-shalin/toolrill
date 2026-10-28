using ToolRill.Client;

namespace ToolRill.Core;

public static class McpConfigurationValidator
{
    public static void Validate(IReadOnlyList<McpServerOptions> servers)
    {
        if (servers.Count == 0)
        {
            throw new InvalidOperationException("No MCP servers are configured.");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var server in servers)
        {
            if (string.IsNullOrWhiteSpace(server.Name))
            {
                throw new InvalidOperationException("MCP server name cannot be empty.");
            }

            if (!names.Add(server.Name))
            {
                throw new InvalidOperationException($"Duplicate MCP server name: '{server.Name}'.");
            }

            if (!server.Enabled)
            {
                continue;
            }

            ValidateTimeouts(server);

            if (string.IsNullOrWhiteSpace(server.Transport))
            {
                throw new InvalidOperationException($"Server '{server.Name}' must specify a transport.");
            }

            if (server.Transport.Equals("stdio", StringComparison.OrdinalIgnoreCase))
            {
                ValidateStdio(server);
            }
            else if (server.Transport.Equals("streamable-http", StringComparison.OrdinalIgnoreCase))
            {
                ValidateStreamableHttp(server);
            }
            else
            {
                throw new InvalidOperationException($"Server '{server.Name}' uses unsupported transport '{server.Transport}'.");
            }
        }
    }

    internal static void ValidateTimeouts(McpServerOptions server)
    {
        if (server.ConnectionTimeoutSeconds is <= 0 or > 4294967
            || server.ToolCallTimeoutSeconds is <= 0 or > 4294967)
        {
            throw new InvalidOperationException(
                $"Server '{server.Name}' connectionTimeoutSeconds and toolCallTimeoutSeconds must be positive integers no greater than 4294967.");
        }
    }

    private static void ValidateStdio(McpServerOptions server)
    {
        StdioConnectionSettings.Resolve(server.Name, server.Command, server.ProjectPath, server.Args, server.Env);
    }

    private static void ValidateStreamableHttp(McpServerOptions server)
    {
        HttpConnectionSettings.Resolve(server.Name, server.Url, server.Headers);
    }
}