# Current State Analysis

## What is now complete

The previous main-loop/claim-lifecycle bundle appears complete in declared scope:

- `Dispatch.cs` is now a smaller dispatch facade.
- `ProcessDispatchClaimLease.cs` owns EF claim writes, renew, held-check and release.
- `ProcessRunAutomationDispatchService.RouteExecution.cs` owns the claimed dispatch route flow.
- `ProcessRunAutomationDispatchService.ExceptionClosure.cs` owns claim-lost/heartbeat-lost/generic failure closure.
- `ProcessDispatchRoutePipeline.cs` exposes the canonical route order.

## Why Process Core is still too early

The route execution body still mixes route decisions and side-effect handoffs:
- candidate hydration
- fresh recovery skip
- PostgreSQL requirement blocking
- upstream artifact materialization
- stranded artifact recovery finalization
- subprocess dispatch
- start transition and candidate reload
- workflow execution observation
- direct-agent execution and finalizer handoff
- competing execution guard
- run-closed guard
- finalized transition application

This route flow should first become a module-local route-handler pipeline. After that, it will be clearer which contracts are stable enough for future `Processes.Core`.

## Why this also prepares future drivers

Future process drivers should not couple to dispatcher partials. A route-handler pipeline provides named route intents and handler inputs that can later be mapped to driver-readiness concepts such as:
- pre-execution environment requirement
- upstream materialization intent
- delegated subprocess evidence
- workflow execution route
- direct agent execution route
- finalizer transition evidence

This bundle documents those concepts only. It must not implement production driver APIs.
