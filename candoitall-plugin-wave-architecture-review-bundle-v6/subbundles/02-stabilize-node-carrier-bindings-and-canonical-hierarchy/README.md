# 02-stabilize-node-carrier-bindings-and-canonical-hierarchy

## Status

- `Completed`

## Objective

Keep node as the universal carrier, but reduce the carrier to durable project semantics and move foreign bindings out of it.

## Covered Inputs

- `PW6-002`
- `PW6-006`
- `PW6-007`

## Prerequisites

- SB01 complete or explicitly trusted.
- Decide the minimal carrier contract that must remain canonical: identity, kind, text, hierarchy, X/Y, markers, schedule anchors, timestamps.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchMetadata.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureInvariantService.cs`

## Evidence Focus

- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:26-59`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:483-500`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:646-655`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:223-246; 290-330; 391; 476`

## Deliverables

- Stable canonical node carrier model or renamed ProjectNodeRecord.
- Typed binding tables for artifact/resource/provider/storage and similar foreign references.
- Canonical hierarchy represented once, not both as ParentNodeKey and generic relation rows.

## Dependency Impact

- Makes the node model safe to evolve while preserving the mindmap-first workflow.
- Prevents plugin-specific payload from bloating the carrier and metadata envelope.

## Validation Depth

- Schema and mapping review.
- Migration tests proving legacy metadata foreign refs are preserved or migrated safely.
- Relation tests proving semantic edges still work after hierarchy duplication is removed.

## Implementation Steps

- Freeze the carrier contract and document which fields stay canonical.
- Move foreign ids and integration payloads into typed binding or facet tables keyed by node id.
- Remove hierarchy-like generic links from create/reparent flows so containment lives in one canonical place.

## Do Not Do

- Do not demote X/Y or semantic markers into cosmetic view state.
- Do not invent a second hidden metadata bucket for new foreign ids.

## Acceptance Checklist

- [ ] Carrier row contains only stable node semantics plus spatial and scheduling meaning.
- [ ] Create/reparent flows no longer persist Contains/BelongsTo duplicates.
- [ ] Metadata no longer introduces new foreign ownership ids.

## Proof Required

- Schema diff or migration proof.
- Focused tests for hierarchy and metadata/binding discipline.
- Updated documentation of the carrier contract.

## Browser Validation Logging

- Capture structure editing after hierarchy migration if the canvas behavior changes.

## Progression Gate

- Do not start SB03 until the carrier contract and hierarchy truth are stable.

## Suggested Agent Prompt

Implement SB02 while preserving node as the universal carrier. Keep X/Y and markers canonical. Move foreign bindings out of the carrier and metadata. Make hierarchy canonical only once.
