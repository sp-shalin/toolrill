# Containerization

ToolRill runs on .NET; it does not need Python or Node.js. The repository provides
two Linux image targets, both running one foreground HTTP gateway. Downstream
servers have their own runtime requirements. Configuration uses the same schema
as [Server Configuration](configuration.md).

## Repository Deployment Files

The [Dockerfile](../Dockerfile) restores and publishes
[ToolRill](../src/ToolRill/ToolRill.csproj) in Release and starts
`dotnet ToolRill.dll`. The [Compose file](../docker-compose.yml) selects an
image target and mounts its matching configuration read-only.

| Target | Image Base | Downstream | Configuration |
| --- | --- | --- | --- |
| `runtime` (default) | .NET 10 ASP.NET Core runtime | Your already-running HTTP MCP server | [deploy/docker/toolrill.json](../deploy/docker/toolrill.json) |
| `sdk` | .NET 10 SDK | Included .NET project over stdio | [deploy/compose/toolrill.json](../deploy/compose/toolrill.json) |

ASP.NET Core is required for HTTP hosting; the plain .NET runtime image is not
sufficient. Both targets use the published gateway, not `dotnet run` for the
gateway itself. Only the `sdk` target includes the SDK and the unchanged
[StdioServer](../samples/StdioServer/StdioServer.csproj) project.

Run the following commands from the repository root with Docker using Linux
containers. These are usage instructions, not recorded successful deployments.

## External HTTP Downstream

The runtime configuration selects the `default` HTTP host, listening on
`http://0.0.0.0:3001`, with one outgoing server named `external` at
`http://host.docker.internal:5051/mcp`. Start your downstream server separately
before starting ToolRill, or replace that URL with its reachable endpoint. The
image and Compose file do not start an external downstream service.

### Docker

```bash
docker build --target runtime -t toolrill:runtime .
docker run --detach --name toolrill --init --stop-timeout 40 --publish 127.0.0.1:3001:3001 toolrill:runtime
```

Omitting `--target` also builds the final `runtime` target. To override the
downstream URL without editing the configuration, add this option before the
image name in `docker run`:

```text
--env TOOLRILL_hosts__0__outgoing__0__url=http://your-server:5051/mcp
```

### Compose

With no image/config selection variables set, Compose uses the runtime target
and its external HTTP configuration:

```bash
docker compose up --detach --build
```

Compose passes through `TOOLRILL_hosts__0__outgoing__0__url` only when it is set in
your environment. For example, in PowerShell:

```powershell
$env:TOOLRILL_hosts__0__outgoing__0__url = "http://your-server:5051/mcp"
docker compose up --detach --build
```

Changing an environment variable requires recreating the container; it is not
live configuration reload.

## Project-Based Stdio Downstream

The SDK configuration still exposes the gateway over HTTP on port 3001. Its
outgoing server named `stdio` launches the included project with
`dotnet run --project /projects/samples/StdioServer/StdioServer.csproj`.
The downstream communicates over stdio inside the container and exposes no port.

### Docker

Choose this instead of the runtime container above; both examples use the name
`toolrill` and the same host port. Stop and remove the previous container before
switching variants.

```bash
docker build --target sdk -t toolrill:sdk .
docker run --detach --name toolrill --init --stop-timeout 40 --publish 127.0.0.1:3001:3001 toolrill:sdk
```

### Compose

Select both the SDK target and its configuration. In PowerShell:

```powershell
$env:TOOLRILL_IMAGE_TARGET = "sdk"
$env:TOOLRILL_CONFIG_FILE = "./deploy/compose/toolrill.json"
Remove-Item Env:TOOLRILL_hosts__0__outgoing__0__url -ErrorAction SilentlyContinue
docker compose up --detach --build
```

In Bash:

```bash
unset TOOLRILL_hosts__0__outgoing__0__url
TOOLRILL_IMAGE_TARGET=sdk TOOLRILL_CONFIG_FILE=./deploy/compose/toolrill.json docker compose up --detach --build
```

Both variables select deployment files; they are not application configuration
settings. Compose tags the resulting image as `toolrill:sdk` or `toolrill:runtime`
and starts only one service. To return to the runtime variant in PowerShell:

```powershell
Remove-Item Env:TOOLRILL_IMAGE_TARGET, Env:TOOLRILL_CONFIG_FILE -ErrorAction SilentlyContinue
docker compose up --detach --build
```

The SDK image restores and builds the sample in Debug as the non-root user
during the image build, matching the transport's default configuration. Startup
still uses `dotnet run` with implicit restore/build; cached dependencies are not
a guarantee of offline startup. Adding different projects requires including
their sources, ancestor build files and dependencies, and updating `projectPath`.
The runtime target cannot launch project-based stdio servers.

## Direct-Command Stdio Downstreams

HOST-005 also permits stdio servers without source projects. Use `command` and
`args` instead of `projectPath`; add `env` for child environment overrides.
For example, an outgoing entry in a separately supplied configuration:

```json
{
  "name": "compiled",
  "transport": "stdio",
  "command": "dotnet",
  "args": ["/servers/MyServer.dll"],
  "env": { "API_TOKEN": "${MCP_API_TOKEN}" }
}
```

The runtime image can launch a compatible precompiled framework-dependent .NET
server without the SDK. Copy/mount its complete published output (not just the
DLL) read-only under `/servers`, and select your HTTP host configuration at
`/config/toolrill.json`. Self-contained/native executables need compatible Linux
architecture, execute permissions and any native libraries. Relative paths are
relative to the gateway's container working directory. Nonempty `args` cannot
be combined with `projectPath`; the latter still requires the SDK image.

Supply referenced values to the gateway container with Docker `--env` or explicit
Compose environment entries. Stdio children inherit that container environment;
`env` overrides expand `${ENV_VAR}` once at validation/launch without modifying
the gateway. Placeholders reference the gateway environment, not sibling `env`
keys. Missing/empty referenced variables fail enabled-server validation.

Neither repository image installs `docker`, `npx`/Node.js or `uvx`/Python.
Using them requires a deliberately customized image and their runtime, package,
network and writable-cache requirements. Launching `docker run --rm -i ...`
also needs Docker CLI/daemon access; no TTY (`-t`). Child `env` values are not
automatically injected into that nested container: forward selected names with
Docker `--env NAME`. Mounting a Docker socket grants broad daemon privileges
and is not part of the repository deployment.

## Configuration and Access

Both images default to `start --host default --config /config/toolrill.json`.
Keeping deployment configuration outside `/app` prevents the CLI project's
published `toolrill.Production.json` from overriding these examples.

Compose mounts the selected configuration at that path read-only and refuses to
create a missing source path. Direct Docker runs use the image's baked-in
configuration. For an editable external file, add a read-only bind mount before
the image name; this PowerShell example resolves an absolute host path:

```powershell
$config = (Resolve-Path ./deploy/docker/toolrill.json).Path
docker run --detach --name toolrill --init --stop-timeout 40 --publish 127.0.0.1:3001:3001 --mount "type=bind,source=$config,target=/config/toolrill.json,readonly" toolrill:runtime
```

Use the SDK configuration and image instead for project-based stdio downstreams.
Direct-command stdio requires an image containing its executable/runtime. CLI commands
that edit configuration cannot write to the read-only mount; edit the host file
and recreate/restart the gateway instead. To select another named host, replace
the default command with `start --host <name> --config /config/toolrill.json` using
an HTTP host on the same internal port.

Existing configuration layering remains base JSON, optional environment sibling
JSON, then `TOOLRILL_` overrides. The images and Compose set
`DOTNET_ENVIRONMENT=Production`. A sibling override file must also be mounted
beside `/config/toolrill.json` if needed. ToolRill expands `${VARIABLE}` only in
outgoing HTTP URLs/header values and stdio `env` values, not arbitrary JSON.
For other environment overrides, use Docker `--env` or explicitly add them to
Compose; only the outgoing URL above is passed through by this Compose file.

Both targets run as the official image's built-in app user via `APP_UID`, not
root. Gateway binaries and configuration need read access only. The SDK sample's
source/build directories are owned by that user; `DOTNET_CLI_HOME=/home/app` and
`NUGET_PACKAGES=/home/app/.nuget/packages` supply writable tooling/cache paths.
Host-mounted projects must likewise be readable and have writable build output
directories. Host permissions can replace image permissions when mounting files;
do not assume that root-owned or read-only project mounts can run `dotnet run`.

## Networking and Ports

- Both examples bind the gateway to `0.0.0.0:3001` inside the container and
  publish only `127.0.0.1:3001:3001` on the host. `EXPOSE 3001` documents the
  internal port; it does not itself publish it.
- Connect an MCP client to `http://127.0.0.1:3001/mcp`. Tools are qualified as
  `external/<tool>` or `basic/<tool>` according to the selected configuration.
- `localhost` in a downstream URL refers to the gateway container. Use a
  reachable service name for another container on the same Docker network.
- Docker Desktop provides `host.docker.internal`. On native Linux, using that
  host name requires `--add-host host.docker.internal:host-gateway` for Docker
  runs, or `extra_hosts: ["host.docker.internal:host-gateway"]` on the Compose
  service. The downstream must listen on an interface reachable from the
  container, not only host loopback. Alternatively use another reachable URL.
- No downstream stdio port is published. Publishing the gateway beyond host
  loopback is an explicit deployment change, not included authentication or
  access control.

## Health and Shutdown

`GET /health` returns HTTP 200 with an empty body when the HTTP gateway is
serving. The listener starts after initial downstream connection and tool
discovery. This endpoint is basic host liveness, not continuous downstream
readiness, tool-call success, or MCP protocol verification. Stdio inbound mode
has no HTTP health endpoint. `/mcp` is not used as a health probe.

Both images include `curl` and probe `http://127.0.0.1:3001/health` every 30
seconds, with a 30-second startup grace period, a 3-second check timeout and
three retries. Compose inherits the image probe. Docker reports unhealthy
status; this setup does not automatically restart unhealthy containers. Changing
the internal listening port requires updating the health probe and publication.

Docker sends SIGTERM; Linux CLI handling cancels startup connection/discovery
and the running HTTP host, then the existing cleanup disposes downstream
connections. ASP.NET Core's normal host lifetime remains in use. Compose enables
`init` for signal forwarding/reaping and allows 40 seconds before forceful
termination. Direct Docker examples use matching `--init` and `--stop-timeout`.
This allowance is not a guarantee of draining every request or completing
arbitrarily slow cleanup; broader lifecycle hardening is not part of this setup.

For the directly launched container:

```bash
docker stop --timeout 40 toolrill
docker rm toolrill
```

For Compose:

```bash
docker compose down
```

Manager-mode multi-process containers are not the selected topology and have
not received child-process shutdown hardening in this work.
