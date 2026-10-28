using ModelContextProtocol.Client;

namespace ToolRill.Client;

public static class StdioTransport
{
    public static async Task<StdioClientTransport> CreateAsync(
        string serverName,
        string command,
        string projectPath,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? args = null,
        IReadOnlyDictionary<string, string>? env = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = StdioConnectionSettings.Resolve(serverName, command, projectPath, args ?? [], env);

        var transport = new StdioClientTransport(
                            new StdioClientTransportOptions
                            {
                                Name = serverName,
                                Command = settings.Command,
                                Arguments = settings.Arguments.ToList(),
                                EnvironmentVariables = settings.EnvironmentVariables
                            });

        return transport;
    }
}