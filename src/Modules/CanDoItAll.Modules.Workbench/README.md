# CanDoItAll.Modules.Workbench

## Purpose

Product module for workbench views, projections, canvas state, and user workspace orchestration.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj
```

## References

Project references:

- `../CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj`
- `../CanDoItAll.Components.CanvasLib/CanDoItAll.Components.CanvasLib.csproj`
- `../CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj`
- `../CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj`
- `../CanDoItAll.Modules.Factory/CanDoItAll.Modules.Factory.csproj`
- `../CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`
- `../CanDoItAll.Modules.Projects/CanDoItAll.Modules.Projects.csproj`
- `../CanDoItAll.Modules.Resources/CanDoItAll.Modules.Resources.csproj`
- `../CanDoItAll.Modules.TestLab/CanDoItAll.Modules.TestLab.csproj`

Framework references:

- None

Direct package references:

- `Microsoft.AspNetCore.Components.Web (10.0.5)`

## Architecture Notes

This module owns product semantics for its bounded area. Keep business behavior here and expose it through typed services, Razor components, and module contracts. MCP projects should call into these services instead of duplicating module logic.

`ProjectStructureProcessRunFolderProjectionPolicy` owns process-run folder projection. It projects current-run managed roots, collapses artifact evidence under `artifacts/.../process-runs/{runId}` to the run artifact folder, collapses generated or external-delivery output persisted under `output/.../process-runs/{runId}/{productRoot}` to the product folder, and ignores wrong-run, dated receipt, absolute, traversal, or otherwise unanchored paths instead of mirroring noisy artifact subtrees. Raw `external-target/...` aliases remain Processes grounding metadata; Workbench projects the managed output root that records the run-owned delivery evidence.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
