# Release

ToolRill is configured as a .NET 10 tool, version `0.1.0`, with command
`toolrill` and Apache-2.0 package licensing.

## Prerequisites

- Install the .NET 10 SDK and work from the repository root.
- Confirm `RepositoryUrl` in [Directory.Build.props](../Directory.Build.props)
  is `https://github.com/sp-shalin/toolrill` before creating a release
  package. Confirm package metadata and version.
- Create/push the GitHub repository and manually invoke **CI** in its Actions
  tab. The [workflow](../.github/workflows/ci.yml) restores, builds Release,
  and runs tests on Ubuntu and Windows; review both jobs and their TRX
  artifacts before release. It never publishes packages.
- Confirm NuGet.org package ID availability/ownership and release approval.

## Pack And Inspect

Run manually:

```bash
dotnet pack src/ToolRill/ToolRill.csproj
```

For this `net10.0` project, packing defaults to Release and writes
`src/ToolRill/bin/Release/ToolRill.0.1.0.nupkg`. Open it with a ZIP viewer and
inspect its `.nuspec` for ID, version, description, Apache-2.0 license,
README reference, and the real repository URL. Confirm the root README,
tool settings, `ToolRill.dll`, runtime configuration, and required dependencies
are present under the package's tool payload. Reject placeholder metadata
or missing files.

Install into a disposable local tool directory and check the command:

```bash
dotnet tool install ToolRill --version 0.1.0 --tool-path ./artifacts/tool-check --add-source ./src/ToolRill/bin/Release
./artifacts/tool-check/toolrill --help
```

On PowerShell, invoke `./artifacts/tool-check/toolrill.exe --help`. Review the
installer's reported version and help output. A local install smoke check
does not replace the CI tests or manual gateway review. Remove the temporary
installation with:

```bash
dotnet tool uninstall ToolRill --tool-path ./artifacts/tool-check
```

## Publish Manually

Only after approval and local verification, publish the inspected package:

```bash
dotnet nuget push src/ToolRill/bin/Release/ToolRill.0.1.0.nupkg --source https://api.nuget.org/v3/index.json --api-key <NUGET_API_KEY>
```

Supply the key securely in your own terminal; never commit it. NuGet.org
versions are immutable: use a new version for subsequent releases. Confirm
the public listing, metadata, and installation from NuGet.org after indexing
before announcing availability.
