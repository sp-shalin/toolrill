using ModelContextProtocol.Client;

namespace ToolRill.Client;

public static class HttpTransport
{
    public static async Task<HttpClientTransport> CreateAsync(
        string serverName,
        string url,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = HttpConnectionSettings.Resolve(serverName, url, headers);

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = settings.Endpoint,
                AdditionalHeaders = settings.Headers,
                TransportMode = HttpTransportMode.StreamableHttp
            }, new HttpClient { Timeout = Timeout.InfiniteTimeSpan }, ownsHttpClient: true);

        return transport;
    }
}