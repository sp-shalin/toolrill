# Contributing

## Build and Test

Install the .NET 10 SDK, then run from the repository root:

```bash
dotnet restore toolrill.slnx
dotnet build toolrill.slnx --no-restore
dotnet test toolrill.slnx
```

See the [testing guide](docs/testing.md) for suite details.

## Coding Style

Follow nearby C# conventions. `TreatWarningsAsErrors` is enabled; keep builds
warning-free. Keep changes focused, avoid unrelated formatting or refactoring,
and update relevant tests and documentation when behavior changes.

## Scope

Please open an issue before implementing new features not marked `Planned` in
the [roadmap](docs/roadmap.md). Reference the roadmap ID when applicable and
agree on scope before starting.

To keep the core `ToolRill` CLI and gateway lightweight, built-in authentication
providers, RBAC engines, secret-manager SDKs, and UI dashboards are out of scope
for the core repository. These integrations should instead use the `PLUG-001`
extensibility interfaces described in the roadmap. `PLUG-001` is currently
`Deferred`; discuss the required interfaces before implementing an integration.

Use the bug and feature issue templates for public reports. Report security
vulnerabilities privately as described in [SECURITY.md](SECURITY.md).