using System.Text.Json;
using System.Text.Json.Nodes;
using ToolRill.Core;
using Microsoft.Extensions.Configuration;

namespace ToolRill.Cli.Services;

internal static class CliConfiguration
{
    public const string DefaultPath = "toolrill.json";
    public const string DefaultHost = "default";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static IConfigurationRoot Load(string configPath)
    {
        var fullPath = Path.GetFullPath(configPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Configuration file not found: '{fullPath}'.",
                fullPath);
        }

        var environment =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Production";

        return new ConfigurationBuilder()
            .SetBasePath(Path.GetDirectoryName(fullPath)!)
            .AddJsonFile(
                Path.GetFileName(fullPath),
                optional: false,
                reloadOnChange: false)
            .AddJsonFile(
                Path.GetFileNameWithoutExtension(fullPath)
                    + "." + environment
                    + Path.GetExtension(fullPath),
                optional: true,
                reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "TOOLRILL_")
            .Build();
    }

    public static JsonObject ReadDocument(string configPath)
    {
        var json = File.ReadAllText(Path.GetFullPath(configPath));

        return JsonNode.Parse(
            json,
            new JsonNodeOptions { PropertyNameCaseInsensitive = true },
            new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            }) as JsonObject
            ?? throw new InvalidOperationException(
                "Configuration must be a JSON object.");
    }

    public static List<ToolRillHostConfiguration> GetHosts(IConfiguration configuration)
    {
        var hosts = configuration.GetSection("hosts")
            .Get<List<ToolRillHostConfiguration>>() ?? [];

        ValidateHostNames(hosts.Select(host => host.Name));

        foreach (var host in hosts)
        {
            if (host.Incoming is null || host.Outgoing is null)
            {
                throw new InvalidOperationException(
                    $"Host '{host.Name}' requires 'incoming' settings and an 'outgoing' array.");
            }
        }

        return hosts;
    }

    public static ToolRillHostConfiguration GetHost(IConfiguration configuration, string name)
        => GetHosts(configuration).FirstOrDefault(host =>
            string.Equals(host.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Host '{name}' was not found in the configuration.");

    public static JsonArray GetServers(JsonObject document, string hostName = DefaultHost)
    {
        var hosts = document["hosts"] as JsonArray
            ?? throw new InvalidOperationException("The 'hosts' setting must be a JSON array.");

        if (hosts.Any(host => host is not JsonObject))
        {
            throw new InvalidOperationException("Each host must be a JSON object.");
        }

        ValidateHostNames(hosts.Select(host => host!["name"]?.GetValue<string>() ?? ""));

        var selected = hosts.OfType<JsonObject>().FirstOrDefault(host =>
            string.Equals(host["name"]!.GetValue<string>(), hostName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Host '{hostName}' was not found in the base configuration.");

        if (selected["incoming"] is not JsonObject)
        {
            throw new InvalidOperationException($"Host '{hostName}' requires an 'incoming' object.");
        }

        return selected["outgoing"] as JsonArray
            ?? throw new InvalidOperationException(
                $"Host '{hostName}' requires an 'outgoing' array.");
    }

    public static void ValidateHost(ToolRillHostConfiguration host)
    {
        var incoming = host.Incoming!;

        if (string.Equals(incoming.Transport, "streamable-http", StringComparison.OrdinalIgnoreCase))
        {
            ValidateHttpUrl(host.Name, incoming.Url);
        }
        else if (!string.Equals(incoming.Transport, "stdio", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Host '{host.Name}' incoming transport must be stdio or streamable-http.");
        }

        McpConfigurationValidator.Validate(host.Outgoing!);
    }

    public static void ValidateHttpUrl(string hostName, string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var address)
            || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"Host '{hostName}' requires an absolute HTTP(S) incoming URL.");
        }
    }

    private static void ValidateHostNames(IEnumerable<string> names)
    {
        var uniqueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Each host requires a nonempty name.");
            }

            if (!uniqueNames.Add(name))
            {
                throw new InvalidOperationException($"Duplicate host name: '{name}'.");
            }
        }

        if (uniqueNames.Count == 0)
        {
            throw new InvalidOperationException("No ToolRill hosts are configured.");
        }
    }

    public static List<McpServerOptions> ReadServers(JsonArray servers)
    {
        if (servers.Any(server => server is not JsonObject))
        {
            throw new InvalidOperationException(
                "Each server must be a JSON object.");
        }

        return servers.Deserialize<List<McpServerOptions>>(JsonOptions) ?? [];
    }

    public static void Save(string configPath, JsonObject document)
    {
        var json = document.ToJsonString(JsonOptions) + Environment.NewLine;
        File.WriteAllText(Path.GetFullPath(configPath), json);
    }
}

internal sealed class ToolRillHostConfiguration
{
    public string Name { get; set; } = "";

    public IncomingConfiguration? Incoming { get; set; }

    public List<McpServerOptions>? Outgoing { get; set; } = [];
}

internal sealed class IncomingConfiguration
{
    public string Transport { get; set; } = "";

    public string? Url { get; set; }
}