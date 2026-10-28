# Roadmap

This is the feature and verification tracker for ToolRill. It separates implemented capabilities from planned work and deferred ideas.

## Status Guide
| Status | Meaning |
| ------ | ------ |
| Implemented | Code and documentation exist for the stated scope. |
| Partial | A foundation exists, but the remaining scope is not complete. |
| Planned | Not yet implemented; select explicitly before starting. |
| In Progress | Currently being worked on. |
| Deferred | Future capability to consider when demand justifies it. |

Manual review is tracked separately using `Pending`, `Accepted`, or `Not recorded`.

Manual acceptance was reset to `Pending` for the ToolRill relaunch on 2026-10-28. Implementation statuses are unchanged. Restore and a Release solution build succeeded; tests, benchmarks, Docker, packaging, and external CI still require renamed-product verification.

## Current Snapshot
| Area | Current State |
| ------ | ------ |
| Basic tools gateway | Implemented (`HOST-001`–`HOST-003`, `TOOL-001`–`TOOL-003`). |
| Customer configuration & CLI | Implemented (`CFG-001`–`CFG-003`, `CLI-001`–`CLI-002`, `HOST-004`–`HOST-005`). Manual execution and review pending. |
| Customer setup guidance | Implemented (`DOC-001`, `DOC-002`). Manual execution and review pending. |
| Lifecycle and diagnostics | `RUN-002`, `OBS-001`, and `OBS-004` Implemented (review pending); `RUN-001`, `OBS-002`, and `OBS-003` remain Partial/Planned. |
| Repository Docker deployment | Implemented (`DEP-001`–`DEP-005`). Manual execution and review pending. |
| Automated tests | Implemented (`TEST-001`–`TEST-004`). Manual execution and review pending. |
| Performance evidence | Partial (`PERF-001` starter benchmarks implemented; `PERF-002`–`PERF-003` planned). |
| CI & release readiness | Implemented (`CI-001`, `REL-001`–`REL-003`). Manual review and NuGet publish pending. |

## Core Gateway and Configuration
| ID | Feature | Status | Manual Review | Scope and Reference |
| ------ | ------ | ------ | ------ | ------ |
| HOST-001 | Inbound stdio hosting | Implemented | Not recorded | Client-launched MCP host; see [Architecture](architecture.md). |
| HOST-002 | Inbound Streamable HTTP hosting | Implemented | Not recorded | Foreground HTTP host exposing `/mcp`; see [CLI](cli.md). |
| HOST-003 | Multiple downstream servers | Implemented | Not recorded | Project-based stdio and HTTP connections; see [Configuration](configuration.md). |
| HOST-004 | Named HTTP host orchestration | Implemented | Accepted | `manager` starts independent HTTP host processes from `toolrill.json`. |
| HOST-005 | Arbitrary outgoing stdio executables, args, and env | Implemented | Accepted | Direct `command`, `args`, and expanded `env` alongside `projectPath`; see [Configuration](configuration.md#arbitrary-stdio-commands). |
| TOOL-001 | Tool discovery and aggregation | Implemented | Not recorded | Discover tools from configured servers at startup. |
| TOOL-002 | Qualified tool routing | Implemented | Not recorded | Route `server/tool` calls to the owning downstream connection. |
| TOOL-003 | Per-server tool filters | Implemented | Not recorded | Wildcard and exact `include`/`exclude` filters; see [Configuration](configuration.md). |
| CFG-001 | External configuration and overrides | Implemented | Not recorded | JSON validation, `--config`, environment sibling files, and `TOOLRILL_` overrides. |
| CFG-002 | Unified named-host configuration | Implemented | Accepted | Unified `toolrill.json` with `hosts`, `incoming`, `outgoing`, and `--host` selection. |
| CFG-003 | Outgoing HTTP headers and environment expansion | Implemented | Accepted | Outgoing HTTP `headers` and `${ENV_VAR}` expansion; see [Configuration](configuration.md#authenticated-http-servers). |
| CLI-001 | Minimal management commands | Implemented | Not recorded | `init`, `server add/list/remove`, `config validate`, `help`, and `start`; see [CLI](cli.md). |
| CLI-002 | Live CLI tool discovery and server inspection | Implemented | Accepted | `server add --discover` and `server inspect [--sync]`; see [CLI](cli.md#inspect-a-server). |
| CLI-003 | Prompt token-savings estimation and filter report | Planned | Not recorded | Estimate `tools/list` schema token footprint before vs. after `include`/`exclude` filters in `server inspect`. |

## Runtime, Observability, and CI
| ID | Feature | Status | Manual Review | Scope and Reference |
| ------ | ------ | ------ | ------ | ------ |
| RUN-001 | Lifecycle, forwarding, and failure hardening | Partial | Not recorded | Basic startup/forwarding cleanup exists; broader resiliency hardening remains planned. |
| RUN-002 | Explicit timeouts and end-to-end cancellation | Implemented | Accepted | Configurable connection/discovery and tool-call timeouts with linked cancellation; see [Configuration](configuration.md#timeouts-and-cancellation). |
| OBS-001 | Structured logging and .NET diagnostics | Implemented | Accepted | JSON-line `stderr` diagnostics and `.NET` host/SDK `ILogger` integration; see [Diagnostics](diagnostics.md). |
| OBS-002 | OpenTelemetry-compatible instrumentation | Planned | Not recorded | `ActivitySource` and `Meter` instrumentation for connections and tool calls. |
| OBS-003 | Health endpoints | Planned | Not recorded | Downstream readiness probes beyond basic `/health` liveness. |
| OBS-004 | Sensitive-data protection in logs | Implemented | Accepted | Credential, header, URL query, and configured-value `[REDACTED]` scrubbing; see [Diagnostics](diagnostics.md#sensitive-values). |
| CI-001 | CI checks for implemented capabilities | Implemented | Accepted | Ubuntu/Windows `.NET 10` build and test workflow in [ci.yml](../.github/workflows/ci.yml). |

## Container Deployment
| ID | Feature | Status | Manual Review | Scope and Reference |
| ------ | ------ | ------ | ------ | ------ |
| DEP-001 | Current image and Compose wiring | Implemented | Accepted | `runtime` and `sdk` targets for `ToolRill.csproj` and selectable Compose service; see [Containerization](containerization.md). |
| DEP-002 | Non-root container execution | Implemented | Accepted | Non-root `app` user in both Docker targets. |
| DEP-003 | Required-port-only exposure | Implemented | Accepted | Loopback `127.0.0.1:3001:3001` publication only. |
| DEP-004 | Container health and environment configuration | Implemented | Accepted | Basic `/health` liveness probe and `Production` environment configuration. |
| DEP-005 | Graceful container shutdown | Implemented | Accepted | `SIGTERM` handling, `init: true`, and 40-second stop timeout. |

## Testing Strategy
| ID | Area | Status | Manual Review | Scope and Reference |
| ------ | ------ | ------ | ------ | ------ |
| TEST-001 | Unit | Implemented | Accepted | Configuration, lifecycle, filtering, routing, timeouts, cancellation, and redaction; see [Testing](testing.md). |
| TEST-002 | Integration | Implemented | Accepted | Mixed `stdio`/HTTP discovery, filtering, headers, direct commands, and CLI discovery; see [Testing](testing.md). |
| TEST-003 | Protocol | Implemented | Accepted | Official SDK `2.2.0` `tools/list`, `tools/call`, schemas, errors, and cancellation; see [Testing](testing.md). |
| TEST-004 | Concurrency | Implemented | Accepted | Concurrent HTTP clients, overlapping tool calls, discovery, and reconnect cycles; see [Testing](testing.md). |

## Performance Benchmarking
| ID | Feature | Status | Manual Review | Scope and Reference |
| ------ | ------ | ------ | ------ | ------ |
| PERF-001 | Starter name-creation benchmarks | Implemented | Accepted | BenchmarkDotNet starter project; see [Benchmarks](benchmarks.md). |
| PERF-002 | Host-operation benchmark coverage | Planned | Not recorded | Discovery, filter evaluation, routing, and concurrent forwarding benchmarks. |
| PERF-003 | Cross-runtime comparison | Planned | Not recorded | Reproducible `.NET` vs. `Python` vs. `TypeScript` comparison harness. |

## License and Release Readiness
| ID | Feature | Status | Manual Review | Scope and Reference |
| ------ | ------ | ------ | ------ | ------ |
| REL-001 | Apache License 2.0 text | Implemented | Accepted | Standard Apache 2.0 text in [LICENSE](../LICENSE) and `README.md`. |
| REL-002 | Release readiness | Implemented | Accepted | Version `0.1.0` ToolRill NuGet `.NET Tool` metadata and [Release guide](release.md). Local package verification and NuGet publication remain pending. |
| REL-003 | Unify entry-point project name as `ToolRill` | Implemented | Accepted | Entry point unified as `src/ToolRill/ToolRill.csproj` (`ToolRill.dll`) across code, Docker, tests, and docs. |

## Future Roadmap
Every item below is **Deferred** until explicitly selected.

### Performance and Compatibility
| ID | Idea | Status | Tracking Note |
| ------ | ------ | ------ | ------ |
| FUT-001 | Deeper performance optimization | Deferred | Base scope on measured hot paths. |
| FUT-002 | Larger benchmark suite | Deferred | Expand beyond initial `PERF-002` scope when needed. |
| FUT-003 | Protocol compatibility improvements | Deferred | Prioritize observed interoperability gaps. |
| FUT-004 | Additional MCP capabilities | Deferred | Aggregate and route MCP `resources` and `prompts` alongside `tools`. |
| FUT-005 | Improved diagnostics | Deferred | Usage-driven additions beyond `OBS-001`/`OBS-002`. |
| FUT-006 | Improved configuration | Deferred | Usage-driven additions beyond `CFG-001`–`CFG-003`. |
| FUT-007 | Additional cross-runtime comparisons | Deferred | Further studies beyond `PERF-003`. |

### Real-World Feedback
| ID | Idea | Status | Tracking Note |
| ------ | ------ | ------ | ------ |
| FUT-008 | Operational improvements | Deferred | Define scope from operating experience. |
| FUT-009 | Deployment improvements | Deferred | Beyond initial `DEP-001`–`DEP-005` scope. |
| FUT-010 | Security improvements | Deferred | Define concrete requirements. |
| FUT-011 | Configuration improvements from feedback | Deferred | Track concrete requests under `FUT-006`. |
| FUT-012 | Additional transports | Deferred | Select only for demonstrated client/server needs. |
| FUT-013 | Reliability improvements | Deferred | Beyond initial `RUN-001`/`RUN-002` scope. |
| FUT-014 | Degraded startup and background reconnect | Deferred | Start with healthy downstreams when one server is offline and retry in the background. |
| FUT-015 | Live configuration reload and tool-list notifications | Deferred | Watch `toolrill.json`, refresh `McpToolRegistry` in memory, and emit `notifications/tools/list_changed`. |

#### Extensibility and Plugins
| ID | Feature | Status | Tracking Note |
| ------ | ------ | ------ | ------ |
| PLUG-001 | Core extensibility seams | Deferred | Public DI interfaces for inbound auth, secret resolution, tool call interception, and audit sinks. |
| PLUG-004 | Dynamic plugin loading | Deferred | Load external extension assemblies at startup. |


### Documentation and Ecosystem Guides
| ID | Feature / Idea | Status | Manual Review | Scope and Reference |
| ------ | ------ | ------ | ------ | ------ |
| DOC-001 | Native multi-project customer setup guide | Implemented | Pending | Three-project setup for VS Code, Claude Code, and custom workflows in [Customer Setup](CUSTOMER_SETUP.md). |
| DOC-002 | Docker multi-project customer setup guide | Implemented | Pending | Containerized multi-host and manager setups in [Customer Docker Setup](CUSTOMER_DOCKER_SETUP.md). |
| DOC-003 | Additional MCP client setup recipes | Deferred | Not recorded | Setup snippets for Cursor, Windsurf, Zed, Goose, and Codex CLI alongside DOC-001/DOC-002. |
| DOC-004 | Downstream server filter profiles | Deferred | Not recorded | Sample `toolrill.json` safe-mode `include`/`exclude` templates for popular MCP servers. |
| DOC-005 | Registry and directory listings | Deferred | Not recorded | Publish package/container metadata to community MCP directories after REL-002. |
