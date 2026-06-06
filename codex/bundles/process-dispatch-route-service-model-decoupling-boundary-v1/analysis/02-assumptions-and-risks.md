# Assumptions And Risks

## Assumptions

- The branch is `maf-processes-refactor`.
- The goal remains incremental decomposition before Process Core.
- Route behavior is already covered by focused route tests and integration tests from the previous bundle.
- Route handlers are top-level module-local classes, but route models and service implementations are still too coupled to dispatcher internals.

## Critical Path Risks

1. **Shallow model wrappers**
   - Codex could create route snapshot records but leave all handlers/facets consuming dispatcher aliases.
   - Mitigation: source scans must reject nested dispatcher aliases in route handlers/facets/services except explicitly named adapter files.

2. **All-facet service resurrection**
   - Codex could keep `ProcessDispatchRouteServices` as the only real dependency and claim the work is done.
   - Mitigation: factory must accept a route facet set or explicit facet implementations, not one all-facet service.

3. **Route order drift**
   - Any change to route handler composition can silently change behavior.
   - Mitigation: route order matrix and architecture test must verify exact canonical order.

4. **Hidden behavior loss**
   - Route handlers cover side-effectful behavior: blocking transitions, materialization requests, subprocess projection, workflow observation, direct-agent execution, finalizer handoff.
   - Mitigation: each route stage must preserve a specific parity test or focused regression assertion.

5. **Premature Core/driver extraction**
   - Creating public Core or production driver APIs before stabilizing route models would freeze bad abstractions.
   - Mitigation: source scans forbid `CanDoItAll.Processes.Core`, `IProcessDriverPack`, `ProcessDriverRegistry`, `DriverPack`.

## Validation Risks

- Full broad architecture tests may still include unrelated historical bundle fixture failures. The bundle must document them separately and run focused scoped tests.
- Build success is not enough. Route order, no dispatcher alias use, and no all-facet service dependency must be source-scanned.
- UI proof is irrelevant and must remain N/A.

## Reopen Triggers

Reopen earlier subbundles if:
- route order changes,
- any route handler gets `ProcessRunAutomationDispatchService` in its constructor,
- route facets expose dispatcher nested aliases after the migration phase,
- `ProcessDispatchRouteServices` remains the only implementation of multiple unrelated route facets,
- any route handler directly calls dispatcher methods instead of its facet,
- any route transition/finalizer/claim behavior loses test coverage,
- Process Core or production driver API tokens appear.
