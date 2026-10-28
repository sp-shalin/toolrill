namespace ToolRill.Client;

public sealed record StdioConnectionSettings(
    string Command, IReadOnlyList<string> Arguments, Dictionary<string, string?> EnvironmentVariables)
{
    public static StdioConnectionSettings Resolve(
        string serverName, string command, string? projectPath,
        IReadOnlyList<string>? args, IReadOnlyDictionary<string, string>? env = null)
    {
        if (string.IsNullOrWhiteSpace(command) || command.Contains('\0'))
        {
            throw new InvalidOperationException($"Server '{serverName}' requires a command naming an executable.");
        }

        if (args is null || args.Any(argument => argument is null || argument.Contains('\0')))
        {
            throw new InvalidOperationException($"Server '{serverName}' requires a non-null args array of non-null strings without NUL characters.");
        }

        var hasProject = !string.IsNullOrWhiteSpace(projectPath);
        if (hasProject && (projectPath!.Contains('\0') || args.Count > 0))
        {
            throw new InvalidOperationException($"Server '{serverName}' cannot combine projectPath with args or use NUL in projectPath.");
        }

        var environment = new Dictionary<string, string?>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var entry in env ?? new Dictionary<string, string>())
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Key.IndexOfAny(['=', '\0']) >= 0
                || entry.Value is null)
            {
                throw new InvalidOperationException($"Server '{serverName}' has an invalid env entry.");
            }

            var value = HttpConnectionSettings.Expand(serverName, entry.Value, sensitive: true);
            if (value.Contains('\0') || !environment.TryAdd(entry.Key, value))
            {
                throw new InvalidOperationException($"Server '{serverName}' has an invalid or duplicate env entry.");
            }
        }

        return new(command, hasProject ? ["run", "--project", projectPath!] : args.ToArray(), environment);
    }
}