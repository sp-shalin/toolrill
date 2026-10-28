using ToolRill.Core;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace ToolRill.TestSupport;

public static class TestLimits
{
    public static readonly TimeSpan Http = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan Stdio = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan Cleanup = TimeSpan.FromSeconds(15);
}

public sealed class TestGateway : IAsyncDisposable
{
    public McpServerManager Manager { get; }
    public McpToolRegistry Registry { get; } = new();
    public McpHostTools Tools { get; }

    private TestGateway()
    {
        var diagnostics = new McpDiagnostics(new() { Enabled = false });
        Manager = new McpServerManager(diagnostics);
        Tools = new McpHostTools(Registry, new McpToolRouter(Manager, Registry), Manager, diagnostics);
    }

    public static async Task<TestGateway> StartAsync(params McpServerOptions[] servers)
    {
        var gateway = new TestGateway();
        try
        {
            McpConfigurationValidator.Validate(servers);
            using var deadline = new CancellationTokenSource(TestLimits.Stdio);
            await gateway.Manager.ConnectAsync(servers, deadline.Token);
            foreach (var server in servers.Where(server => server.Enabled))
            {
                var tools = await gateway.Manager.Connections[server.Name].ListToolsAsync(deadline.Token);
                gateway.Registry.Add(server.Name, tools, server.Tools);
            }
            return gateway;
        }
        catch
        {
            await gateway.DisposeAsync();
            throw;
        }
    }

    public async Task<CallToolResult> CallAsync(string name, IReadOnlyDictionary<string, object?>? arguments = null)
        => (CallToolResult)await Tools.CallToolAsync(name, arguments ?? new Dictionary<string, object?>())
            .WaitAsync(TestLimits.Http);

    public async ValueTask DisposeAsync()
        => await Manager.DisposeAsync().AsTask().WaitAsync(TestLimits.Cleanup);

    public static async Task<McpClient> ConnectClientAsync(Uri endpoint, CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TestLimits.Http);
        var transport = new HttpClientTransport(new()
        {
            Endpoint = endpoint,
            TransportMode = HttpTransportMode.StreamableHttp
        });
        return await McpClient.CreateAsync(transport,
            new() { InitializationTimeout = TestLimits.Http }, cancellationToken: deadline.Token);
    }
}