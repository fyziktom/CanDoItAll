# Source Artifacts

## Primary Code Artifact

- `CanDoItAll-canonical-model-refactor` (uploaded zip extracted under `/mnt/data/unpacked_current`)

## Prior Bundle / Historical Context

- `candoitall-canonical-architecture-review-bundle-v4/` inside the repository root
- `architecture/adrs/ADR-0001..0004`
- `architecture/reviews/2026-04-04-*`

## Important Reviewed Areas

- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureInvariantService.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectNodeScopeBridge.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureNodeEditor.cs`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs`
- `src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs`
- `src/CanDoItAll.Modules.Resources/ResourceModels.cs`
- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs`
- `src/CanDoItAll.Modules.Workspace/WorkspaceModuleServiceCollectionExtensions.cs`
- `src/CanDoItAll.Modules.Workspace/ProviderExecution.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`

## Honest Validation Limitation

- `dotnet` is not installed in this container, so `dotnet build/test/run` could not be executed here.
- This bundle is therefore a **deep static architecture review plus evidence mapping**, not a claimed runtime validation pass.
