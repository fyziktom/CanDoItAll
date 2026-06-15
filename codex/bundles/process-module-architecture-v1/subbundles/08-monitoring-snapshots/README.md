# SB08 Monitoring And Snapshots

## Status

Planned.

## Objective

Build event-first monitoring, observer/subscriber infrastructure, current/live snapshot cache, historical projections, and UI-friendly read models.

## Covered Inputs

- REQ-026 through REQ-030

## Prerequisites

- SB05 complete.
- SB07 complete.

## Exact Source References

- `bundle://architecture/01-target-solution.md`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationCache.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`

## Deliverables

- Runtime event store.
- Observer/subscriber pipeline.
- Snapshot projector services.
- Live snapshot cache.
- Historical projections.
- Time-range filtered query APIs.
- UI read models.

## Dependency Impact

- UI rebuild depends on stable projections.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Persist typed runtime events.
2. Add asynchronous projection consumers.
3. Build current process snapshot.
4. Build live dashboard snapshot.
5. Build run history and timeline projections.
6. Implement cache invalidation and explicit refresh.
7. Implement time-range filtering by event timestamp.

## Scope Exceptions

No full UI rebuild in this subbundle.

## Do Not Do

- Do not query runtime internals from UI.
- Do not let projections block runtime transitions.
- Do not include events outside selected history range unless explicitly requested.

## Acceptance Checklist

- Live Hour excludes older historical events.
- Explicit history range includes older events.
- Runtime still progresses when projection consumer is delayed.
- Snapshot cache returns latest projection without full reload.

## Proof Required

- Projection tests.
- Delayed observer non-blocking test.
- Time range negative tests.
- Semantic Adequacy Gate.
- `proof/SB08/manifest.md`.
- Production Behavior Artifact Matrix for event records and snapshot records.

## Browser Validation Logging

- Plan Playwright validation for Live Processes after SB09 consumes these projections.

## Progression Gate

- SB09 cannot start UI wiring until read models are stable.

## Suggested Agent Prompt

Build monitoring as an event and projection system. Live/history views must never drive runtime execution.
