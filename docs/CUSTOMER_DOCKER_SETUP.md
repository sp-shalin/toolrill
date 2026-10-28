# ToolRill Customer Setup with Docker

This is a scenario guide using placeholder downstream servers, not a
self-contained runnable demo. For the included stdio and HTTP servers, start
with the [server sample walkthrough](../samples/README.md).

This guide covers the same three-project tool selections as
[the non-container customer guide](CUSTOMER_SETUP.md), with ToolRill running in
Linux containers. Clients can be VS Code, Claude Code, or a custom MCP workflow.
The customer projects and clients remain on your computer; only the gateway
runs in Docker in the main example.

The recommended layout is **one named host per container**, all reading one
shared `toolrill.json`. This gives each host separate ports, logs, connections,
and lifecycle control without introducing new ToolRill features.

## 1. Current Repository Deployment Status

The repository's root [Dockerfile](../Dockerfile) and [Compose file](../docker-compose.yml) run a single foreground HTTP gateway on port `3001`. However, that root Compose setup only runs one default service on one port (`3001`), not the three simultaneous project hosts (`3101`, `3102`, `3103`) and external customer configuration mounts used in this multi-project scenario.

Therefore, the following sections show standalone customer `Dockerfile.*` and `compose.yaml` examples tailored for a multi-project host layout outside the repository. There is no assumption that a ready-made `ToolRill` container image has been published to a container registry.

## 2. Prerequisites and Installation Choices

On Windows, install Docker Desktop and select **Linux containers**. Docker
must be running. On other systems, use Docker Engine/Desktop with Docker
Compose v2. Examples use PowerShell and Windows paths; adapt the host-side
paths and shell syntax on Linux/macOS. Container paths are Linux paths.

The container images supply .NET 10. You do not need a host .NET installation
for the image builds shown here. Source setup needs Git or another way to
obtain the repository. VS Code, Claude Code, or your custom MCP client remain
separate prerequisites. Start the three downstream servers as described below.

Choose one installation method **inside the image**:

| Setup | Image installation | Host machine requirement |
| --- | --- | --- |
| 1: Global tool | Install `ToolRill` globally for the container's user. | Docker; a matching published NuGet package. |
| 2: Local tool | Install `ToolRill` in a tool manifest inside the image. | Docker; a matching published NuGet package. |
| 3: Source clone | Publish `src/ToolRill/ToolRill.csproj` in a build stage and run `ToolRill.dll`. | Docker and a repository clone. |

A global or project-local tool installed on **Windows** is not automatically
available inside a Linux container. Do not mount a Windows tool executable or
Windows build output and assume it will run there. Install/publish for the
container environment using the examples below. Your customer application's
project file does not need a ToolRill package reference.

For Setups 1-2, use a released package version containing the unified
`hosts`/`incoming`/`outgoing` feature. If that package is not available, use
Setup 3. Renaming a file does not update an older executable. Private or local
package sources need their own Docker build/package-source setup; do not put
feed credentials in a committed Dockerfile or build argument.

## 3. Locations, Networks, and Tool Requirements

Use this example customer layout outside the ToolRill source repository:

```text
C:/McpDocker/
    Dockerfile.global
    Dockerfile.local
    Dockerfile.source
    compose.yaml
    config/
        toolrill.json
C:/Tools/toolrill/             Repository clone for Setup 3
C:/Work/Project1/            VS Code or another client's project
C:/Work/Project2/
C:/Work/Project3/
```

Only the Dockerfile for your chosen setup is needed. Create the directories:

```powershell
New-Item -ItemType Directory -Force C:/McpDocker/config
```

The mount will make `C:/McpDocker/config/toolrill.json` available inside each
container as `/config/toolrill.json`. The file belongs to the customer, not to
the image's installation directory. Mount the configuration directory
read-only for hosting; editing happens on the host machine.

### The Three Downstream Servers

Assume these servers are already running on your Windows host:

| Server | Host MCP endpoint | Example raw tool names |
| --- | --- | --- |
| Server 1 | `http://localhost:5051/mcp` | `s1_tool1` through `s1_tool4` |
| Server 2 | `http://localhost:5052/mcp` | `s2_tool1` through `s2_tool5` |
| Server 3 | `http://localhost:5053/mcp` | `s3_tool1` through `s3_tool10` |

These are placeholders, not tools or servers shipped with ToolRill. Replace
them with actual MCP endpoints and the raw names reported by those servers.
Filters are based on tool names, not numeric positions.

| Project | Selection | Expected ToolRill tools |
| --- | --- | --- |
| Project 1 | Three Server 1 tools; exclude its fourth tool. | 3 |
| Project 2 | Two Server 1 tools and eight Server 3 tools; omit Server 2. | 10 |
| Project 3 | All four + five + ten tools from all three servers. | 19 |

### Three Different Address Spaces

| Address | Meaning |
| --- | --- |
| `0.0.0.0:3101` inside a container | Listen on container interfaces so published ports can reach ToolRill. Not a client URL. |
| `http://127.0.0.1:3101/mcp` on your computer | Client-facing published endpoint for Project 1. |
| `http://host.docker.internal:5051/mcp` inside a container | Outgoing connection to Server 1 on the Docker host. |

`localhost` inside a container means **that container**, not your Windows
computer or another container. Docker Desktop provides `host.docker.internal`
for host access. This is why the outgoing addresses below differ from the
non-container guide.

Your downstream services must listen on an interface reachable from Docker,
and the host firewall must allow the connection. Docker Desktop may provide
host-loopback access through its networking, but verify this for your service
and environment rather than assuming a host-only listener is reachable.
On native Linux, reaching a host-loopback-only service through the bridge
gateway generally requires changing its bind address or network setup.
Do not broaden network access without considering downstream authentication
and firewall rules.

## 4. Complete Docker Configuration

Create `C:/McpDocker/config/toolrill.json` with the following contents. This is
the same unified schema and filtering policy as the non-container example;
only network addresses change.

```json
{
  "diagnostics": {
    "enabled": false
  },
  "hosts": [
    {
      "name": "project-1",
      "incoming": {
        "transport": "streamable-http",
        "url": "http://0.0.0.0:3101"
      },
      "outgoing": [
        {
          "name": "server-1",
          "transport": "streamable-http",
          "url": "http://host.docker.internal:5051/mcp",
          "tools": {
            "include": ["s1_tool1", "s1_tool2", "s1_tool3"],
            "exclude": ["s1_tool4"]
          }
        }
      ]
    },
    {
      "name": "project-2",
      "incoming": {
        "transport": "streamable-http",
        "url": "http://0.0.0.0:3102"
      },
      "outgoing": [
        {
          "name": "server-1",
          "transport": "streamable-http",
          "url": "http://host.docker.internal:5051/mcp",
          "tools": {
            "include": ["s1_tool1", "s1_tool2"]
          }
        },
        {
          "name": "server-3",
          "transport": "streamable-http",
          "url": "http://host.docker.internal:5053/mcp",
          "tools": {
            "include": [
              "s3_tool1", "s3_tool2", "s3_tool3", "s3_tool4",
              "s3_tool5", "s3_tool6", "s3_tool7", "s3_tool8"
            ]
          }
        }
      ]
    },
    {
      "name": "project-3",
      "incoming": {
        "transport": "streamable-http",
        "url": "http://0.0.0.0:3103"
      },
      "outgoing": [
        {
          "name": "server-1",
          "transport": "streamable-http",
          "url": "http://host.docker.internal:5051/mcp"
        },
        {
          "name": "server-2",
          "transport": "streamable-http",
          "url": "http://host.docker.internal:5052/mcp"
        },
        {
          "name": "server-3",
          "transport": "streamable-http",
          "url": "http://host.docker.internal:5053/mcp"
        }
      ]
    }
  ]
}
```

`incoming` is client -> ToolRill. `outgoing` is ToolRill -> downstream servers.
A host name chooses a configuration; an outgoing server name qualifies its
tools, such as `server-1/s1_tool1`. Host names are not added to tool names.
Names are case-insensitive for selection and filtering; server names need
only be unique within their host.

Filters match raw tool names. A nonempty `include` allows only its matches,
and `exclude` takes precedence. Project 2's excluded remainder does not need
an explicit exclude list. Project 3 omits filters and receives all tools.
Exact allowlists keep newly introduced tools out of Projects 1-2; Project 3
will include new tools after restart and may then have more than 19.

This file deliberately has no `default` host. Individual starts must supply
`--host project-1`, `project-2`, or `project-3`. The entrypoint examples below
receive their command through Docker/Compose rather than relying on bare
ToolRill startup.

## 5. Build One Customer Image

All three image examples expose a compatible command entrypoint. They use
the local tag `toolrill-customer:local`, so the same Compose file works regardless
of your installation choice. Build one image, then proceed to Section 6.

The following package examples deliberately retain the SDK image to make
tool installation and .NET/ASP.NET Core runtime availability straightforward.
They are basic customer examples, not minimal hardened production images.

### Setup 1: Global Tool Inside the Container

Example `C:/McpDocker/Dockerfile.global`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0
ARG TOOLRILL_VERSION
RUN dotnet tool install --global ToolRill --version "${TOOLRILL_VERSION}"
ENV PATH="/root/.dotnet/tools:${PATH}"
WORKDIR /app
ENTRYPOINT ["toolrill"]
```

Build from the customer directory, substituting a real released version:

```powershell
Set-Location C:/McpDocker
docker build -f Dockerfile.global --build-arg TOOLRILL_VERSION=<released-version> -t toolrill-customer:local .
```

This global installation is for the image's container user, not your host
account. Your projects and HTTP clients do not need a host global ToolRill
installation. The example runs as the image's default user; non-root execution
requires deliberately configuring tool paths and file access as part of a
separate deployment-hardening task.

### Setup 2: Local Tool Manifest Inside the Container

Example `C:/McpDocker/Dockerfile.local`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0
ARG TOOLRILL_VERSION
WORKDIR /tool
RUN dotnet new tool-manifest
RUN dotnet tool install ToolRill --version "${TOOLRILL_VERSION}"
ENTRYPOINT ["dotnet", "tool", "run", "toolrill", "--"]
```

Build it:

```powershell
Set-Location C:/McpDocker
docker build -f Dockerfile.local --build-arg TOOLRILL_VERSION=<released-version> -t toolrill-customer:local .
```

The manifest lives at `/tool/.config/dotnet-tools.json` and `/tool` remains
the working directory for local-tool resolution. `/config/toolrill.json` is
still a separate mounted customer file. Do not change the runtime working
directory away from the manifest or mount another directory over `/tool`.

If you already maintain a checked-in customer tool manifest, an alternative
image can copy that manifest and run `dotnet tool restore` instead of creating
one. Its version/package source must be suitable for the container. Do not
copy the host's resolved binary or caches as an installation substitute.

### Setup 3: Build from a Repository Clone

Obtain the source using the actual repository GitHub URL:

```powershell
git clone <repository-url> C:/Tools/toolrill
```

Example `C:/McpDocker/Dockerfile.source`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/ToolRill/ToolRill.csproj
RUN dotnet publish src/ToolRill/ToolRill.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "ToolRill.dll"]
```

Build with the **repository** as context and the **customer** Dockerfile:

```powershell
docker build -f C:/McpDocker/Dockerfile.source -t toolrill-customer:local C:/Tools/toolrill
```

The context must contain `Directory.Build.props` and the CLI/Core/Client/Server
projects. The repository's `.dockerignore` excludes generated output and test
folders. This example restores/publishes the CLI project, not the test solution.
It uses `aspnet:10.0` because the gateway includes ASP.NET Core HTTP hosting;
the plain `runtime:10.0` image alone is not a sufficient choice for this build.

This source runtime image has no SDK. That is sufficient for outgoing HTTP
servers but not for ToolRill's current outgoing stdio `dotnet run --project`
transport. See Section 10 before using local downstream projects.

Bundled source configuration may exist under `/app`, but the explicit
`--config /config/toolrill.json` selects your mounted file instead. Overrides
are looked up beside that file, not beside the assembly.

## 6. Recommended Compose Deployment: One Host per Container

Example `C:/McpDocker/compose.yaml`:

```yaml
services:
  project-1:
    image: toolrill-customer:local
    init: true
    restart: "no"
    command: ["start", "--host", "project-1", "--config", "/config/toolrill.json"]
    ports:
      - "127.0.0.1:3101:3101"
    volumes:
      - ./config:/config:ro
    extra_hosts:
      - "host.docker.internal:host-gateway"

  project-2:
    image: toolrill-customer:local
    init: true
    restart: "no"
    command: ["start", "--host", "project-2", "--config", "/config/toolrill.json"]
    ports:
      - "127.0.0.1:3102:3102"
    volumes:
      - ./config:/config:ro
    extra_hosts:
      - "host.docker.internal:host-gateway"

  project-3:
    image: toolrill-customer:local
    init: true
    restart: "no"
    command: ["start", "--host", "project-3", "--config", "/config/toolrill.json"]
    ports:
      - "127.0.0.1:3103:3103"
    volumes:
      - ./config:/config:ro
    extra_hosts:
      - "host.docker.internal:host-gateway"
```

The `host-gateway` mapping supports compatible Docker Engine installations
where the host alias is not supplied automatically, especially native Linux.
Docker Desktop normally provides host access itself; if overriding the alias
does not work with your Desktop network, remove `extra_hosts` and use its
built-in `host.docker.internal` resolution. Neither option overrides host
firewalls or makes host-loopback-only services universally reachable.

Docker publishes ports on **host loopback only**. Inside the containers,
ToolRill binds `0.0.0.0` so those publications can reach it. This does not mean
you should publish `3101:3101` on every host interface for an untrusted network.

Start the downstream MCP servers first. Once the mounted file is populated:

```powershell
Set-Location C:/McpDocker
docker compose run --rm --no-deps project-1 config validate --config /config/toolrill.json
docker compose up -d
docker compose ps
docker compose logs -f
```

The one-off validation command uses the same image/mount but does not start
hosting or publish the service's ports. It validates all host settings, not
live server availability or real tool-name matches.

| Container Service | Selected host | Host-machine client URL | Expected tools |
| --- | --- | --- | --- |
| `project-1` | `project-1` | `http://127.0.0.1:3101/mcp` | 3 |
| `project-2` | `project-2` | `http://127.0.0.1:3102/mcp` | 10 |
| `project-3` | `project-3` | `http://127.0.0.1:3103/mcp` | 19 |

There is no `manager` inside this recommended layout. Docker/Compose launches
three separate single-host processes instead. Do not start a host-machine
manager against the same published ports at the same time.

Detached containers keep running after you close client windows. View logs
with Compose; press Ctrl+C to exit log following without stopping containers.
Stop/remove this deployment with:

```powershell
Set-Location C:/McpDocker
docker compose down
```

This does not delete your bind-mounted host configuration or stop the
independently running downstream servers. `restart: "no"` is intentional:
this example does not claim automatic recovery or hide startup failures.

## 7. Scenario 1: Three VS Code Projects

All three installation/image choices produce the same client endpoints.

1. Complete one image setup and launch the three Compose services.
2. Open Projects 1, 2, and 3 in separate VS Code windows.
3. Add only the corresponding HTTP entry to each project's workspace MCP file.
4. Review workspace/MCP trust, connect using `MCP: List Servers`, and inspect
   the provider's tools in the chat tool picker. Use a tool-capable agent mode.

`C:/Work/Project1/.vscode/mcp.json`:

```json
{
  "servers": {
    "toolrill-project-1": {
      "type": "http",
      "url": "http://127.0.0.1:3101/mcp"
    }
  }
}
```

`C:/Work/Project2/.vscode/mcp.json`:

```json
{
  "servers": {
    "toolrill-project-2": {
      "type": "http",
      "url": "http://127.0.0.1:3102/mcp"
    }
  }
}
```

`C:/Work/Project3/.vscode/mcp.json`:

```json
{
  "servers": {
    "toolrill-project-3": {
      "type": "http",
      "url": "http://127.0.0.1:3103/mcp"
    }
  }
}
```

Recent VS Code versions also support project-root `.mcp.json` with a
`mcpServers` wrapper, like the Claude examples below. Choose the supported
format for your version and avoid duplicate entries in both locations.

Expected result: Project 1 sees Server 1 tools 1-3, Project 2 sees Server 1
tools 1-2 and Server 3 tools 1-8, and Project 3 sees all 19. Review existing
user-wide providers and direct downstream entries: those can expose extra
tools outside the gateway's filtering policy.

## 8. Scenario 2: VS Code Projects 1-2 and Claude Code Project 3

No changes to the Docker image, Compose services, or ToolRill file are needed.

1. Start the three services and configure the first two VS Code projects as
   in Section 7.
2. From Project 3, add its published HTTP endpoint to Claude Code at project scope:

```powershell
Set-Location C:/Work/Project3
claude mcp add --transport http --scope project toolrill-project-3 http://127.0.0.1:3103/mcp
```

This creates/updates `C:/Work/Project3/.mcp.json` with this entry:

```json
{
  "mcpServers": {
    "toolrill-project-3": {
      "type": "http",
      "url": "http://127.0.0.1:3103/mcp"
    }
  }
}
```

3. Start Claude Code in Project 3, review project/server approval, and use
   `/mcp` to inspect the connection and discovered tools.

Expected result: three tools in VS Code Project 1, ten in VS Code Project 2,
and 19 through Claude Code Project 3's ToolRill connection. Claude's project MCP
scope is unrelated to whether the container image used a local .NET tool.

## 9. Scenario 3: VS Code Project 1, Claude Code Project 2, Custom Workflow Project 3

1. Keep the same three Docker services and the same shared configuration.
2. Configure VS Code Project 1 using its Section 7 entry.
3. From Project 2, configure Claude Code:

```powershell
Set-Location C:/Work/Project2
claude mcp add --transport http --scope project toolrill-project-2 http://127.0.0.1:3102/mcp
```

Project 2's `.mcp.json` entry is:

```json
{
  "mcpServers": {
    "toolrill-project-2": {
      "type": "http",
      "url": "http://127.0.0.1:3102/mcp"
    }
  }
}
```

4. Review approvals and tools in Claude's `/mcp` panel.
5. Configure Project 3's workflow MCP adapter for Streamable HTTP at
   `http://127.0.0.1:3103/mcp` when the workflow runs on the host machine.

If the workflow accepts the common client connection format, its entry is:

```json
{
  "mcpServers": {
    "toolrill-project-3": {
      "type": "http",
      "url": "http://127.0.0.1:3103/mcp"
    }
  }
}
```

The location and format of this client file depend on the workflow product.
This is not a universal workflow schema or another ToolRill config file.
Its adapter must use an MCP SDK to initialize, discover tools using
`tools/list`, register definitions with the AI runner, invoke `tools/call`
with discovered names/schema-valid arguments, and handle sessions, pagination,
HTTP/SSE responses and cleanup. An ordinary REST/webhook integration is not
automatically an MCP client. ToolRill does not execute the AI workflow itself.

Expected result: VS Code Project 1 gets three tools, Claude Code Project 2
gets ten, and the Project 3 workflow discovers all 19. These counts exclude
client built-in tools and other MCP providers. Client policy, approvals, tool
schema compatibility, and model selection can further affect usability.

### If the Custom Workflow Also Runs in Docker

Place its container on the same Compose network, then configure its client
URL as `http://project-3:3103/mcp`, using the Compose service name. Do not use
`localhost:3103`, which would point back to the workflow container itself.
No host port publication is needed for that container-to-container connection.

A workflow in another Compose project needs an explicitly shared Docker
network or a deliberately configured route to the host's published endpoint.
The sample host publications are loopback-only; do not assume a separate
container can reach them through the host gateway. A remote/WSL/dev-container
VS Code session has the same address-space issue: use a route appropriate to
where its MCP client actually runs, not just where the editor UI runs.

## 10. Other Downstream Server Locations

### Downstream Servers in the Same Docker Network

If Server 1/2/3 are Compose services on the same network, use their service
names and **container** listening ports in every relevant outgoing entry:

```json
"url": "http://server-1:5051/mcp"
```

The equivalent URLs for the other two would be `http://server-2:5052/mcp`
and `http://server-3:5053/mcp`, assuming those are their service names and
internal ports. If a server is published as `5051:8080`, use
`http://server-1:8080/mcp` inside the network, not host port 5051.

Those downstream services must bind reachable container interfaces. They need
their own correct images/configuration; this guide does not invent images for
your three unspecified servers. Remove the host-alias mapping if all outgoing
connections use service names. Avoid publishing downstream ports to the host
unless they are needed elsewhere.

Compose starting services in order is not proof of readiness. ToolRill currently
fails a host's startup on a downstream connection failure rather than waiting
with automatic retry. Start/verify downstream readiness using their supported
mechanisms before launching the gateway. Meaningful healthchecks depend on
the actual server and are not provided by this basic example.

### Remote HTTP Downstream Servers

Use the actual reachable MCP URL, such as `https://mcp.example.com/mcp`, rather
than a Docker service name or host alias. Review DNS, proxy settings, trusted
CA certificates, firewall rules, and credentials separately. Do not add
unsupported authentication keys to ToolRill's minimal outgoing schema or assume
client credentials are transparently forwarded to downstream servers.

### Outgoing Stdio .NET Projects Inside ToolRill's Container

When an outgoing stdio entry uses `projectPath`, ToolRill launches
`dotnet run --project <projectPath>`. That needs the .NET SDK and the complete
downstream project's source/dependencies inside the gateway container.

Setups 1-2 above already use an SDK image. For Setup 3, replace its runtime
stage's `aspnet:10.0` base with `sdk:10.0` if these local project launches are
required. Supply source by a reviewed image copy or bind mount and use Linux
paths, for example:

```json
{
  "name": "server-1",
  "transport": "stdio",
  "command": "dotnet",
  "projectPath": "/servers/Server1/Server1.csproj",
  "tools": {
    "include": ["s1_tool1", "s1_tool2", "s1_tool3"],
    "exclude": ["s1_tool4"]
  }
}
```

Use the appropriate filters per host, not the Project 1 list everywhere.
`C:/McpServers/...` is a host path, not a valid container project path.
Source layouts must include referenced projects and parent build files.
`dotnet run` may restore packages and write `bin`/`obj`; a read-only source
mount alone can fail. Use deliberately writable per-container build areas
or put the source in the SDK image. Do not let multiple containers overwrite
one shared host build-output directory without planning for that concurrency.

Each ToolRill host that includes an outgoing stdio server launches its own
downstream process. It does not attach to another container's stdin. For
separately containerized downstream servers, HTTP is the simpler connection.
Alternatively, omit `projectPath` and set `command` plus an optional `args`
array to launch a prebuilt .NET DLL, Python/Node server, or another executable.
Install that executable and its required runtimes inside the gateway image;
the runtime-only image does not supply Python or Node.js. See
[arbitrary stdio commands](configuration.md#arbitrary-stdio-commands).

Absolute container paths avoid working-directory ambiguity. A relative
`projectPath` follows the gateway's working directory (`/app` or `/tool` in
these images), not `/config`. Mounting your customer project is not necessary
for an HTTP-only gateway. If tools need files, mount/configure them only where
the downstream server actually runs, with the minimum appropriate permissions.

## 11. Optional: One Container Running Manager

You can retain ToolRill's manager approach instead of three Compose services.
It starts three child HTTP host processes inside **one** container using the
same file. Do not run it alongside Section 6's containers on the same ports.

For Docker Desktop with its built-in host alias, an example is:

```powershell
docker run -d --init --name toolrill-manager -p 127.0.0.1:3101:3101 -p 127.0.0.1:3102:3102 -p 127.0.0.1:3103:3103 --mount type=bind,source=C:/McpDocker/config,target=/config,readonly toolrill-customer:local manager --config /config/toolrill.json
docker logs -f toolrill-manager
```

On a compatible native Linux engine, adapt the mount path and add
`--add-host host.docker.internal:host-gateway` when your outgoing servers
run on the host. The host-binding caveats from Section 3 still apply.

Client files and filtering results stay exactly the same. Manager validates
the HTTP profiles before launching children, but it does not wait for confirmed
readiness or supervise/restart failed children. Docker can report the parent
container as running even if a child host has failed. One-container manager
therefore has less useful per-host visibility and lifecycle control than
one host per container. `--init` assists process reaping; it does not add
application health monitoring or guarantee graceful shutdown of every child.

Stop and remove it when finished:

```powershell
docker stop toolrill-manager
docker rm toolrill-manager
```

## 12. Optional: Client-Launched Stdio Container

For a client-owned process without published HTTP ports, keep the same shared
file and explicitly override incoming transport with the `stdio` command.
Diagnostics must remain disabled to keep stdout reserved for MCP. Outgoing
HTTP servers and per-host filters do not need to change.

For VS Code Project 1, using the built image and Docker Desktop:

```json
{
  "servers": {
    "toolrill-project-1": {
      "type": "stdio",
      "command": "docker",
      "args": [
        "run", "--rm", "-i", "--init",
        "--mount", "type=bind,source=C:/McpDocker/config,target=/config,readonly",
        "toolrill-customer:local", "stdio",
        "--host", "project-1", "--config", "/config/toolrill.json"
      ]
    }
  }
}
```

For Claude Code Project 2:

```powershell
Set-Location C:/Work/Project2
claude mcp add --transport stdio --scope project toolrill-project-2 -- docker run --rm -i --init --mount type=bind,source=C:/McpDocker/config,target=/config,readonly toolrill-customer:local stdio --host project-2 --config /config/toolrill.json
```

Use `project-3` for a third client and adapt the JSON wrapper for that client.
On native Linux, adapt paths and host networking as above. The Docker daemon
must be accessible from the client's actual execution environment.

Use `-i` for the MCP stdin stream, but **do not use `-t`**: allocating a TTY
can alter protocol output. Do not use `-d` for this client-owned stdio process,
and do not publish ports or run manager. Pre-build the image; building through
the client launch command can delay startup and contaminate protocol output.
Each client launches a separate container; `--rm` removes it after it exits.
Check for leftover running containers after an interrupted client session.

Access to the Docker daemon is a significant privilege. Client approval of a
`docker run` command is not a sandbox guarantee. Review the image, mounts,
and command, and do not mount the Docker socket or sensitive host directories
into ToolRill just to make MCP work.

## 13. Configuration Updates, Persistence, and Access Limits

The shared file mount is read-only inside the hosting containers. Edit
`C:/McpDocker/config/toolrill.json` on the host, then restart the affected service:

```powershell
Set-Location C:/McpDocker
docker compose restart project-2
```

Restart all affected services if changing shared diagnostics or multiple
profiles. Reconnect/refresh the clients afterward: configuration and tool
discovery are not live-reloaded. Changing Compose ports/environment/mounts
requires service recreation with `docker compose up -d`, not only a restart.
After rebuilding an image, recreate the services to use the new image.

No image rebuild is required for a base configuration edit, because it is
mounted. Back up and protect the host configuration directory; container
removal does not delete that bind-mounted data. Do not edit an ephemeral
copy under `/app` or install tools interactively inside a running container
and expect those changes to survive recreation.

An optional `/config/toolrill.Production.json` can override the base file, and
`TOOLRILL_` variables override those values. `DOTNET_ENVIRONMENT` defaults to
`Production`. For first setup, use just the base file. Array overrides are
numeric-index-based, not matched by host name; keep indices consistent.
For example, `TOOLRILL_hosts__1__outgoing__0__enabled=false` changes Project 2's
first outgoing server in this example. Each container receives its own
environment; do not assume host shell variables are injected automatically.

If you want to use `init` inside a container instead of writing the example
directly, run a separate one-off container with a **writable** config mount
and `init --config /config/toolrill.json`. It creates only the initial `default`
stdio profile and refuses existing targets. Populate your three hosts before
validation/startup, then return hosting mounts to read-only. `server add/remove`
also need write access and do not have tool-filter editing flags.

Containerization does not turn these profiles into user authorization rules.
Anyone able to reach Project 3's endpoint can request its unfiltered tools;
ToolRill does not authenticate a client's project or route by client identity.
The example's loopback publications reduce network exposure but do not block
other local clients from choosing another port. Container peers on the same
network may also reach listening services without host port publication.

Use independently controlled networks/endpoints and downstream data/credential
permissions if clients are untrusted. Do not publish broadly, run privileged
containers, add host networking, or mount entire drives as shortcuts for this
setup. Security hardening, inbound authentication and production availability
are not implemented by these examples. No measured throughput or concurrent
request capacity is claimed.

## 14. Troubleshooting and Manual Expectations

| Symptom | Likely cause or action |
| ------ | ------ |
| Root repository Compose file only starts one port (3001) | The repository's root `docker-compose.yml` is wired for a single-host demo (`DEP-001`); use the multi-service `compose.yaml` in Section 6 for three project hosts. |
| Package install fails or old CLI rejects `--host` | Package/version/source is unavailable or predates this feature; use a matching release or the source image. |
| Container cannot find `/config/toolrill.json` | Wrong/missing host directory or filename; check bind mount and container path. |
| HTTP port is published but client cannot connect | ToolRill bound container loopback, child failed, port conflicts, or discovery is incomplete; bind container `0.0.0.0`, inspect logs and client status. |
| Downstream connection to `localhost:5051` fails | It points to the gateway container; use host alias, service DNS or a remote reachable endpoint. |
| Host alias resolves but connection fails | Check downstream bind interface, host firewall, Docker networking and readiness. |
| Local tool does not resolve | Container working directory no longer contains/can discover `/tool`'s manifest. |
| Stdio project fails in the source runtime image | SDK/source/dependencies or writable build directories are missing; see Section 10. |
| A Windows path fails inside a container | Configure the mounted Linux path, not the host path. |
| Expected tool names/counts differ | Check actual raw names, filters, other client providers, schema compatibility, environment overrides and stale discovery. |
| Manager container runs but one endpoint is absent | Parent is alive while a child failed; inspect child output or use separate host containers. |
| Stdio client gets invalid protocol output | Remove TTY/detached flags; keep diagnostics off, avoid build output, and inspect stdout producers. |

For your own manual review:

1. Confirm the image contains the intended CLI version and the mounted unified file.
2. Confirm each downstream MCP server is reachable from the gateway network.
3. Validate configuration, start services, and inspect individual logs/status.
4. Connect Project 1 to `127.0.0.1:3101/mcp` and confirm only its three tools.
5. Connect Project 2 to `127.0.0.1:3102/mcp` and confirm the two-plus-eight selection.
6. Connect Project 3 to `127.0.0.1:3103/mcp` and confirm all 19 downstream tools.
7. Invoke one permitted, non-destructive tool through each client's approval flow.
8. Edit a filter, restart the affected host, reconnect its client and check discovery.
9. Stop the deployment and confirm the customer file remains on disk.

A running container, printed endpoint, successful ordinary HTTP GET, or configuration validation alone is not proof of a working MCP session. A downstream startup failure prevents that host from offering a partial connected set; investigate the real error rather than expecting automatic gateway recovery.

## References

- [Non-container customer setup](CUSTOMER_SETUP.md)
- [Unified host/server configuration](configuration.md)
- [CLI commands](cli.md)
- [Current container deployment limitations](containerization.md)
- [Docker port publishing](https://docs.docker.com/engine/network/port-publishing/)
- [Docker Desktop networking](https://docs.docker.com/desktop/features/networking/)
- [Docker Compose networking](https://docs.docker.com/compose/how-tos/networking/)
- [VS Code MCP configuration](https://code.visualstudio.com/docs/copilot/customization/mcp-servers)
- [Claude Code MCP setup](https://code.claude.com/docs/en/mcp)