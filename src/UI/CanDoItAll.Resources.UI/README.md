# CanDoItAll.Resources.UI

## Purpose

The complete Resources Registry and Browse renderer, including connector fields, promotion, receipt history and governed read-only preview. Production and sandbox mount these same components.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework: `net10.0`

```powershell
dotnet build src/UI/CanDoItAll.Resources.UI/CanDoItAll.Resources.UI.csproj --configuration ResourcesUiProof /m:1
```

## Dependencies

The evaluated dependency inputs are in [CanDoItAll.Resources.UI.csproj](CanDoItAll.Resources.UI.csproj).
The [Resources boundary receipt](../../../docs/architecture/resources-ui-boundary.md)
records the evaluated source-mode closure, validation and compatibility decisions.

## Architecture Notes

The surface receives typed workspace contracts and observes their explicit change events. It does not inject module owners. Browse uses the actual FileBrowser, FileInteraction and shared AppComponents host-action catalog. Preview is capped at 16 MiB with mode switching disabled. Scoped CSS stays beside its owning DOM.
