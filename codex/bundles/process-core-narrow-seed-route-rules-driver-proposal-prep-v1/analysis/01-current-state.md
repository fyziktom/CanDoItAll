# Current State Review

## Completed in previous bundle

The previous bundle reported:

- SB001-SB036 completed.
- Route DTO source interfaces removed.
- Dispatcher payload recovery is confined to `ProcessDispatchRouteModelAdapters`.
- Route handlers/services consume pure DTOs and adapter calls remain at named application edges.
- Finalizer route calls use route-owned input records and compatibility is isolated in `ProcessDispatchFinalizerAdapter`.
- Hydration was split into collaborators for artifact-input preparation, candidate assembly, direct-agent binding, recovery, and cooperation metadata.
- Pre-execution database/materialization facts are route-facing.
- Subprocess runtime consumes route-owned input and projection persistence is explicit.
- Direct-agent runtime consumes `ProcessDispatchDirectAgentExecutionInput`.
- `ProcessRouteExecutionOutcome` exposes a route run snapshot rather than full execution detail.
- Projection run snapshot and execution observation split was completed.
- Artifact expectation snapshots converged for validation/projection/satisfaction.
- Driver-readiness remains documentation-only.

## Critical proof state

The final red-team says the next step may be a **narrow Process Core proposal**, not a broad extraction.

The recommended first candidates are:

1. Route stage order and route eligibility descriptors.
2. Subprocess artifact source mapping and lifecycle status rules.
3. Artifact expectation snapshots and pure matching rules.

This bundle chooses the safest first candidate for production movement:

> Route stage order and route eligibility descriptors.

The subprocess and artifact candidates are included as rehearsal/contract-map phases, not as production Core moves, unless the route Core seed passes all gates and a later bundle explicitly authorizes them.

## Current blockers for broad Core

Still out of Core:

- EF / database / `DbContext`
- workspace, storage, filesystem
- AgentFramework execution
- claim lifecycle and heartbeat
- step transition execution
- finalizer application and mutation
- process-driver APIs and runtime helper dispatch
