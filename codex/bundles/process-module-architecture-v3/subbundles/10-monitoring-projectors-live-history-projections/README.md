# SB10 Monitoring Projectors, Live/History Snapshots, And Projection Contracts

## Status

Completed on 2026-06-15.

## Execution Notes

SB10 implemented event-first projection workers, live/history/detail projection read models, replay/dead-letter handling, projection-history persistence, source-generated projection serialization, and UI-ready projection query services. Browser validation remains deferred to SB13 because SB10 does not ship a visible UI surface.

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
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationCache.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`

## Source Evidence To Use

- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationCache.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
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

## Performance Antipattern Notes

- Read `architecture/19-dotnet-performance-guardrails.md` and `validation/05-dotnet-performance-antipattern-checklist.md` before creating or modifying C# hot-path code.
- Record exact performance scan counts in the execution report when this subbundle changes runtime, dispatcher, manager, projection, template, Git, adapter, persistence, or UI service code.
- Do not introduce sync-over-async, unbounded event/projector queues, per-call `HttpClient`, per-call `JsonSerializerOptions`, load-all UI queries, or LINQ-heavy hot paths without a recorded mitigation and proof.
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

- [x] Projection contracts exist.
- [x] Current and historical projections exist.
- [x] Replay/dead-letter tests pass.
- [x] Live/history tests pass.
- [x] UI-ready query services exist.

## Proof Required

- Test output.
- Projection replay proof.
- Live/history proof.
- UI projection review.

## Recorded Proof

- `repo://codex/bundles/process-module-architecture-v3/proof/SB10/manifest.md`
- `repo://codex/bundles/process-module-architecture-v3/proof/SB10/semantic-invariants.md`
- `repo://codex/bundles/process-module-architecture-v3/proof/SB10/projection-review.md`
- `repo://codex/bundles/process-module-architecture-v3/proof/SB10/test-unit-sb10.txt`
- `repo://codex/bundles/process-module-architecture-v3/proof/SB10/test-unit-sb10-process-slice.txt`
- `repo://codex/bundles/process-module-architecture-v3/proof/SB10/build-unit-sb10.txt`
- `repo://codex/bundles/process-module-architecture-v3/proof/SB10/build-solution-sb10.txt`
- `repo://codex/bundles/process-module-architecture-v3/proof/SB10/codeanalytics-snapshot-summary.txt`

## Browser Validation Logging

- Browser validation is deferred to SB13 because this bundle has projection contracts but no final UI behavior.

## Progression Gate

- Passed for SB13 prerequisites. Projection contracts now support the required live/history/detail/canvas/artifact/freshness UI surfaces; SB13 still owns visible UI composition and browser proof.

## Suggested Agent Prompt

Execute SB10 from `codex/bundles/process-module-architecture-v3/subbundles/10-monitoring-projectors-live-history-projections`. Build event-first projections and live/history contracts. Do not query runtime internals from UI.

## Handoff Notes For Next Bundle

SB11 can emit additional event/facet metadata into the runtime stream. SB13 should consume `ProcessRuntimeProjectionQueryService`, `ProcessLiveProcessSnapshot`, `ProcessRunDetailProjection`, `ProcessTimelineEventProjection`, runtime canvas models, artifact map models, and `ProcessProjectionFreshness` rather than runtime internals.
