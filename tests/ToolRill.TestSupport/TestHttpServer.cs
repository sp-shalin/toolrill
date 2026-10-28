using System.Globalization;
using System.Net;
using System.Text.Json;
using ToolRill.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ToolRill.TestSupport;

public sealed class TestHttpServer : IAsyncDisposable
{
    private readonly WebApplication _application;
    public Uri Endpoint { get; }
    public TestGate Gate { get; }

    private TestHttpServer(WebApplication application, Uri endpoint, TestGate gate)
    {
        _application = application;
        Endpoint = endpoint;
        Gate = gate;
    }

    public static async Task<TestHttpServer> StartAsync(
        string identity, int expectedCalls = 1, string? blockedMethod = null,
        IReadOnlyDictionary<string, string>? requiredHeaders = null, string? rejectedMethod = null)
    {
        var gate = new TestGate(expectedCalls);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            EnvironmentName = "Testing"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));

        string Echo(string text, int number)
            => $"{identity}:{text}:{number.ToString(CultureInfo.InvariantCulture)}";

        async Task<string> Wait(string text, int number, CancellationToken cancellationToken)
        {
            await gate.EnterAsync(cancellationToken);
            return Echo(text, number);
        }

        builder.Services.AddMcpServer().WithHttpTransport().WithTools([
            McpServerTool.Create(Echo, new() { Name = "echo", Description = "Echo text and number." }),
            McpServerTool.Create(Wait, new() { Name = "wait", Description = "Wait for the test gate." }),
            McpServerTool.Create(() => new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = $"{identity}:controlled failure" }]
            }, new() { Name = "fail", Description = "Return a controlled tool error." })
        ]);

        var application = builder.Build();
        if (requiredHeaders is not null)
        {
            application.Use(async (context, next) =>
            {
                if (requiredHeaders.Any(header => context.Request.Headers[header.Key].ToString() != header.Value))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
                await next(context);
            });
        }
        if (blockedMethod is not null || rejectedMethod is not null)
        {
            application.Use(async (context, next) =>
            {
                if (HttpMethods.IsPost(context.Request.Method))
                {
                    context.Request.EnableBuffering();
                    using var message = await JsonDocument.ParseAsync(
                        context.Request.Body, cancellationToken: context.RequestAborted);
                    context.Request.Body.Position = 0;
                    if (message.RootElement.TryGetProperty("method", out var method))
                    {
                        var requestMethod = method.GetString();
                        var isDiscovery = requestMethod == "server/discover";
                        if (requestMethod == rejectedMethod || (isDiscovery && rejectedMethod == "initialize"))
                        {
                            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                            return;
                        }
                        if (requestMethod == blockedMethod || (isDiscovery && blockedMethod == "initialize"))
                        {
                            await gate.EnterAsync(context.RequestAborted);
                        }
                    }
                }
                await next(context);
            });
        }
        application.MapMcp("/mcp");
        try
        {
            using var deadline = new CancellationTokenSource(TestLimits.Http);
            await application.StartAsync(deadline.Token);
            var endpoint = new Uri(application.Urls.Single() + "/mcp");
            return new TestHttpServer(application, endpoint, gate);
        }
        catch
        {
            gate.Release();
            await application.DisposeAsync().AsTask().WaitAsync(TestLimits.Cleanup);
            throw;
        }
    }

    public ToolRill.Core.McpServerOptions Options(string name, McpToolFilterOptions? filter = null)
        => new() { Name = name, Transport = "streamable-http", Url = Endpoint.AbsoluteUri, Tools = filter };

    public async Task StopAsync()
    {
        Gate.Release();
        using var deadline = new CancellationTokenSource(TestLimits.Http);
        await _application.StopAsync(deadline.Token);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync();
        }
        finally
        {
            await _application.DisposeAsync().AsTask().WaitAsync(TestLimits.Http);
        }
    }
}

public sealed class TestGate(int expectedCalls)
{
    private readonly TaskCompletionSource _arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public async Task EnterAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _calls) == expectedCalls)
        {
            _arrived.TrySetResult();
        }
        try
        {
            await _released.Task.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _cancelled.TrySetResult();
            throw;
        }
    }

    public Task WaitForArrivalsAsync() => _arrived.Task.WaitAsync(TestLimits.Http);
    public Task WaitForCancellationAsync() => _cancelled.Task.WaitAsync(TestLimits.Http);
    public void Release() => _released.TrySetResult();
}