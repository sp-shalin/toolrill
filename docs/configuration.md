# Host and Server Configuration

Configure ToolRill in one `toolrill.json`, containing a `hosts` list. Each named
host has `incoming` settings for clients connecting to ToolRill and its own
`outgoing` list of downstream MCP servers. `diagnostics` stays at the root
and applies to all hosts. No separate `instances.json` is needed.

`--host` selects a host by name and defaults to `default`, not the first entry.
Host names must be nonempty and unique, ignoring case. Add more hosts manually.
Each host needs an `incoming` object and an `outgoing` array.

| Incoming Setting | Purpose |
| --- | --- |
| `transport` | Required: `stdio` for client-launched hosting or `streamable-http` for HTTP hosting. |
| `url` | HTTP(S) listening base address for an HTTP host; clients connect at this address plus `/mcp`. |

Incoming transport is independent of outgoing transports: either kind of host
can connect to both local stdio and already-running HTTP downstream servers.

## Single Server

This default stdio host exposes a local .NET MCP server. Diagnostics are
disabled here to keep diagnostic stdout out of the stdio protocol:

```json
{
  "diagnostics": { "enabled": false },
  "hosts": [
    {
      "name": "default",
      "incoming": { "transport": "stdio" },
      "outgoing": [
        {
          "name": "local",
          "transport": "stdio",
          "command": "dotnet",
          "projectPath": "./samples/StdioServer/StdioServer.csproj"
        }
      ]
    }
  ]
}
```

ToolRill starts the server with `dotnet run --project <projectPath>` and exchanges
MCP messages over standard input/output. Relative project paths are resolved
from the host's working directory, not the configuration file's directory.

## Multiple Servers

Add entries to a host's `outgoing` array. Each downstream server name must
be unique within that host, ignoring case. Servers can use the same transport
or different transports. This is an outgoing-array fragment, not a full file:

```json
[
    {
      "name": "local",
      "transport": "stdio",
      "command": "dotnet",
      "projectPath": "./samples/StdioServer/StdioServer.csproj"
    },
    {
      "name": "remote",
      "transport": "streamable-http",
      "url": "http://localhost:5051/mcp"
    }
]
```

For a single HTTP server, keep only the `remote` entry. Unlike stdio, the HTTP
server must already be running; ToolRill connects to its absolute MCP endpoint URL.

### Server Settings

| Setting | Purpose |
| --- | --- |
| `name` | Required, nonempty downstream server name; unique within its host. |
| `enabled` | Defaults to `true`; `false` skips connection and tool discovery. |
| `transport` | `stdio` or `streamable-http`; defaults to `stdio`. |
| `command` | Required for stdio; executable name on `PATH` or executable path, not a shell command line. |
| `projectPath` | Optional stdio .NET project path; when supplied, launches `<command> run --project <projectPath>`. |
| `args` | Optional stdio string array, default `[]`; arguments for direct-command launches without `projectPath`. |
| `env` | Optional stdio string dictionary; child environment overrides with single-pass `${ENV_VAR}` expansion in values. |
| `url` | Required for HTTP; absolute HTTP(S) MCP endpoint URL; supports `${ENV_VAR}`. |
| `headers` | Optional outgoing HTTP request-header dictionary; values support `${ENV_VAR}`. |
| `connectionTimeoutSeconds` | Optional integer, default `30`; budget for each connection/initialization or tool-discovery operation. |
| `toolCallTimeoutSeconds` | Optional integer, default `60`; budget for each downstream tool call. |
| `tools` | Optional include/exclude filters, shown below. |

MCP is the protocol for both transports; choose the transport per server, not
a separate protocol. The old `arguments` setting remains unused; use `args`.

To disable a server, set `"enabled": false` on its entry. Disabled entries
still need unique names but do not need connection settings. All entries may
be disabled; an empty `outgoing` array is not valid for validation or startup.
It is allowed while editing configuration. `init` creates such an empty list;
add a server before starting that host.

### Arbitrary Stdio Commands

An enabled stdio entry requires a nonempty `command`. Choose either a
`projectPath` for the existing project launch or omit it to launch the command
directly with `args`. An executable that needs no arguments can omit `args`.
Nonempty `args` and `projectPath` cannot be combined, preventing silently ignored
arguments. Project launches keep exactly `run --project <projectPath>`; `env`
works with both forms.

For a precompiled .NET MCP server, no SDK is required on the gateway machine,
only the runtime required by that server:

```json
{
  "name": "compiled",
  "transport": "stdio",
  "command": "dotnet",
  "args": ["/opt/mcp/MyServer.dll", "--profile", "team workspace"],
  "env": {
    "API_TOKEN": "${MCP_API_TOKEN}",
    "SERVICE_REGION": "west"
  }
}
```

The executable must implement MCP over stdio and keep non-protocol output off
stdout. Each `args` element is passed as one argument, including spaces or an
empty string. ToolRill does not split strings, insert a shell, expand wildcards,
or expand `${ENV_VAR}` in commands, arguments or project paths. Executable and
relative argument paths use the host's working directory, not the config file's
directory. Validation does not check executable existence or MCP compatibility.

Other outgoing-array examples (replace image/package/executable names with your
actual stdio MCP servers):

```json
[
  { "name": "container", "transport": "stdio", "command": "docker", "args": ["run", "--rm", "-i", "my-mcp-server:latest"] },
  { "name": "node", "transport": "stdio", "command": "npx", "args": ["--yes", "my-mcp-server"] },
  { "name": "python", "transport": "stdio", "command": "uvx", "args": ["my-mcp-server"] },
  { "name": "native", "transport": "stdio", "command": "/opt/mcp/server", "env": { "API_TOKEN": "${MCP_API_TOKEN}" } }
]
```

Install the required executables/runtimes separately. On Windows, a batch shim
may require an explicit command such as `cmd.exe` with
`args: ["/c", "npx", "--yes", "my-mcp-server"]`; this explicitly opts into that
shell's parsing rules. Docker stdio needs `-i` and no `-t`. An `env` override
configures the Docker CLI process, not automatically the container; use Docker's
`--env NAME` arguments to forward selected inherited variables to it.

Children inherit the gateway environment, then `env` overrides are applied
without changing the parent environment. Values may be literal (including empty
strings) or contain multiple `${ENV_VAR}` placeholders. References read the
gateway process environment, not sibling `env` entries; substitution is
single-pass and does not recursively expand substituted text. Missing or empty
referenced variables fail validation/connection for enabled servers; disabled
entries skip resolution. The configuration retains its placeholders. Names
cannot be empty or contain `=` or NUL; values and arguments cannot contain NUL
or be null. Duplicate environment names follow OS case-sensitivity rules.
Substituted environment values receive the existing exact-value log redaction;
arbitrary literal `env` values, transformed secrets, child output and MCP
payloads are not guaranteed protected. Prefer placeholders over credentials in
files or command-line arguments. This is local process configuration, not a
secret vault, sandbox, or permission boundary; only launch trusted commands.

### Authenticated HTTP Servers

For example, add this entry to a host's `outgoing` array for GitHub's remote
MCP server. Supply `GITHUB_TOKEN` in the environment of the process launching
ToolRill, with the permissions required by that server:

```json
{
  "name": "github",
  "transport": "streamable-http",
  "url": "https://api.githubcopilot.com/mcp/",
  "headers": {
    "Authorization": "Bearer ${GITHUB_TOKEN}"
  }
}
```

An endpoint can also come from the environment: `"url": "${MCP_URL}"`.
For HTTP, expansion applies to outgoing URLs and header values, once per
validation or connection, after effective configuration is loaded; stdio `env`
values use the same expansion rules. Multiple
placeholders and surrounding literal text are supported; substituted text is
not recursively expanded or URL-encoded. Variable names follow the operating
system's environment lookup rules. Missing or empty variables on enabled
servers fail validation with the server and variable name, not the value.
Disabled servers skip resolution. Saved JSON retains placeholders, including
when added through the CLI. Use HTTPS for credentials outside loopback tests.

Headers are sent on the downstream SDK's HTTP requests, including initialization,
discovery and calls; they do not configure inbound client authentication. Header
names must be valid, unique ignoring case, and usable as request headers (not
content headers such as `Content-Type`). CR/LF/NUL values are rejected. Prefer
environment placeholders to literal credentials in files or command history.
ToolRill redacts resolved header values and their environment substitutions from
ToolRill diagnostics, including arbitrary custom header names. These values are
retained in memory for process-lifetime redaction; encoded/transformed variants,
third-party output and MCP payloads remain outside that guarantee. See
[Diagnostics](diagnostics.md#sensitive-values). No OAuth flow or secret vault is
introduced; server access and token validity are not checked by configuration validation.

### Timeouts and Cancellation

Both timeout settings apply to outgoing stdio and HTTP servers and accept
whole seconds from `1` through `4294967` (the .NET timer limit); zero, negative
values and larger values are invalid. For example, on an outgoing entry:

```json
{
  "name": "remote",
  "transport": "streamable-http",
  "url": "http://localhost:5051/mcp",
  "connectionTimeoutSeconds": 30,
  "toolCallTimeoutSeconds": 120
}
```

Connection initialization and each discovery call receive separate budgets,
not one deadline for the entire host startup. Increase the connection budget
if a project-based stdio server needs more time to build and initialize.
Environment overrides work as usual, for example
`TOOLRILL_hosts__0__outgoing__0__toolCallTimeoutSeconds=120`.

Each budget is linked to the caller's cancellation token. Startup cancellation
reaches connection initialization and discovery; inbound MCP request
cancellation reaches the downstream tool call through both inbound transports.
In core APIs, budget expiration throws `TimeoutException`, while caller
cancellation remains `OperationCanceledException`. Inbound SDK handling determines
the corresponding MCP response. A failed connection stops startup; a tool-call
timeout does not automatically disconnect or retry the server.

Cancellation is cooperative: the SDK cancels the request and signals the
downstream, but cannot guarantee that a server stops work or reverses side
effects. These settings are not shutdown/drain deadlines.

## Multiple Clients and Hosts

Different clients can use different named hosts from the same file. For example:

```json
{
  "diagnostics": { "enabled": false },
  "hosts": [
    {
      "name": "default",
      "incoming": { "transport": "stdio" },
      "outgoing": [
        {
          "name": "local",
          "transport": "stdio",
          "command": "dotnet",
          "projectPath": "./samples/StdioServer/StdioServer.csproj"
        }
      ]
    },
    {
      "name": "work",
      "incoming": {
        "transport": "streamable-http",
        "url": "http://127.0.0.1:3101"
      },
      "outgoing": [
        {
          "name": "inventory",
          "transport": "streamable-http",
          "url": "http://localhost:5051/mcp"
        }
      ]
    },
    {
      "name": "research",
      "incoming": {
        "transport": "streamable-http",
        "url": "http://127.0.0.1:3102"
      },
      "outgoing": [
        {
          "name": "catalog",
          "transport": "streamable-http",
          "url": "http://localhost:5052/mcp"
        }
      ]
    }
  ]
}
```

Run `toolrill manager` to start `work` and `research` as separate HTTP processes.
One VS Code client can connect to `http://127.0.0.1:3101/mcp` for inventory
tools, while another VS Code or Claude client connects to
`http://127.0.0.1:3102/mcp` for catalog tools. Downstream services must be running
at their configured outgoing URLs. Manager checks distinct configured listening
addresses and validates the HTTP hosts before starting any children.

A stdio client instead launches `toolrill start --host default --config <absolute-path>`.
Other stdio clients can launch other named stdio hosts the same way. Manager
skips stdio hosts. `start --host work` starts only `work`, never all hosts.

Each client still needs its normal MCP connection entry specifying the command
and arguments or HTTP URL. Those entries point to ToolRill; they do not contain
its outgoing setup. Each host has separate connections and tool registries;
host names are configuration selectors, not client identities or security
boundaries. Clients wanting the same configuration can use the same HTTP
endpoint. No load balancing or automatic per-client routing is introduced.

## Tools: Aggregation, Routing, and Filtering

At startup, ToolRill discovers tools from connected servers and combines them
into one list for the selected host. Each tool uses a qualified name, such as `local/hello` or
`remote/hello`, so servers can expose tools with the same raw name. Requests
are routed to the server named before `/`; no routing configuration is needed.
Host names are not added to tool names. A downstream name such as `inventory`
is freely chosen and produces `inventory/<tool>`; it is not a command keyword.

Add `tools` to a server entry to control which of its tools are exposed:

```json
{
  "name": "remote",
  "transport": "streamable-http",
  "url": "http://localhost:5051/mcp",
  "tools": {
    "include": ["search_*", "get_repository"],
    "exclude": ["delete_*"]
  }
}
```

- Omit `tools`, or leave `include` empty, to allow all tools.
- A nonempty `include` requires a match; `exclude` always takes precedence.
- Patterns match raw names, not `server/tool` names, ignoring case. `*` matches
  zero or more characters; exact names are also supported.
- Filtered tools cannot be listed or called through the host. Filtering every
  tool does not disconnect the server.

### Live CLI Discovery

Use `toolrill server add <name> ... --discover` to initialize `tools.include` from
the new downstream's live `tools/list`. Use
`toolrill server inspect <name> [--host NAME] [--config FILE]` to print live raw
names and descriptions, or add `--sync` to replace the existing include list.
These are finite connections through the same HTTP/stdio clients and separate
connection/discovery timeout budgets, without calling any tools or starting
the gateway. Stdio launches the configured trusted executable.

Add discovers using the proposed base entry; inspect uses effective settings
(including overrides, HTTP headers and stdio args/env), rejects disabled servers,
and lists tools regardless of filters. Both save only after successful discovery
and disconnect. Errors are reported to stderr with exit code 1 and no saved
changes. Sync requires a matching base-file host/server and writes only its
`tools.include`; exclusions, other hosts/settings and stored placeholders remain.
Override sources are not modified and can still supersede the saved list.

Discovered names are raw, not `server/tool` qualified. An empty discovered list
saves `include: []`, which allows all tools subject to exclusions under the
existing filter rules. These remain pattern filters, not a new literal-name
security boundary. Restart a running host to apply changes; discovery does not
refresh its in-memory registry. See [CLI](cli.md#inspect-a-server) for usage and limits.

## Start and Manage the Host

Validate the file, then choose how MCP clients connect to ToolRill:

```bash
toolrill config validate --host default --config ./toolrill.json
toolrill start --host default --config ./toolrill.json
```

`start` reads the selected host's incoming transport and URL. After `init`,
the `default` host uses stdio, intended for an MCP client to launch. For a
temporary HTTP override instead:

```bash
toolrill start --host default --mode serve-http --config ./toolrill.json --url http://127.0.0.1:3001
```

The host endpoint is `http://127.0.0.1:3001/mcp`. This inbound mode is independent
of downstream transports: either host mode can connect to stdio and HTTP servers.
`--mode` and `--url` override incoming settings only for the current run;
the file is not changed. `--url` is rejected for effective stdio hosting.
Direct `stdio` and `serve-http` commands explicitly choose the transport.

The host connects and discovers tools during startup, then reports tools and
server states. Press Ctrl+C to stop and disconnect downstream connections.
Configuration is read at startup; restart the host after changing servers or
filters. CLI inspection does not reconnect or refresh an already-running host.

Use `server add`, `server list`, `server inspect --sync`, and `server remove` to manage configuration;
see the [CLI guide](cli.md) for their syntax. Multiple entries here share one
host when placed in its outgoing list. `--host` on these commands selects which
list to manage. `config validate` checks every host unless `--host` selects one;
single-host startup and add/remove do not require other draft hosts to have
configured outgoing servers. The CLI's `manager` mode uses HTTP hosts from
this same file, not external configuration paths.

## Configuration Files and Overrides

`--config` defaults to `toolrill.json` in the current working directory for source
runs and globally installed tools. The selected file must exist. ToolRill does
not automatically read, rename or migrate old `appsettings.json` or
`instances.json` files. An explicit custom filename must also use the unified
schema; legacy root `servers` and `instances` schemas are not supported.
Effective configuration loads in this order:

1. The selected base file.
2. Its optional environment-specific sibling, such as `toolrill.Production.json`.
3. Environment variables prefixed with `TOOLRILL_`, using `__` for nested settings
  (for example, `TOOLRILL_hosts__0__outgoing__0__enabled=false`).

`DOTNET_ENVIRONMENT` selects the environment and defaults to `Production`.
Later values override matching earlier values. CLI add/remove and inspect --sync operations edit
only the base file, so an override can still change the effective server list.
Array overrides retain the configuration provider's numeric-index semantics;
they are not matched by host or server name. Keep indices aligned between files.
The optional override file is not another required host configuration file.

## Failure Handling

- Invalid configuration prevents startup: check server names and required
  transport settings with `toolrill config validate`.
- Validation does not check whether a project exists or an HTTP endpoint is reachable.
- A downstream connection failure stops startup and triggers connection cleanup;
  the current host does not continue with only the remaining servers.
- Tool-call failures propagate to the caller. Automatic retry/reconnect is not configured.

## Other Guides

- [CLI commands and installation](cli.md)
- [Containerization](containerization.md)
- [Diagnostics](diagnostics.md)
- [Benchmarks](benchmarks.md)