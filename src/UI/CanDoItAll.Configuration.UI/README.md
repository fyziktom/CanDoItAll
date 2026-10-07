# CanDoItAll.Configuration.UI

## Purpose

Shared connector fields and the actual neutral schema loop for Resources and Workspace
configuration hosts. It uses the existing SharedKernel schema/state and BaseLib controls.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework: `net10.0`

```powershell
dotnet build src/UI/CanDoItAll.Configuration.UI/CanDoItAll.Configuration.UI.csproj --configuration ResourcesUiProof /m:1
```

## Dependencies

The evaluated dependency inputs are in [CanDoItAll.Configuration.UI.csproj](CanDoItAll.Configuration.UI.csproj).
The [Resources boundary receipt](../../../docs/architecture/resources-ui-boundary.md)
records the evaluated source-mode closure, validation and compatibility decisions.

## Architecture Notes

ConfigurationInputDraft retains unblurred raw text and per-field edit versions while owner validation uses the canonical configuration state. Secret options contain only IDs and names. Unknown references stay visible. Callers that need draft continuity across remounts own and pass the draft; this library never loads secret values or executes connectors.

`ConfigurationSchemaRenderer` owns ordinary field rendering and encoded field-level issues.
Workspace retains only the reference projection and trusted renderer resolution. The
[independent sandbox](../../Sandboxes/CanDoItAll.Configuration.UiSandbox/README.md) loads no
renderer registry or arbitrary component types.
