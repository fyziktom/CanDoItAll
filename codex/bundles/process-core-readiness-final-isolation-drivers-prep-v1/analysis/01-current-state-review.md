# Current State Review

## Verdict

The latest `maf-processes-refactor` state appears successful within its declared scope. The route service/model decoupling bundle completed, build and focused tests passed, and no Process Core or production driver API was introduced.

## Positive findings

- `Dispatch.cs` is now a thin dispatch loop and is reported around 208 lines after the last bundle.
- Route handlers are top-level module-local classes rather than private nested dispatcher classes.
- Route-facing files no longer expose direct dispatcher nested aliases for `DispatchCandidate`, `DispatchExecutionOutcome`, or `ProcessStepDispatchClaim`.
- `ProcessDispatchRouteModels.cs` introduces route-owned candidate, claim, and execution outcome models.
- The route source boundary scan reports no all-facet route service and no forbidden route-facing dispatcher nested model references.
- The latest proof reports a successful solution build, full unit test run, focused dispatch integrations, and focused subprocess/projection/execution-client integrations.

## Remaining issues

### 1. Route services still adapt back to dispatcher methods

`ProcessDispatchRouteServices.cs` now has narrow services, but most services still call back into `ProcessRunAutomationDispatchService` through `ProcessDispatchRouteModelAdapters`.
This is acceptable as a transition state but is not a clean application boundary.

### 2. Route model adapters still retain dispatcher-owned source payloads

`ProcessDispatchRouteModelAdapters.cs` keeps `DispatcherCandidateSource`, `DispatcherDispatchClaimSource`, and `DispatcherExecutionOutcomeSource`. That is correctly isolated, but it means route models are not independently usable yet.

### 3. Candidate hydration is still application-heavy

`ProcessDispatchCandidateHydrationService` does EF reads, workspace scoping, execution-run lookup, direct-agent binding, manual recovery lookup, project-structure access mutation, and cooperation metadata assembly.
This should remain application-local, but it should be split into explicit collaborators before any Core extraction.

### 4. Subprocess runtime still uses dispatcher-owned aliases

`ProcessDispatchSubprocessRuntimeService` uses dispatcher nested aliases and owns child-run observation plus projection persistence. It should be isolated further before Core discussions.

### 5. Finalizer application service still depends on dispatcher finalizer aliases

`ProcessDispatchFinalizerApplicationService` uses dispatcher-owned finalizer context/result aliases and delegates. This is a bridge, not yet a standalone application boundary.

### 6. Core readiness is improving but not complete

Pure rules such as route order, route kind classification, subprocess lifecycle status mapping, and transition request shaping are plausible future Core candidates.
EF, storage, AgentFramework execution, claim lifecycle, artifact projection, and process module services remain application/infrastructure local.

## Decision

Do not create `CanDoItAll.Processes.Core` in this bundle.
The next best step is a multi-domain isolation pass that burns down the remaining dispatcher adapters and creates a final go/no-go readiness matrix for Core and driver preparation.
