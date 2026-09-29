# CanDoItAll.Resources.UiSandbox

## Purpose

A directly runnable, database-free Resources development host. It mounts the production renderer and presentation controllers with bounded in-memory owner implementations.

## Project Type

- SDK: `Microsoft.NET.Sdk.Web`
- Target framework: `net10.0`

```powershell
dotnet build src/Sandboxes/CanDoItAll.Resources.UiSandbox/CanDoItAll.Resources.UiSandbox.csproj --configuration ResourcesUiProof /m:1
```

## Dependencies

The evaluated dependency inputs are in [CanDoItAll.Resources.UiSandbox.csproj](CanDoItAll.Resources.UiSandbox.csproj).
The [Resources boundary receipt](../../../docs/architecture/resources-ui-boundary.md)
records the evaluated source-mode closure, validation and compatibility decisions.

## Architecture Notes

The scenario selector covers representative, empty, large, unavailable/retired references, missing connectors, invalid fields, failed reads, refused/unknown/committed-warning writes and denied/unavailable effects. Select a lane and hold the next operation to reproduce races, including lease release. Reset retires the original store; accepted writes still belong to it. FTP/IPFS labels are synthetic. Local launch records intent; downloads stream only fixture text.

## Development loop

```powershell
dotnet watch --non-interactive --project src/Sandboxes/CanDoItAll.Resources.UiSandbox --configuration ResourcesUiProof --no-launch-profile -- --urls http://127.0.0.1:5097
```

Choose an unused task-owned loopback port. No production database configuration is required.
The host links the existing generated Web `wwwroot/css/output.css` as content, with no Web
project reference. Regenerate that authoritative theme through the repository frontend
build if it is missing or changed. BaseLib fonts, FileTools assets and scoped styles are
served through static web assets in source and published builds. The source-mode graph
has 20 projects, 2 packages and no native assets on the recorded sibling revisions.
