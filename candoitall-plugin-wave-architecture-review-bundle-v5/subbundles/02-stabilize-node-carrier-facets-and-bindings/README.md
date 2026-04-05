# 02-stabilize-node-carrier-facets-and-bindings

## Status

- `Completed`

## Objective

Keep node as the universal carrier, but slim the carrier so it only stores stable node semantics, spatial meaning, and scheduling anchors while facets and bindings hold specialized concerns.

## Covered Inputs

- `PWA-002`
- `PWA-005`
- `R-002`
- `R-003`

## Prerequisites

- SB01 complete or projection writes quarantined.
- Agree the list of carrier-owned fields versus facet-owned fields before migration starts.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchMetadata.cs`

## Evidence Focus

- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:26-59`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:165-447`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:613-648`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:113-131`

## Deliverables

- A slim ProjectNode carrier record/table.
- Explicit binding tables for external artifacts/resources/providers/accounts.
- Typed facets or an equivalent governed facet model for meetings, participants, work items, repositories, environments, infrastructure, and future plugins.
- Metadata validation that rejects hidden foreign-owner fields.

## Dependency Impact

- Creates the stable base required by SB03 lifecycle work.
- Prevents plugin work from leaking connector state into ad-hoc metadata.

## Validation Depth

- Persistence schema review.
- Round-trip tests for existing node kinds.
- Migration tests proving X/Y and marker semantics are preserved.
- Negative tests for forbidden metadata ownership leakage.

## Implementation Steps

- Define the carrier contract: identity, project scope, parent relation, textual meaning, node kind key, X/Y, semantic markers, schedule anchors, and minimal status/progress.
- Move route, external artifact identifiers, media/storage references, and other foreign-owner references into binding/facet persistence.
- Introduce a transitional adapter so existing UI and MCP contracts can keep using the current surface DTO while internals migrate.
- Tighten metadata validation to a bounded, node-local descriptive scope.

## Do Not Do

- Do not demote X/Y or semantic markers to ephemeral UI state.
- Do not replace one giant metadata blob with another giant facet blob without ownership rules.

## Acceptance Checklist

- [x] Carrier persistence no longer stores route plus artifact plus media plus storage plus provider references in the same row.
- [x] X/Y and marker meaning survive migration unchanged.
- [x] Foreign-owner ids are expressed only through explicit bindings/facets.

## Proof Required

- Schema diff.
- Migration tests.
- Architecture note that enumerates carrier-owned vs facet-owned fields.

## Completion Notes

- Added `Workbench_ProjectNodeBindings` and `Workbench_ProjectNodeReferences` as the explicit persistence boundary for route, artifact/media/storage payload, and foreign-owner references.
- Introduced `ProjectNodeBindingStorage` as the migration-safe adapter that normalizes legacy carrier rows, persists binding/reference ownership, and rehydrates the existing `ProjectStructureNode` surface DTO for callers.
- `ProjectWorkbenchService` write flows now persist sanitized carrier rows and explicit bindings/references instead of keeping foreign-owned payload in the canonical carrier row.

## Architecture Resolution

- The carrier remains the universal node, but its durable meaning is now constrained to node semantics, hierarchy, schedule anchors, spatial placement, progress, and markers.
- External navigation, artifact/media/storage payload, and cross-module foreign-owner ids are no longer canonical carrier state; they are persisted through explicit binding/reference rows.
- Metadata remains descriptive and node-local after sanitization. Foreign-owner ids still round-trip through the public surface DTO, but only as hydrated projection data backed by binding/reference persistence.

## Proof Produced

- Schema diff proof is in `src/CanDoItAll.Migrations.Sqlite/Migrations/20260405024055_AddProjectNodeBindings.cs` and `src/CanDoItAll.Migrations.PostgreSql/Migrations/20260405024129_AddProjectNodeBindings.cs`.
- Runtime proof: `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests|FullyQualifiedName~ProjectWorkbenchSubtreeRecompositionIntegrationTests|FullyQualifiedName~ProjectStructureAgentIntegrationTests|FullyQualifiedName~ProjectStructureAgentApiIntegrationTests"` passed with `40/40` tests.
- Added integration coverage proving uploaded-file bindings persist outside the carrier row, transcript provider references are stored in explicit reference rows, schema repair recreates the new binding tables, and legacy carrier rows normalize without changing `X/Y` or marker semantics.
- Architecture ownership note is updated in `architecture/02-node-carrier-and-facet-model.md`.

## Browser Validation Logging

- Capture structure editing flows if node edit forms change.

## Progression Gate

- Do not start SB03 until the carrier/facet boundary is explicit and migration-safe.

## Suggested Agent Prompt

Implement SB02 without changing the product story that node is the universal carrier. Split the current ProjectObjectRecord into a stable carrier plus explicit facets/bindings, preserve X/Y and marker semantics, add metadata guardrails, and keep current surface DTOs compatible.
