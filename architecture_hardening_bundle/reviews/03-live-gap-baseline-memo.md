# Live gap baseline memo

## Status

- `Completed`
- Captured on `2026-04-13` after prepared-stage validator repair and fresh targeted test proof on the live repository.

## Snapshot and repository grounding

- CodeAnalytics snapshot used for architecture inventory: `snap-20260411110915-071aa37c`.
- `CanDoItAll.Modules.Processes` is a product project referenced by `CanDoItAll.Composition`, `CanDoItAll.Mcp.Processes`, `CanDoItAll.Modules.Workbench`, `CanDoItAll.ScenarioSeeder`, and `CanDoItAll.Web`.
- `ProcessesService` remains a large aggregation point, and the live code still spreads behavior across `Persistence`, `Publication`, `Runtime`, `Reads`, and `Support` partials.

## Fresh proof

- Prepared-stage validator:
  - `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\architecture_hardening_bundle --profile initiative --stage prepared`
  - Result: passed.
- Integration baseline:
  - `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessImportMetadataIntegrationTests|FullyQualifiedName~ProcessDeletionIntegrationTests|FullyQualifiedName~SqliteWriteCoordinationIntegrationTests" -v:minimal`
  - Result: 15 passed, 0 failed.
- Component baseline:
  - `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests|FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessStepEditorFormTests" -v:minimal`
  - Result: 19 passed, 0 failed.
- MCP baseline:
  - `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\CanDoItAll.Mcp.Processes.Tests.csproj --filter "FullyQualifiedName~ProcessesToolsTests|FullyQualifiedName~ProcessTemplateProjectionServiceTests|FullyQualifiedName~ProcessTemplateCatalogServiceTests|FullyQualifiedName~ProcessTemplatePackLoaderTests|FullyQualifiedName~ProcessTemplateMermaidExporterTests" -v:minimal`
  - Result: 19 passed, 0 failed.

## Current behavior already protected

| Risk area | Existing proof | What it currently protects |
| --- | --- | --- |
| Save, publish, and runtime flow behavior | `ProcessesServiceIntegrationTests` at lines 293, 404, 438, 475, and 493 | Branch routing, publish-clone preservation of canvas positions and artifact inputs, dependency gating, and structural validation failures. |
| Import, deletion, and write coordination | `ProcessImportMetadataIntegrationTests`, `ProcessDeletionIntegrationTests`, and `SqliteWriteCoordinationIntegrationTests` | Import provenance persistence, full graph deletion cleanup, and concurrent SQLite write coordination for adjacent infrastructure services. |
| Workspace and canvas behavior | `ProcessWorkspaceTests` at lines 35, 140, 552, and 765 plus `ProcessCanvasSurfaceFactoryTests` and `ProcessStepEditorFormTests` | Shell containment, canvas dependency editing, coalesced persistence, and template-driven editor interactions. |
| MCP contract behavior | `ProcessesToolsTests` at lines 12, 73, and 88 plus template projection/catalog/exporter tests | Structured save/publish/transition tool responses and template projection compatibility. |

## Live gaps still open

- Canonical dependency representation is not directly asserted end-to-end.
  Existing tests cover branch routing and editor link behavior, but they do not prove one canonical dependency model across authoring, persistence, publish clone, read-side projection, and runtime execution.
- Validation purity is currently violated in production code.
  `src/CanDoItAll.Modules.Processes/ProcessesService.Support.cs:11-12` shows `ValidateDefinitionEditor` calling `ProcessCanvasBranching.NormalizeDefinitionEditor(model)`, which mutates the editor during validation.
- Differential persistence is currently destructive.
  `src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs:125` enters `SaveDefinitionChildrenAsync`, and lines `139-146` remove role, step, dependency, artifact, and branch child rows before rebuilding them.
- Provider-agnostic optimistic concurrency is not evident in the module source.
  A source search across `src/CanDoItAll.Modules.Processes/*.cs` found no `RowVersion`, `Concurrency`, `Timestamp`, or version-token usage tied to Process definition or runtime aggregates.
- The main service and the baseline test class remain maintainability hotspots.
  The current snapshot reports `ProcessesService.cs` as a large file, `ProcessesService.Runtime.cs` as a large file, and `ProcessesServiceIntegrationTests.cs` as a large test hotspot.

## Baseline decision

- No new characterization tests were added in subbundle `01`.
- Reason:
  - the current targeted suites already protect the user-visible behaviors that later refactors must not regress,
  - the highest-risk uncovered items are architectural invariants that later subbundles are expected to change, not preserve as-is,
  - adding tests that freeze the current destructive save or mutation-based validation behavior would harden the wrong contract.
- Downstream requirement:
  - subbundles `02-06` must add or reshape tests only where they introduce a better contract, such as canonical dependency ownership, pure validation boundaries, optimistic concurrency, and stable child identity.

## Progression recommendation

- Subbundle `02-canonical-dependency-model-and-compatibility-boundary` may start.
- It must treat this memo as the baseline truth:
  - preserve the currently protected surface behaviors,
  - do not preserve mutation-in-validation or destructive child recreation,
  - add focused tests only when the new canonical boundary becomes explicit.
