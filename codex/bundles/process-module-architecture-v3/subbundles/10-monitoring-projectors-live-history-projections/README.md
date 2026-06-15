# SB10 Monitoring Projectors, Live/History Snapshots, And Projection Contracts

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Implement event-first projection workers and projection contracts for current snapshots, history, run details, timeline, definition canvas, runtime canvas, artifact map, incidents, freshness, lag, replay, and dead letters.

## Why This Bundle Exists

The UI must preserve current live/history direction without using query-built runtime truth. This bundle creates the projection surface before UI rebuild.

## Covered Inputs

- REQ-026 through REQ-030.
- v3 UI projection inventory.

## Context Reset: Read These First

- SB08 and SB09 execution reports.
- `architecture/08-monitoring-events-snapshots-and-ui-projections.md`
- `architecture/12-runtime-persistence-event-store-and-outbox.md`
- `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md`

## Exact Source References

- `repo://codex/bundles/process-module-architecture-v3/architecture/08-monitoring-events-snapshots-and-ui-projections.md`
- `repo://codex/bundles/process-module-architecture-v3/architecture/15-ui-ux-projection-contracts-and-reuse-plan.md`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationCache.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`

## Source Evidence To Use

- `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationCache.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
- SB01 observation/UI archive.

## Prerequisites

- SB08 complete.
- SB09 event inputs stable.

## In Scope

- Projection worker infrastructure.
- Current snapshot projections.
- Historical projections.
- Run detail projection.
- Timeline projection.
- Definition canvas projection.
- Runtime canvas projection.
- Artifact map projection.
- Incident projection.
- Freshness/projector lag metadata.
- Replay and dead-letter handling.
- Live last-hour active-run semantics.

## Out Of Scope

- No Blazor UI rebuild.
- No template migration.
- No execution adapters.

## Target Projects / Files

- `src/CanDoItAll.Processes.Projections`
- `src/CanDoItAll.Processes.Persistence`
- `src/CanDoItAll.Processes.Application`
- projection tests.

## Deliverables

- Projection contracts and projectors.
- Projection query services.
- Replay/dead-letter support.
- Live/history correctness tests.

## Expected Deliverables

- UI can be rebuilt without runtime internals.
- Live last-hour query excludes stale completed events while active runs remain visible by explicit rule.
- Projection freshness and lag are visible.

## Dependency Impact

- SB13 UI rebuild depends on these projections.
- SB11 adapters can emit events/facets consumed by projections.

## Validation Depth

- Validate with projection replay tests, dead-letter tests, live last-hour tests, active-run inclusion tests, freshness/lag tests, restricted diagnostic tests, and projection review.

## Architecture Invariants That Must Hold

- Projectors do not mutate runtime state.
- Projection state is derived and rebuildable.
- UI reads projections only.
- Raw diagnostics remain restricted links.

## Implementation Steps

1. Define projection DTOs.
2. Implement projection workers and offsets.
3. Implement current snapshots.
4. Implement history/timeline/run detail projections.
5. Implement canvas/artifact/incident projections.
6. Implement replay/dead-letter behavior.
7. Add live/history tests.

## Refactoring Review Checkpoint

- Split projectors by projection family.
- Split query services from projector workers.
- Verify projection storage stays separate from runtime state.

## Required Tests / Proof

- Projection replay tests.
- Dead-letter tests.
- Live last-hour tests.
- Active-run inclusion tests.
- Projection freshness/lag tests.
- Restricted diagnostic link tests.

## Search Proof

- Search UI/application projections for old observation service usage.
- Search projectors for runtime state mutation.
- Search for direct runtime EF reads from UI.

## Stop And Report Conditions

- Stop if projection contracts are insufficient and UI work would require runtime internals.
- Stop if projectors need to mutate runtime state.
- Stop if time filtering cannot be tested at query/projection boundary.

## Do Not Do

- Do not rebuild `ProcessObservationService` as authoritative runtime truth.
- Do not expose raw diagnostics as normal projection text.
- Do not let UI drive projection writes.

## Acceptance Checklist

- [ ] Projection contracts exist.
- [ ] Current and historical projections exist.
- [ ] Replay/dead-letter tests pass.
- [ ] Live/history tests pass.
- [ ] UI-ready query services exist.

## Proof Required

- Test output.
- Projection replay proof.
- Live/history proof.
- UI projection review.

## Browser Validation Logging

- Browser validation is deferred to SB13 because this bundle has projection contracts but no final UI behavior.

## Progression Gate

- SB13 cannot start until projection contracts support required UI surfaces.

## Suggested Agent Prompt

Execute SB10 from `codex/bundles/process-module-architecture-v3/subbundles/10-monitoring-projectors-live-history-projections`. Build event-first projections and live/history contracts. Do not query runtime internals from UI.

## Handoff Notes For Next Bundle

Record projection DTOs, query services, freshness semantics, test fixtures, and gaps for SB11/SB13.
