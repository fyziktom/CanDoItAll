# Target Architecture

## Current transitional shape

```text
ProcessDispatchRouteHandlers.cs
  top-level route handlers

ProcessDispatchRouteFacets.cs
  route facet interfaces
  still use dispatcher nested aliases

ProcessDispatchRouteServices.cs
  one all-facet adapter
  holds ProcessRunAutomationDispatchService dispatcher
  implements all route facet interfaces
  forwards most calls to dispatcher
```

## Target shape for this bundle

```text
ProcessDispatchRouteModels.cs
  ProcessRouteCandidateSnapshot
  ProcessRouteStepSnapshot
  ProcessRouteRunSnapshot
  ProcessRouteDispatchClaim
  ProcessRouteExecutionContext
  ProcessRouteDirectAgentOutcome
  ProcessRouteMutableState

ProcessDispatchRouteAdapters.cs
  only bridge from dispatcher-owned nested models into route-owned models

ProcessDispatchRouteFacetSet.cs
  IProcessDispatchRouteFacetSet or record of narrow services

Narrow service implementations:
  ProcessDispatchDatabaseRequirementRouteService
  ProcessDispatchUpstreamMaterializationRouteService
  ProcessDispatchRecoveryRouteService
  ProcessDispatchSubprocessRouteService
  ProcessDispatchStartTransitionRouteService
  ProcessDispatchWorkflowRouteService
  ProcessDispatchDirectAgentRouteService
  ProcessDispatchGuardRouteService
  ProcessDispatchFinalizerRouteService
  ProcessDispatchFailureClosureService

ProcessDispatchRouteHandlerFactory.cs
  consumes route facet set/narrow services
  never consumes one all-facet service

ProcessDispatchRouteHandlers.cs
  consumes route-owned models
  consumes only narrow facet interfaces
```

## Adapter rule

Dispatcher-owned nested types may appear only in explicit adapter files:

- `ProcessDispatchRouteModelAdapters.cs`
- `ProcessRunAutomationDispatchService.RouteServiceAdapters.cs` or equivalent dispatcher-owned adapter partial
- focused tests that verify adapter parity

They must not appear in:
- route handlers,
- route facet interfaces,
- route service implementations,
- route handler factory,
- route pipeline/order assertion code.

## Driver readiness

This bundle still does not add production process drivers.

It should document:
- route stage intent,
- side-effect category,
- future driver evidence family,
- which route stages could later accept domain driver diagnostics.

No runtime driver registration, driver interfaces, or driver packages are allowed.
