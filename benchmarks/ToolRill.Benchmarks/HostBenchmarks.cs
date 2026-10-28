using BenchmarkDotNet.Attributes;

namespace ToolRill.Benchmarks;

[MemoryDiagnoser]
public class HostBenchmarks
{
    private readonly string _serverName = "server-a";
    private readonly string _toolName = "hello";

    [Benchmark]
    public string CreateServerName()
    {
        return new string(_serverName.AsSpan());
    }

    [Benchmark]
    public string CreateToolName()
    {
        return $"{_serverName}/{_toolName}";
    }
}