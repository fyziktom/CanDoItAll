# 02-stabilize-node-carrier-facets-and-bindings

## Status

- `Prepared for Codex execution`

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

- `/mnt/data/unpacked_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `/mnt/data/unpacked_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs`

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

- [ ] Carrier persistence no longer stores route plus artifact plus media plus storage plus provider references in the same row.
- [ ] X/Y and marker meaning survive migration unchanged.
- [ ] Foreign-owner ids are expressed only through explicit bindings/facets.

## Proof Required

- Schema diff.
- Migration tests.
- Architecture note that enumerates carrier-owned vs facet-owned fields.

## Browser Validation Logging

- Capture structure editing flows if node edit forms change.

## Progression Gate

- Do not start SB03 until the carrier/facet boundary is explicit and migration-safe.

## Suggested Agent Prompt

Implement SB02 without changing the product story that node is the universal carrier. Split the current ProjectObjectRecord into a stable carrier plus explicit facets/bindings, preserve X/Y and marker semantics, add metadata guardrails, and keep current surface DTOs compatible.
