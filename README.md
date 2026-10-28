# ToolRill - Universal MCP Host & Tool Gateway (.NET 10)

ToolRill is a **language-agnostic Model Context Protocol (MCP) host and tool gateway** built on .NET 10. Connect any MCP client (VS Code, Claude Code, Goose, Cursor, or custom agents) to one unified gateway endpoint to aggregate and filter tools across any downstream MCP server—whether remote HTTP APIs, Docker containers, Node (`npx`), Python (`uvx`), or local .NET projects.

The gateway itself uses the official C# MCP SDK with zero Python or Node.js overhead, and can run directly via the .NET CLI or inside a self-contained Docker container with no host .NET installation required. Performance, simplicity, and production readiness are project goals, not claims of completed hardening or measured performance.

## Current Capabilities

- Inbound MCP over `stdio` or `Streamable HTTP`.
- Multiple downstream servers over `Streamable HTTP` (with custom `headers` and `${ENV_VAR}` expansion) or `stdio` (arbitrary executables/`args`/`env` or local `.NET` projects).
- Live CLI tool discovery (`toolrill server add --discover` and `toolrill server inspect --sync`), aggregation, `server/tool` routing, and per-server `include`/`exclude` wildcard filters.
- JSON configuration, validation, environment overrides, configurable timeouts, and sensitive-data log redaction.
- A minimal CLI and Linux Docker targets (`runtime` and `sdk`) for single-host or multi-host (`manager`) foreground hosting.

#### Why Use ToolRill?

Connecting an IDE directly to a single MCP server works well for simple setups, but breaks down as you add multiple servers, projects, and AI clients:
- **Prevent LLM Context & Token Bloat:** Connecting 3 to 5 MCP servers directly to VS Code or Claude Code can expose 50 to 150+ tools on every prompt. ToolRill lets you define named project hosts with `include`/`exclude` wildcard filters so each workspace only receives the exact tools it needs.
- **Add Tool Filtering to Any Downstream Server:** Most community and internal MCP servers expose every tool (including destructive operations) with no built-in filtering. ToolRill enforces per-server allowlists and blocklists at the gateway layer without modifying downstream server code.
- **Configure Once Across Clients:** Define your downstream servers and tool policies in one `toolrill.json` file and connect VS Code, Claude Code, and custom C# agent workflows to lightweight local or containerized HTTP endpoints.
- **Zero Python or Node.js Runtime Dependency:** Built natively on .NET 10 and the official C# MCP SDK, fitting directly into .NET enterprise environments and standard NuGet toolchains.

## Getting Started

Install the .NET 10 SDK for either option below. These instructions are for
using ToolRill, not setting up a contribution environment.

### Install the Package

No repository clone is needed. Once a `ToolRill` release is available on NuGet:

```bash
dotnet tool install --global ToolRill
```

If you downloaded a `.nupkg` instead, add its containing folder with
`--add-source <package-folder>` to the install command. The .NET global tools
directory must be on your `PATH`. If no package release is available yet,
use the source option below.

In a directory where you want to keep your configuration:

```bash
toolrill init
toolrill server add inventory --transport streamable-http --url http://localhost:5051/mcp
toolrill config validate
toolrill start --mode serve-http
```

### Run from a Clone

Clone this repository using its GitHub clone URL, then run from its root.
No global tool installation is needed:

```bash
git clone <repository-url> toolrill
cd toolrill
dotnet run --project src/ToolRill -- init
dotnet run --project src/ToolRill -- server add inventory --transport streamable-http --url http://localhost:5051/mcp
dotnet run --project src/ToolRill -- config validate
dotnet run --project src/ToolRill -- start --mode serve-http
```

### Use the Host

Both examples create `toolrill.json` in the working directory, including when
using the globally installed tool. ToolRill does not automatically use your
project's `appsettings.json`. Replace the
example downstream URL with your running MCP server's endpoint. Connect your
MCP client to ToolRill at `http://127.0.0.1:3001/mcp`; press Ctrl+C to stop the host.

`inventory` is a name you choose for the downstream server, not a command
keyword. Its tools are exposed as `inventory/<tool>`.

One file contains named `hosts`, each with `incoming` settings for clients
connecting to ToolRill and an `outgoing` list of downstream servers. `init`
creates a `default` stdio host. Use `--host <name>` to select another configured
host; `start` reads its incoming settings. Different clients can select
different hosts and tool sets. `manager` starts all configured HTTP hosts
from this same file; no separate `instances.json` is needed.

For client-launched stdio hosting, omit `--mode serve-http` after initialization.
The flags override configuration for that run without changing the file. See the
[CLI guide](docs/cli.md) for commands and the
[configuration guide](docs/configuration.md) for local stdio servers, multiple
hosts, and tool filters. Existing custom configuration paths remain available
with `--config`, but their contents must use the new unified schema.

## Documentation

| Guide | Contents |
| --- | --- |
| [Architecture](docs/architecture.md) | Project roles, startup, request flow, and host modes. |
| [CLI](docs/cli.md) | Installation, commands, and a configure-and-run workflow. |
| [Configuration](docs/configuration.md) | Servers, transports, tools, lifecycle, and failures. |
| [Containerization](docs/containerization.md) | Deployment considerations and current Docker limitations. |
| [Diagnostics](docs/diagnostics.md) | Logging settings and stdio considerations. |
| [Release](docs/release.md) | NuGet tool packaging, local verification, and publishing guide. |
| [Testing](docs/testing.md) | Unit, integration, protocol, and concurrency test suites and commands. |
| [Benchmarks](docs/benchmarks.md) | BenchmarkDotNet harness and cross-runtime comparison guide. |
| [Server Samples](samples/README.md) | Connect the included stdio and HTTP servers through one gateway. |
| [Multi-Project Setup](docs/CUSTOMER_SETUP.md) | Scenario guide for per-project tool selections across different clients. |
| [Multi-Project Docker Setup](docs/CUSTOMER_DOCKER_SETUP.md) | Scenario guide for running project hosts in separate containers. |
| [Contributing](CONTRIBUTING.md) | Development setup, coding style, and contribution scope. |
| [Security](SECURITY.md) | Private vulnerability reporting and security boundaries. |

## Scope and Security Boundary

ToolRill is open source under the Apache License 2.0; see [License](#license) and the [Roadmap](docs/roadmap.md).
Note that named hosts and tool filters are **configuration selectors, not security boundaries**—inbound client authentication, downstream secret vaulting, and multi-tenant isolation are outside the scope of this gateway. Bind HTTP hosts to loopback (`127.0.0.1`) or place them behind an authenticated reverse proxy when running in shared environments.

## License

ToolRill is licensed under the Apache License, Version 2.0. The [LICENSE](LICENSE)
file contains the complete, unmodified standard license text.
Dependency licenses remain applicable.
