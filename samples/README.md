# Downstream Server Samples

These small MCP servers demonstrate the two downstream transports supported
by ToolRill. They are tool providers, not alternative gateway implementations.

| Project | Transport | Tools | Lifecycle |
| --- | --- | --- | --- |
| [StdioServer](StdioServer/Program.cs) | stdio | Hello and Goodbye methods; the greeting can be configured through `Greeting`. | ToolRill launches a child process. |
| [HttpServer](HttpServer/Program.cs) | Streamable HTTP at `/mcp` | Hello method returning an HTTP-server greeting. | Start separately before connecting ToolRill. |

The stdio sample also supports automated integration tests and the SDK Docker
example. Keep its behavior stable when changing it. The HTTP sample is a
standalone learning example; automated HTTP tests use a separate test fixture.
Neither sample is a production service or an authentication example.

## Run Both Through ToolRill

Install the .NET 10 SDK. Run these PowerShell commands from the repository root.
No globally installed ToolRill package is needed.

In a first terminal, start the HTTP downstream on a free loopback port:

```powershell
dotnet run --project samples/HttpServer --no-launch-profile -- --urls http://127.0.0.1:5051
```

Keep that terminal open. In a second terminal at the repository root, create a
separate demo configuration so the repository's existing settings are unchanged:

```powershell
New-Item -ItemType Directory -Force artifacts/samples-demo
dotnet run --project src/ToolRill -- init --config artifacts/samples-demo/toolrill.json
dotnet run --project src/ToolRill -- server add local --transport stdio --project samples/StdioServer/StdioServer.csproj --config artifacts/samples-demo/toolrill.json
dotnet run --project src/ToolRill -- server add remote --transport streamable-http --url http://127.0.0.1:5051/mcp --config artifacts/samples-demo/toolrill.json
dotnet run --project src/ToolRill -- config validate --config artifacts/samples-demo/toolrill.json
dotnet run --project src/ToolRill -- server inspect local --config artifacts/samples-demo/toolrill.json
dotnet run --project src/ToolRill -- server inspect remote --config artifacts/samples-demo/toolrill.json
dotnet run --project src/ToolRill -- start --mode serve-http --config artifacts/samples-demo/toolrill.json
```

`init` refuses to overwrite an existing file. On subsequent runs, reuse the
configuration and skip initialization and server additions. Relative project
paths require the gateway commands to run from the repository root.

Connect your MCP client to `http://127.0.0.1:3001/mcp`. Use another port with
`--url http://127.0.0.1:3002` on the final command if needed, and update the
client URL accordingly.
The gateway exposes three downstream tools with `local/` or `remote/` prefixes;
use the exact discovered tool names rather than assuming method-name casing.
Calling the local greeting returns `Hello from Server A!`; the remote greeting
returns `Hello from HttpServer over Streamable HTTP!`.

For a filtering exercise, stop the gateway, edit the local outgoing entry's
`tools.include` list to contain only its discovered greeting-tool name, then
restart and refresh your client. Only that local tool and the remote tool
should remain exposed. See the [filter configuration](../docs/configuration.md).

Press Ctrl+C in each terminal to stop the gateway and HTTP sample. The gateway
owns its stdio child; the separately started HTTP server has its own lifecycle.

## Further Reading

- [CLI commands](../docs/cli.md)
- [Docker examples, including the bundled stdio sample](../docs/containerization.md)
- [Multi-project scenario guide](../docs/CUSTOMER_SETUP.md)
- [Multi-project Docker scenario guide](../docs/CUSTOMER_DOCKER_SETUP.md)