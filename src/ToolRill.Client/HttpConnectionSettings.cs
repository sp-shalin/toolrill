using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace ToolRill.Client;

public sealed record HttpConnectionSettings(Uri Endpoint, Dictionary<string, string> Headers)
{
    private static readonly ConcurrentDictionary<string, byte> SensitiveValues = new(StringComparer.Ordinal);
    private static readonly Regex Placeholder = new(@"\$\{([^{}]+)\}", RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public static HttpConnectionSettings Resolve(
        string serverName, string url, IReadOnlyDictionary<string, string>? headers = null)
    {
        var resolvedHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers ?? new Dictionary<string, string>())
        {
            if (header.Value is null)
            {
                throw new InvalidOperationException($"Server '{serverName}' has a null HTTP header value.");
            }
            var value = Expand(serverName, header.Value, sensitive: true);
            if (!string.IsNullOrEmpty(value))
            {
                SensitiveValues.TryAdd(value, 0);
            }

            try
            {
                using var request = new HttpRequestMessage();
                if (value.IndexOfAny(['\r', '\n', '\0']) >= 0)
                {
                    throw new FormatException();
                }
                request.Headers.Add(header.Key, value);
                if (!resolvedHeaders.TryAdd(header.Key, value))
                {
                    throw new FormatException();
                }
            }
            catch (Exception error) when (error is FormatException or ArgumentException or InvalidOperationException)
            {
                throw new InvalidOperationException($"Server '{serverName}' has an invalid or duplicate HTTP header.");
            }
        }

        var resolvedUrl = Expand(serverName, url);
        if (string.IsNullOrWhiteSpace(resolvedUrl))
        {
            throw new InvalidOperationException($"Server '{serverName}' requires a url.");
        }
        if (!Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"Server '{serverName}' has an invalid URL; use an absolute HTTP(S) URL.");
        }

        return new(endpoint, resolvedHeaders);
    }

    public static string RedactHeaderValues(string message)
    {
        foreach (var value in SensitiveValues.Keys.OrderByDescending(value => value.Length))
        {
            message = message.Replace(value, "[REDACTED]", StringComparison.Ordinal);
        }
        return message;
    }

    internal static string Expand(string serverName, string value, bool sensitive = false)
        => Placeholder.Replace(value, match =>
        {
            var name = match.Groups[1].Value;
            var expanded = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(expanded))
            {
                throw new InvalidOperationException(
                    $"Server '{serverName}' references missing or empty environment variable '{name}'.");
            }
            if (sensitive)
            {
                SensitiveValues.TryAdd(expanded, 0);
            }
            return expanded;
        });
}