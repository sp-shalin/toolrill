# Benchmarks

The [benchmark project](../benchmarks/ToolRill.Benchmarks/ToolRill.Benchmarks.csproj)
uses BenchmarkDotNet and is separate from the host runtime and planned test suite.

## Current Coverage

`HostBenchmarks` uses `[MemoryDiagnoser]` for these starter string benchmarks:

| Method | Operation |
| --- | --- |
| `CreateServerName` | Allocate a new string from the runtime server-name value `server-a`, preserving its characters. |
| `CreateToolName` | Format runtime server and tool names as `server-a/hello`, using the same interpolation as `McpToolInfo.QualifiedName`. |

Core preserves configured server names without trimming or case normalization;
validation rejects blank names and case-insensitive duplicates. These benchmarks
isolate string creation and formatting, not configuration validation, tool
discovery, routing, connections, or host throughput.

To run the existing benchmarks from the repository root with the .NET 10 SDK:

```bash
dotnet run --configuration Release --project benchmarks/ToolRill.Benchmarks
```

The BenchmarkDotNet switcher accepts CLI arguments after `--`. To select all
benchmarks without an interactive selection prompt and use a short job:

```bash
dotnet run --configuration Release --project benchmarks/ToolRill.Benchmarks -- --filter '*' --job short
```

Artifacts default to `benchmarks/BenchmarkDotNet.Artifacts` relative to the
working directory, so run these commands from the repository root. Override
the output location with `--artifacts <path>` after `--` when needed.

Normal host configuration is not needed for these benchmarks. Record the
hardware, OS, runtime, workload, and settings when reporting results so they
can be reproduced.

## Planned Coverage

### Cross-Runtime Comparison

After the .NET implementation is stable, equivalent MCP Host implementations can be evaluated using:

- .NET
- Python
- TypeScript/Node.js

The purpose is to answer:

> **What are the practical performance and resource characteristics of a native .NET MCP Host compared with equivalent implementations?**

The comparison should use equivalent:

- hardware
- OS
- container limits
- workload
- MCP messages
- concurrency
- downstream servers
- test duration

Potential measurements:

| Metric | .NET | Python | TypeScript |
| --- | --- | --- | --- |
| Throughput | TBD | TBD | TBD |
| p50 latency | TBD | TBD | TBD |
| p95 latency | TBD | TBD | TBD |
| p99 latency | TBD | TBD | TBD |
| Memory | TBD | TBD | TBD |
| CPU | TBD | TBD | TBD |
| Startup | TBD | TBD | TBD |
| Allocations | TBD | TBD | TBD |
