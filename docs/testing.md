# Testing

ToolRill includes four .NET 10 xUnit test suites covering unit, integration, protocol, and concurrency behavior using the official C# MCP SDK 2.2.0 and shared test-only fixtures.

## ToolRill Transition Verification

On 2026-10-28, the renamed solution restored and built successfully in Release on Windows. The four test suites, shared fixtures, samples, and benchmarks compile with the ToolRill project names. Tests were **Not run** during the transition; packaging, benchmark execution, Docker, and external CI remain pending manual verification. Earlier-name results do not constitute acceptance of the renamed release.

## Projects and Coverage

| Project | Authored Basic Coverage |
| --- | --- |
| [ToolRill.UnitTests](../tests/ToolRill.UnitTests/ToolRill.UnitTests.csproj) | Valid/invalid configuration including timeout defaults/ranges; lifecycle guards, startup/discovery pre-cancellation, repeated shutdown and state snapshots; aggregation, filters, lookup, routing and tool errors; connection/discovery timeout expiration, tool-call expiration, downstream cancellation and post-timeout reuse; end-to-end inbound HTTP/stdio cancellation; diagnostic redaction, structured stderr and stdout isolation. |
| [ToolRill.IntegrationTests](../tests/ToolRill.IntegrationTests/ToolRill.IntegrationTests.csproj) | Mixed stdio/HTTP discovery, filtering and calls; disabled servers; explicit disconnect/reconnect; first-failure startup behavior; stopped downstream failure and healthy-server calls; CLI named-host validation; connection disposal. |
| [ToolRill.ProtocolTests](../tests/ToolRill.ProtocolTests/ToolRill.ProtocolTests.csproj) | Official SDK initialization, tools/list and tools/call against actual HTTP and stdio gateways; qualified names, descriptions, input schemas, string/integer arguments, escaped text, nested results/errors, unknown tools, invalid numeric arguments and upstream SDK cancellation. |
| [ToolRill.ConcurrencyTests](../tests/ToolRill.ConcurrencyTests/ToolRill.ConcurrencyTests.csproj) | Two HTTP clients, eight overlapping calls across two downstreams, concurrent discovery, three client connection cycles, three explicit downstream reconnect cycles and concurrent successful/failed tool calls. |
| [ToolRill.TestSupport](../tests/ToolRill.TestSupport/ToolRill.TestSupport.csproj) | Small real HTTP downstream, core gateway assembly, temporary configuration, CLI process ownership, typed result decoding and async gates. This is not a test suite. |

Configuration and disconnected lifecycle cases are isolated unit tests.
Registry, aggregation and successful routing cases in the unit suite are
component tests: they obtain genuine SDK tool metadata from a local HTTP
fixture instead of mocking sealed production classes.

[McpTimeoutTests](../tests/ToolRill.UnitTests/McpTimeoutTests.cs) adds gated
initialization/discovery and tool-call component cases. Expiration assertions
expect the gateway's `TimeoutException`; separate harness cancellation guards
cannot satisfy them. Caller-cancellation cases expect `OperationCanceledException`
and await a downstream gate cancellation signal. The two inbound cancellation
cases reuse the process fixture for real HTTP/stdio gateways and are not
isolated unit tests.

[McpDiagnosticsTests](../tests/ToolRill.UnitTests/McpDiagnosticsTests.cs) adds isolated
cases for credential headers (including cookies), quoted assignments, Bearer/Basic
credentials, URL user info/query values/fragments and exception text. Console
capture checks that enabled diagnostics and CLI error output leave stdout empty,
emit structured single-line diagnostics on stderr, preserve activity correlation,
and respect enabled/severity settings. Framework provider coverage includes
formatted state and nested exceptions. Console-changing cases use a nonparallel
xUnit collection and restore both streams in `finally`; no host is launched.
These checks are authored and compiled. See
[Diagnostics](diagnostics.md#sensitive-values) for redaction rules and limits.

## Outgoing HTTP Header and Environment Expansion Coverage

Unit cases cover URL/header expansion, repeated placeholders, unchanged source
settings, missing/empty variables, disabled-server bypass, invalid and duplicate
headers, and exact-value redaction for arbitrary custom headers. Integration
cases reuse `TestHttpServer` with required-header middleware returning 401 for
missing/incorrect values, then discover and call tools using literal or
environment-expanded Authorization and custom headers. CLI cases cover repeatable
headers, values containing `=`, placeholder persistence, effective configuration
binding, missing variables and rejection without saving. Environment variables
use unique names and are cleared in `finally`; no external credentials are needed.

The equivalent customer configuration is an outgoing entry such as:

```json
{
  "name": "github",
  "transport": "streamable-http",
  "url": "${GITHUB_MCP_URL}",
  "headers": { "Authorization": "Bearer ${GITHUB_TOKEN}" }
}
```

For real use, set `GITHUB_MCP_URL` to `https://api.githubcopilot.com/mcp/` and
provide a suitably permitted `GITHUB_TOKEN` securely. The tests use local loopback fixtures and synthetic values rather than external endpoints or real tokens. See [Configuration](configuration.md#authenticated-http-servers).

## Requirements

- A source checkout and the .NET 10 SDK, with `dotnet` on `PATH`.
- NuGet access for initial restore, or the required packages already cached.
- Permission to bind loopback ports, launch child processes and write temporary
  files. No external MCP server, Docker, Python or Node.js is required.

## Stdio Direct-Command and CLI Discovery Coverage

Unit cases cover project argument preservation, valid direct commands with/without
arguments, unchanged argument boundaries (including empty strings), rejection
of project/args conflicts, invalid env names/null/NUL values and invalid
arguments, single-pass environment expansion, unchanged parent/source settings,
substitution redaction, missing/empty-variable failures and disabled bypass.

Integration cases launch the existing StdioServer through a real stdio gateway
using either its project or matching prebuilt DLL. JSON configuration binds
`args` and `env`; SDK discovery/calls assert the environment-configured greeting
and an argument override containing spaces. StdioServer's default greeting and
goodbye behavior are preserved; only its hello greeting is now configurable.
CLI cases cover direct commands with/without JSON-array `--args`, persistence,
effective configuration validation, malformed/non-string args, HTTP rejection
and project/args conflicts without saving.

The stdio integration cases reuse [StdioServer](../samples/StdioServer/Program.cs).
The project-based transport invokes `dotnet run --project`, so that
sample can be built by the SDK during a later test execution even when the
test runner uses `--no-build`. Direct-DLL cases instead locate the matching
sample `bin/<configuration>/net10.0/StdioServer.dll` built by the solution;
they do not invoke `dotnet run`. See
[stdio configuration](configuration.md#arbitrary-stdio-commands) for production
`args` and `env` examples and runtime requirements.

## CLI-002 Coverage

[CliDiscoveryTests](../tests/ToolRill.UnitTests/CliDiscoveryTests.cs) covers include-only
updates preserving exclusions/settings, raw-name spelling, empty discovered
lists, valueless-flag rejection of duplicates/values/wrong commands, missing or
disabled servers, duplicate additions before connecting, and pre-cancellation
without saving. These use temporary `TestConfiguration` fixtures where needed;
the unit assembly has friend access to the internal CLI command helpers.

The same class includes an authored identity regression case checking the
`ToolRill` host assembly, `toolrill.json` default, and `TOOLRILL_` environment
configuration overrides. It was compiled but not executed during the rename.

[CliDiscoveryIntegrationTests](../tests/ToolRill.IntegrationTests/CliDiscoveryIntegrationTests.cs)
authors real finite CLI calls using `TestProcess`, `TestHttpServer` and the
matching StdioServer project/prebuilt DLL. Cases cover add discovery and inspect
over both transports, required HTTP headers, raw names and descriptions, sync
versus read-only inspection, case-insensitive named-host/server selection,
effective sibling connection overrides with base-only writes, preserved
exclusions/other hosts, initialization/discovery rejection, stdio launch failure,
discovery timeout without saving, and unchanged offline add behavior. The HTTP
fixture can reject a selected JSON-RPC method with 503 to distinguish discovery
failure from initialization failure. Tests compare original file contents on
failure. They do not independently prove OS child exit or a cleanup deadline.

See [CLI usage](cli.md#inspect-a-server) and [configuration semantics](configuration.md#live-cli-discovery).

## Build Without Running Tests

From the repository root:

```bash
dotnet restore toolrill.slnx
dotnet build toolrill.slnx --no-restore
```

These commands compile the suites and their CLI/sample dependencies. They do
not discover or execute tests and do not start any application.

## Running the Tests

These projects use VSTest via `Microsoft.NET.Test.Sdk`, not Microsoft.Testing.Platform.

```bash
dotnet test toolrill.slnx --no-build --logger trx
dotnet test tests/ToolRill.UnitTests/ToolRill.UnitTests.csproj --no-build
dotnet test tests/ToolRill.IntegrationTests/ToolRill.IntegrationTests.csproj --no-build
dotnet test tests/ToolRill.ProtocolTests/ToolRill.ProtocolTests.csproj --no-build
dotnet test tests/ToolRill.ConcurrencyTests/ToolRill.ConcurrencyTests.csproj --no-build
dotnet test tests/ToolRill.UnitTests/ToolRill.UnitTests.csproj --no-build --filter "FullyQualifiedName~McpConfigurationValidatorTests"
```

Build before using `--no-build`. For Release, pass `--configuration Release`
to both build and test commands; fixtures locate the matching CLI output.
Keep the checkout available because fixtures find the solution and sample
paths relative to the test output. TRX files can record later results; record
the SDK, OS, configuration and command alongside them.

## Fixture Isolation and Workload

HTTP downstreams and CLI gateways bind `127.0.0.1` on OS-assigned ports, not
the normal customer ports. CLI HTTP readiness uses its listening announcement
followed by SDK initialization, never `/health`. CLI fixtures select a temporary
`selected` host, disable ToolRill diagnostics, remove child-process `TOOLRILL_`
overrides and use the Testing environment without changing the parent process.
The SDK alone reads stdio protocol output; stderr is captured separately.

The main concurrency case uses two upstream SDK clients and two downstream
HTTP servers. Each client submits four uniquely identified calls, alternating
servers, for eight total. Both downstream gates must observe four arrivals
before release, proving overlap without latency assertions. Separate cases
hold a primary call while performing three client or secondary-downstream
connection cycles. Lifecycle changes on the same secondary connection are
serialized; registry writes are not raced with requests.

HTTP operations and gate waits use 30-second harness bounds; gateway/stdio
startup uses a 60-second allowance. Owned client/process cleanup is bounded
separately. Gates are released during cleanup, and owned HTTP gateway process
trees are terminated when their fixture ends. The SDK owns stdio process
disposal. Independent OS-level downstream child-exit assertions and graceful
signal handling are not covered; actual cleanup still requires manual review.

## Limits

TEST-001 is **Implemented** for its authored scope, including RUN-002 gateway
timeouts and end-to-end cancellation. No passing behavior is established until
execution and manual review. Gated HTTP downstream cases assert cooperative
request cancellation; they do not prove that arbitrary third-party servers
stop work, undo side effects, or that stdio child processes are forcibly stopped.
Harness bounds remain safeguards, separate from configured gateway budgets.

The existing adapters return downstream `CallToolResult` as serialized JSON
inside upstream text content. Tests decode that representation explicitly,
including its nested `IsError`; they do not assume transparent raw-result or
outer-error forwarding. Invalid arguments may be rejected by the upstream SDK
or reported in the nested downstream tool result. Transport-failure cases
accept the SDK/HTTP/IO/connection error families, but not harness timeouts.

Startup stops at the first failed connection; reconnect is explicit, not an
automatic retry. Connection-state assertions use the connection itself, not
the manager's startup-only status snapshot. No live registry refresh or
concurrent mutation guarantee is claimed.

This is bounded functional coverage, not a stress test, benchmark, full MCP
conformance suite, compatibility-version matrix or security/deployment gate.
Resources, prompts, sampling, inbound authentication/OAuth, external token
verification, Docker shutdown and broader
runtime hardening remain outside the selected scope.

## Related Guides

- [Architecture](architecture.md)
- [Configuration](configuration.md)
- [Diagnostics](diagnostics.md)