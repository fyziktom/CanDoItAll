# Current State Analysis

## What is already good

- MAF no longer directly depends on `CanDoItAll.Modules.Processes`.
- Process automation execution now flows through process-owned snapshots and `IProcessAutomationExecutionClient`.
- Artifact projection has been split into source-family coordinators and projection facets.
- Projection coordinators no longer consume a broad host and no longer directly reference dispatcher nested models, except through a dedicated snapshot adapter.
- Observation/outcome parsing has module-local helpers.
- Implementation proof, artifact satisfaction, residual artifact validation, execution/retry/provider recovery and projection logic have been materially reduced.

## Current bottleneck

`ProcessRunAutomationDispatchService.Dispatch.cs` remains the high-risk orchestration hotspot. The `DispatchAsync` method still directly owns:

- candidate header load timing/logging;
- in-memory step guard lease;
- durable claim acquisition;
- heartbeat creation and cancellation-token derivation;
- candidate hydration;
- fresh-recovery skip;
- database requirement blocking;
- missing upstream artifact materialization;
- stranded artifact recovery;
- subprocess route;
- start transition route;
- workflow route;
- direct-agent execution route;
- competing execution check;
- closed-run check;
- direct finalizer invocation;
- exception classification;
- failure transition;
- heartbeat disposal;
- durable claim release.

This is still too much to extract into Process Core. The next step must isolate the route and claim lifecycles inside `CanDoItAll.Modules.Processes` first.

## Target state after this bundle

- `DispatchAsync` becomes a small loop that obtains candidate headers and delegates claimed step processing to module-local dispatch-loop services.
- Durable claim EF operations are behind a module-local claim store/coordinator.
- Heartbeat/renew/release semantics remain identical but are isolated and tested.
- Exception closure and failure transition request construction are explicit and tested.
- Route order is captured as a testable route pipeline.
- No public Core/API surface exists yet.
