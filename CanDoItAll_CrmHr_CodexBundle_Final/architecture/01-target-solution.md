# Target Solution

## Architectural Direction

- Add `CanDoItAll.Modules.CrmHr` as a new module with explicit registration in the existing composition pipeline.
- Model shared identity through a Party root while keeping CRM, HR, AI-agent, and project assignment slices as contextual extensions.
- Treat project/workbench participants as projections that may point at central parties, while still allowing honest project-local-only actors.

## Live-Repo Contract Adjustments

- Project and node dependency decisions must align with `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureDependencyAnalysis.cs` and the checklist/read APIs already shipped in Workbench and the MCP layer.
- New files or media must flow through `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Storage\Placement\StoragePlacementService.cs` and `StorageObjectReference` instead of bypassing current storage drivers.
- AI-agent identity must stay separated from Workspace runtime provider profiles while being linkable to them.
- Project, meeting, and work-item assignment extensions must preserve existing metadata envelopes and editor/create flows rather than inventing a second assignment pipeline.

## Shipping Boundary

- CRM/HR pages stay outside CanvasLib and use BaseLib-first composition.
- Shared domain and integration logic belong in the CRM/HR module and existing services, not in page lifecycle code.
- Schema, migrations, tests, and browser proof are part of the feature contract, not optional follow-up work.
