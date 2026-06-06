# Current State Analysis

## Previous bundle status

The previous route-handler facet implementation bundle appears completed in scope.

Observed branch evidence:
- `codex/bundles/process-dispatch-route-handler-facet-implementation-boundary-v1/reviews/01-execution-report.md` reports `Status: Completed`.
- SB001-SB144 are represented as individual execution-report rows.
- Source assertions say the dispatcher route partial no longer contains private nested route handler classes and route handler constructors do not receive `ProcessRunAutomationDispatchService dispatcher` parameters.
- `ProcessDispatchRouteHandlers.cs` contains top-level module-local route handler classes.
- `ProcessDispatchRouteHandlerFactory.cs` creates handlers in canonical route order.
- No Process Core, production driver API, UI, or mobile proof drift is reported.

## Remaining architectural issues

### 1. Route facets still expose dispatcher-owned nested models

`ProcessDispatchRouteFacets.cs` still contains aliases such as:

```csharp
using DispatchCandidate = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchCandidate;
using DispatchExecutionOutcome = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchExecutionOutcome;
using ProcessStepDispatchClaim = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.ProcessStepDispatchClaim;
```

This means the route-facet boundary still depends on dispatcher-private model shape.

### 2. One broad all-facet route service still exists

`ProcessDispatchRouteServices` implements all route facets and forwards to `ProcessRunAutomationDispatchService`. This is a transitional all-in-one adapter. It is useful, but if kept too long it becomes a new miniature dispatcher.

### 3. Handler factory passes the same all-facet service to every handler

`ProcessDispatchRouteHandlerFactory.Create(...)` accepts one `ProcessDispatchRouteServices routeServices` and passes it into all handlers. This hides the dependency surface of each route handler.

### 4. Route context still carries dispatcher execution/candidate state

The route context is cleaner than before, but still centered around dispatcher-owned `ProcessClaimedDispatchExecution` and `DispatchCandidate` behavior. The next step should introduce read-only route snapshots and explicit mutable route state.

### 5. Process Core is still premature

The dispatcher has better seams now, but route service adapters, subprocess route side effects, finalizer handoffs, and direct-agent execution still need module-local model/adapter boundaries before any safe Core extraction.

## Recommended next cutline

Do not start `CanDoItAll.Processes.Core`.

The next bundle should build a module-local route service/model decoupling boundary:

- route snapshots,
- route dispatch claim snapshot,
- route execution outcome snapshot/adapters,
- route mutable state,
- route facet set,
- split route service implementations,
- factory narrowing,
- source scans that forbid dispatcher nested type aliases outside explicit adapter files.
