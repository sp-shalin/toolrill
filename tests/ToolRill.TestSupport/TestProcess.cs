using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using ToolRill.Core;
using ModelContextProtocol.Client;

namespace ToolRill.TestSupport;

public sealed class TestProcess : IAsyncDisposable
{
    private readonly TestConfiguration _configuration;
    private readonly ConcurrentQueue<string> _diagnostics = new();
    private readonly TaskCompletionSource<Uri> _listening = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Process? _process;
    private Task? _stdout;
    private Task? _stderr;
    private McpClient? _client;

    public McpClient Client => _client ?? throw new InvalidOperationException("Gateway client is not initialized.");
    public Uri? Endpoint { get; private set; }
    public string Diagnostics => string.Join(Environment.NewLine, _diagnostics);

    private TestProcess(TestConfiguration configuration) => _configuration = configuration;

    public static string RepositoryRoot
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "toolrill.slnx")))
                {
                    return directory.FullName;
                }
            }
            throw new DirectoryNotFoundException("Could not locate the ToolRill repository from the test output.");
        }
    }

    private static string CliAssembly
    {
        get
        {
            var output = new DirectoryInfo(AppContext.BaseDirectory);
            var assembly = Path.Combine(RepositoryRoot, "src", "ToolRill", "bin",
                output.Parent!.Name, output.Name, "ToolRill.dll");
            return File.Exists(assembly) ? assembly
                : throw new FileNotFoundException("Build the matching CLI configuration before running tests.", assembly);
        }
    }

    public static McpServerOptions SampleOptions(string name = "local")
        => new()
        {
            Name = name,
            Transport = "stdio",
            Command = "dotnet",
            ProjectPath = Path.Combine(RepositoryRoot, "samples", "StdioServer", "StdioServer.csproj")
        };

    public static async Task<TestProcess> StartAsync(string transport, params McpServerOptions[] servers)
    {
        var gateway = new TestProcess(TestConfiguration.Create(transport, servers));
        try
        {
            using var deadline = new CancellationTokenSource(TestLimits.Stdio);
            string[] arguments = ["start", "--config", gateway._configuration.Path, "--host", "selected"];
            if (transport == "stdio")
            {
                var stdio = new StdioClientTransport(new()
                {
                    Name = "toolrill-test-gateway",
                    Command = "dotnet",
                    Arguments = [CliAssembly, .. arguments],
                    WorkingDirectory = RepositoryRoot,
                    EnvironmentVariables = EnvironmentOverrides(),
                    StandardErrorLines = line => gateway._diagnostics.Enqueue(line),
                    ShutdownTimeout = TimeSpan.FromSeconds(5)
                });
                gateway._client = await McpClient.CreateAsync(stdio,
                    new() { InitializationTimeout = TestLimits.Stdio }, cancellationToken: deadline.Token);
            }
            else if (transport == "streamable-http")
            {
                gateway._process = Process.Start(StartInfo(arguments))
                    ?? throw new InvalidOperationException("Could not start the gateway process.");
                gateway._stdout = gateway.ReadLinesAsync(gateway._process.StandardOutput, discoverEndpoint: false);
                gateway._stderr = gateway.ReadLinesAsync(gateway._process.StandardError, discoverEndpoint: true);
                var exited = gateway._process.WaitForExitAsync(deadline.Token);
                if (await Task.WhenAny(gateway._listening.Task, exited) == exited)
                {
                    await exited;
                    throw new InvalidOperationException($"Gateway exited before listening. {gateway.Diagnostics}");
                }
                gateway.Endpoint = await gateway._listening.Task.WaitAsync(deadline.Token);
                gateway._client = await TestGateway.ConnectClientAsync(gateway.Endpoint, deadline.Token);
            }
            else
            {
                throw new ArgumentException("Unsupported fixture transport.", nameof(transport));
            }
            return gateway;
        }
        catch (Exception exception)
        {
            await gateway.DisposeAsync();
            throw new InvalidOperationException($"Gateway fixture startup failed. {gateway.Diagnostics}", exception);
        }
    }

    public async Task<McpClient> ConnectAnotherClientAsync()
    {
        using var deadline = new CancellationTokenSource(TestLimits.Http);
        return await TestGateway.ConnectClientAsync(Endpoint
            ?? throw new InvalidOperationException("Multiple clients require HTTP."), deadline.Token);
    }

    private async Task ReadLinesAsync(StreamReader reader, bool discoverEndpoint)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            _diagnostics.Enqueue(line);
            if (!discoverEndpoint || !line.StartsWith('{'))
            {
                continue;
            }
            string? message;
            try
            {
                using var entry = JsonDocument.Parse(line);
                message = entry.RootElement.TryGetProperty("message", out var value)
                    && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            }
            catch (JsonException)
            {
                continue;
            }
            if (message is null)
            {
                continue;
            }
            const string marker = "Now listening on: ";
            var position = message.IndexOf(marker, StringComparison.Ordinal);
            if (position >= 0
                && Uri.TryCreate(message[(position + marker.Length)..].Trim(), UriKind.Absolute, out var address)
                && address.IsLoopback && address.Port != 0)
            {
                _listening.TrySetResult(new Uri(address, "/mcp"));
            }
        }
    }

    private static Dictionary<string, string?> EnvironmentOverrides()
    {
        var environment = new Dictionary<string, string?>();
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var name = (string)entry.Key;
            if (name.StartsWith("TOOLRILL_", StringComparison.OrdinalIgnoreCase))
            {
                environment[name] = null;
            }
        }
        environment["DOTNET_ENVIRONMENT"] = "Testing";
        environment["Logging__LogLevel__Microsoft.Hosting.Lifetime"] = "Information";
        environment["Logging__Console__FormatterName"] = "simple";
        environment["Logging__Console__FormatterOptions__ColorBehavior"] = "Disabled";
        environment["Logging__Console__FormatterOptions__JsonWriterOptions__Indented"] = "false";
        return environment;
    }

    private static ProcessStartInfo StartInfo(IEnumerable<string> arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = RepositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(CliAssembly);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        foreach (var entry in EnvironmentOverrides())
        {
            if (entry.Value is null)
            {
                start.Environment.Remove(entry.Key);
            }
            else
            {
                start.Environment[entry.Key] = entry.Value;
            }
        }
        return start;
    }

    public static async Task<CommandResult> RunCommandAsync(params string[] arguments)
    {
        using var process = Process.Start(StartInfo(arguments))
            ?? throw new InvalidOperationException("Could not start the CLI process.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            using var deadline = new CancellationTokenSource(TestLimits.Stdio);
            await process.WaitForExitAsync(deadline.Token);
            return new CommandResult(process.ExitCode, await stdout, await stderr);
        }
        finally
        {
            await StopProcessAsync(process);
        }
    }

    private static async Task StopProcessAsync(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }
        await process.WaitForExitAsync().WaitAsync(TestLimits.Cleanup);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_client is not null)
            {
                await _client.DisposeAsync().AsTask().WaitAsync(TestLimits.Cleanup);
                _client = null;
            }
        }
        finally
        {
            try
            {
                if (_process is not null)
                {
                    await StopProcessAsync(_process);
                    await Task.WhenAll(_stdout ?? Task.CompletedTask, _stderr ?? Task.CompletedTask)
                        .WaitAsync(TestLimits.Cleanup);
                    _process.Dispose();
                    _process = null;
                }
            }
            finally
            {
                _configuration.Dispose();
            }
        }
    }
}

public sealed record CommandResult(int ExitCode, string Output, string Error);

public sealed class TestConfiguration : IDisposable
{
    private readonly DirectoryInfo _directory;
    public string Path { get; }

    private TestConfiguration(DirectoryInfo directory)
    {
        _directory = directory;
        Path = System.IO.Path.Combine(directory.FullName, "toolrill.json");
    }

    public static TestConfiguration Create(string transport, IReadOnlyList<McpServerOptions> servers,
        bool includeInvalidHost = false)
    {
        var configuration = new TestConfiguration(Directory.CreateTempSubdirectory("toolrill-tests-"));
        try
        {
            var hosts = new List<object>
            {
                new
                {
                    name = "selected",
                    incoming = new { transport, url = transport == "streamable-http" ? "http://127.0.0.1:0" : null },
                    outgoing = servers
                }
            };
            if (includeInvalidHost)
            {
                hosts.Add(new { name = "invalid", incoming = new { transport = "unsupported" }, outgoing = servers });
            }
            File.WriteAllText(configuration.Path, JsonSerializer.Serialize(new
            {
                diagnostics = new { enabled = transport == "streamable-http" },
                hosts
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            return configuration;
        }
        catch
        {
            configuration.Dispose();
            throw;
        }
    }

    public void Dispose() => _directory.Delete(recursive: true);
}