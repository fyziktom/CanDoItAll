# CanDoItAll.Modules.Resources.Presentation

## Purpose

Registry and Browse state policy shared by the production Resources host and standalone scenario host. Controllers call typed owner ports and contain no production service registration or database access.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework: `net10.0`

```powershell
dotnet build src/Modules/CanDoItAll.Modules.Resources.Presentation/CanDoItAll.Modules.Resources.Presentation.csproj --configuration ResourcesUiProof /m:1
```

## Dependencies

The evaluated dependency inputs are in [CanDoItAll.Modules.Resources.Presentation.csproj](CanDoItAll.Modules.Resources.Presentation.csproj).
The [Resources boundary receipt](../../../docs/architecture/resources-ui-boundary.md)
records the evaluated source-mode closure, validation and compatibility decisions.

## Architecture Notes

Registry owns stable drafts/EditContext, field versions, captured commands and target-labelled receipts. Browse separates catalog, source, preview, promotion and file-action lifetimes. Accepted writes retain their original receipt after navigation; stale reads and leases cannot replace successors. Unknown outcomes require exact read-only review, never automatic replay.
