# CLI Tools

ToolRill provides a minimal configure-and-run CLI. Management commands are offline
except `server add --discover` and `server inspect`, which make finite downstream
connections. `start` runs the host in the foreground.

| Command | Description |
| --- | --- |
| `toolrill init` | Create a configuration file. |
| `toolrill server add` | Add a downstream server. |
| `toolrill server list` | List configured servers. |
| `toolrill server inspect` | List live downstream tools; optionally sync include filters. |
| `toolrill server remove` | Remove a configured server. |
| `toolrill config validate` | Validate configuration. |
| `toolrill start` | Start the host in the foreground. |
| `toolrill help` | Show commands and usage. |
| `toolrill stdio` | Start the selected host over stdio. |
| `toolrill serve-http` | Start the host over HTTP. |
| `toolrill manager` | Start all configured HTTP hosts. |

## Installation

Use the .NET 10 SDK to build and package the CLI. The installed tool requires a
compatible .NET 10 runtime. From the repository root, build a local package and
install it with the standard .NET tool commands:

```bash
dotnet pack src/ToolRill/ToolRill.csproj --output ./artifacts
dotnet tool install --global ToolRill --add-source ./artifacts
```

The package ID is `ToolRill`; the installed command is `toolrill`. These commands
use a locally built package and do not assume a published NuGet release.
The .NET global tools directory must be on your `PATH`.

Alternatively, run from source without installing:

```bash
dotnet run --project src/ToolRill -- help
dotnet run --project src/ToolRill -- server list --config ./toolrill.json
```

Replace everything after `--` with the desired CLI command and options.

## Quick Start

Run in a directory where you want to keep the host configuration:

```bash
toolrill init
toolrill server add inventory --transport streamable-http --url http://localhost:5051/mcp
toolrill server list
toolrill config validate
toolrill start --mode serve-http --url http://127.0.0.1:3001
```

The downstream server must already be available at the supplied URL when you
start the host. The host exposes MCP at `http://127.0.0.1:3001/mcp`.
Press Ctrl+C to stop foreground hosting.

`inventory` is your chosen downstream server name, not a keyword; its tools
use names such as `inventory/search`. The commands above select the host named
`default`. A host name selects a configuration; a server name identifies a
downstream tool provider inside that configuration.

For an existing .NET MCP server project, use stdio instead:

```bash
toolrill server add local --transport stdio --project ./path/to/Server.csproj
toolrill config validate
toolrill start
```

Stdio startup is intended for an MCP client to launch and communicate with;
it is not an interactive command prompt.

## Commands

### Help

```bash
toolrill help
toolrill help server
toolrill help server add
toolrill help start
toolrill --help
toolrill server add --help
```

Recognized commands accept `--help` or `-h`. Help does not require configuration
or launch the host. Bare `toolrill server` and `toolrill config` show group usage.
Invoking `toolrill` without a command starts the `default` host using its
configured incoming transport, rather than showing help.

### Initialize Configuration

```bash
toolrill init [--config FILE]
```

Creates `toolrill.json` with one host named `default`, `incoming.transport` set
to `stdio`, and an empty `outgoing` array. `--config` chooses another filename.
Existing target files are never overwritten, and the parent directory must
already exist. An unrelated `appsettings.json` is left untouched. Add at least
one outgoing server before validating or starting this host.

Add other entries to `hosts` manually; there is no host-creation command.
See the [configuration guide](configuration.md) for the complete schema.

### Add a Server

```bash
toolrill server add <name> --transport stdio --project PATH [--command COMMAND] [--discover] [--host NAME] [--config FILE]
toolrill server add <name> --transport stdio --command COMMAND [--args JSON_ARRAY] [--discover] [--host NAME] [--config FILE]
toolrill server add <name> --transport streamable-http --url URL [--header KEY=VALUE]... [--discover] [--host NAME] [--config FILE]
```

`<name>` is a downstream server identifier you choose, such as `inventory`.
Names must be nonempty and unique within the selected host, ignoring case.
The transport is required; new servers are enabled by default. `--host`
selects an existing host and defaults to `default`; a missing host is an error.
For example, `server add inventory --host work ...` edits only `work.outgoing`.

With `--project`, stdio keeps the existing project launch: `--command` defaults
to `dotnet`, and the process runs as `<command> run --project <path>`. Without
`--project`, supply `--command` naming an executable and optionally `--args`
as one JSON array of strings. No arguments are required for a standalone
executable that needs none. Nonempty `args` and `projectPath` cannot be combined.
Paths are stored as supplied; relative paths follow the host's working directory,
not the configuration file's directory. `--url` is not accepted for stdio.

For Bash or PowerShell, keep the JSON array in one shell argument:

```bash
toolrill server add compiled --transport stdio --command dotnet --args '["/opt/mcp/MyServer.dll","--profile","team workspace"]'
toolrill server add container --transport stdio --command docker --args '["run","--rm","-i","my-mcp-server:latest"]'
```

Adapt quoting to your shell (including escaping JSON double quotes in `cmd.exe`).
Arguments are persisted and forwarded individually, never split as a command
line. Malformed JSON, non-string array members and HTTP `--args` are rejected
without saving. Configuration validation does not launch or locate executables.

Set child environment overrides by editing the outgoing entry's `env` object in
`toolrill.json`, for example `"env": { "API_TOKEN": "${MCP_API_TOKEN}" }`; there is
no CLI `--env` option. Values expand once from the gateway environment during
validation/connection, with missing/empty-variable errors for enabled entries.
The parent environment and saved placeholders are unchanged. `env` works for
project launches too. `args`, `command` and `projectPath` are not expanded.
See [arbitrary stdio configuration](configuration.md#arbitrary-stdio-commands)
for inheritance, supported executables, Windows shims and security limits.

Streamable HTTP requires an absolute URL. `--project`, `--command` and `--args` are not
accepted for this transport.

Repeat `--header KEY=VALUE` for outgoing HTTP headers. Names must be unique
ignoring case; values may contain `=`. Stdio rejects `--header`. URL and header
values support `${ENV_VAR}`; referenced variables must be set and nonempty in
the CLI environment even during add/validation. Placeholders are saved unchanged.
For GitHub's remote MCP server, with `GITHUB_TOKEN` already supplied securely:

```bash
toolrill server add github --transport streamable-http --url https://api.githubcopilot.com/mcp/ --header 'Authorization=Bearer ${GITHUB_TOKEN}'
```

Single quotes preserve the placeholder in Bash and PowerShell; in `cmd.exe`,
use double quotes instead. For an environment-supplied endpoint, use
`--url '${MCP_URL}'` in Bash/PowerShell. Avoid literal tokens in shell history.
See [authenticated HTTP configuration](configuration.md#authenticated-http-servers)
for scope and log-redaction limits. This is outgoing authentication only, not
an interactive OAuth flow. Add without `--discover` is not a reachability check.

The configuration file must exist; use `init` first for a new file. Add
validates the selected host's resulting base-file outgoing list before saving, including
rejecting duplicate names.

Pass the valueless `--discover` flag to connect to the new server, request
`tools/list`, disconnect, and save its raw tool names in `tools.include`:

```bash
toolrill server add inventory --transport streamable-http --url http://localhost:5051/mcp --discover
toolrill server add local --transport stdio --project ./path/to/Server.csproj --discover
```

Discovery uses the new base-file entry, including supplied HTTP headers and
environment expansion, not sibling-file or `TOOLRILL_` overrides. It uses the
default 30-second budget separately for connection and discovery. No tool is
called. A connection, discovery, or cleanup failure reports an error to stderr,
exits with code 1, and leaves the file unchanged, including not adding the server.
Without `--discover`, add retains its offline behavior.

### Inspect a Server

```bash
toolrill server inspect <name> [--host NAME] [--config FILE] [--sync]
toolrill server inspect inventory --sync
```

Connects directly to the configured downstream, requests `tools/list`, disconnects,
and prints each raw tool name followed by a tab and its description to stdout.
It ignores tool filters when discovering, makes no tool calls, and does not
start a gateway or keep running. An empty result prints no tool rows.
Host defaults to `default`; host/server matching is case-insensitive.
Missing hosts/servers and disabled servers are errors, without connecting.

Inspect uses effective connection settings from the base file, environment
sibling, and `TOOLRILL_` overrides, including headers, args, env, and configured
connection/discovery timeouts. For stdio it launches the trusted configured
command; HTTP requires an already-running downstream. Relative paths use the
CLI working directory.

Without `--sync`, no configuration is written. The valueless `--sync` flag
replaces only that server's base-file `tools.include` with the discovered raw
names, preserving `tools.exclude`, other settings, hosts, and placeholders.
It requires a matching host/server in the base file, even if an override adds
one to effective configuration. Override files and environment variables are
never written; they may still override the saved include list. Connection,
discovery, or cleanup failure reports stderr, exits 1, and leaves the file
unchanged. Restart any running gateway to apply filter changes.

An empty discovery saves `include: []`, which retains the existing allow-all
include behavior (exclusions still apply); it is not a deny-all policy. Filters
remain pattern-based, including any `*` in discovered names. Discovery verifies
only initialization/listing at that moment, not tool-call success or ongoing
readiness. SDK disposal is not a forced-process-termination or shutdown-deadline
guarantee. Only use trusted downstream executables and endpoints.

### List Configured Servers

```bash
toolrill server list [--host NAME] [--config FILE]
```

Prints the selected host's server names, configured enabled/disabled states, and transports.
Listing reads effective configuration, including environment overrides. It
does not connect, report live status, or display connection settings. An empty
list is reported as having no configured servers.

### Remove a Server

```bash
toolrill server remove <name> [--host NAME] [--config FILE]
```

Removes an entry from the selected host's outgoing list in the base file,
using a case-insensitive name match. A
missing name fails without saving. Any remaining entries must pass validation
before saving. Removing the final entry is allowed, but the resulting empty
outgoing list cannot be validated or started until another server is added.
Other hosts and their incoming settings are not changed.

### Validate Configuration

```bash
toolrill config validate [--host NAME] [--config FILE]
```

Validates incoming settings and outgoing servers for all hosts, or only the
host supplied with `--host`. Uses the same effective configuration loading
and downstream server validator as startup.
This checks configuration only; it does not check downstream availability or
whether stdio executables, argument targets or project files exist.

### Start the Host

```bash
toolrill start [--host NAME] [--mode stdio|serve-http] [--config FILE] [--url URL]
```

Selects `default` unless `--host` names another configured host. Its
`incoming.transport` supplies the mode: `stdio` or `streamable-http`.
HTTP hosts use `incoming.url`, exposing MCP at `/mcp`.

`--mode` and `--url` override those settings for this run without saving them.
Switching a stdio host to HTTP without an address uses
`http://127.0.0.1:3001`. Configured HTTP hosts must supply an incoming URL,
or receive one through `--url`. `--url` applies only to the effective HTTP
mode. Hosting remains in the foreground with existing shutdown behavior;
this does not install a service or create a background daemon.

The existing direct modes remain available:

```bash
toolrill stdio [--host NAME] [--config FILE]
toolrill serve-http [--host NAME] [--config FILE] [--url URL]
toolrill manager [--config FILE]
```

The first two explicitly override the selected host's incoming transport.
`manager` reads the same `toolrill.json`, validates the HTTP hosts and distinct
listening addresses, and launches an independent process for each HTTP host.
Each child selects its own outgoing list from that file. Stdio hosts are
skipped because their clients launch them individually. There is no separate
`instances.json` or `--instances` option.

### Different Hosts for Different Clients

After adding named hosts manually, manage their outgoing servers separately:

```bash
toolrill server add inventory --host work --transport streamable-http --url http://localhost:5051/mcp
toolrill server list --host work
toolrill config validate --host work
toolrill start --host work
```

Configure a stdio client to launch `toolrill start --host default` with the
configuration directory as its working directory, or pass an absolute
`--config` path. Another client can connect to an HTTP host's incoming URL
plus `/mcp`. To run all configured HTTP hosts together, use `toolrill manager`.
The [configuration guide](configuration.md#multiple-clients-and-hosts)
shows distinct HTTP hosts and tool sets in one file. This is configuration
selection, not automatic per-client routing or load balancing.

The bundled `development` and `work` hosts retain their example HTTP addresses
but have empty outgoing lists because their old referenced files were absent.
Populate these lists before using manager. Installation does not select these
bundled examples automatically.

## Configuration Files and Overrides

`--config` defaults to `toolrill.json` in the current working directory, for
both source runs and globally installed tools. ToolRill does not automatically
read or migrate `appsettings.json` or `instances.json`. Custom filenames are
allowed, but must contain the unified `hosts`/`incoming`/`outgoing` schema.
Installation does not create a working configuration or select the repository's
sample files automatically. Quote names and paths containing spaces.

Listing, validation, and hosting load configuration in the existing order:

1. The selected base JSON file.
2. An optional environment-specific sibling, such as `toolrill.Production.json`.
3. Environment variables prefixed with `TOOLRILL_`.

`DOTNET_ENVIRONMENT` chooses the environment-specific file and defaults to
`Production`. Environment values override the corresponding base values.
Host and outgoing arrays retain the configuration provider's index-based
override behavior. For example, `TOOLRILL_hosts__0__outgoing__0__enabled=false`
disables the first outgoing server in the first host. Manager and its children
use the same file and override loading rules.

`init`, `server add`, and `server remove` target only the selected base file.
They do not edit environment-specific files or copy environment-variable
values into JSON. Consequently, an effective override can change an added
entry or retain an entry after it is removed from the base file.

Add/remove preserve other hosts, incoming settings, root diagnostics,
unrelated JSON values and existing server properties,
including tool filters. Files are reformatted on saving; JSON comments and
original whitespace are not preserved. Concurrent configuration editing is
not supported.

## Errors and Exit Codes

Command names and options are case-insensitive. Server names follow their
subcommand, and options use separate values (`--config FILE`, not
`--config=FILE`). Unknown options, duplicate options, missing values, and
unexpected arguments fail rather than being ignored. Only `server add --header`
is repeatable; other duplicate options remain errors.

Successful finite commands and help return exit code `0`. Command and
configuration errors are written to stderr and return exit code `1`. Normal
Ctrl+C cancellation is handled as shutdown. Finite management output goes to
stdout; hosting retains its existing protocol and diagnostic output behavior.
