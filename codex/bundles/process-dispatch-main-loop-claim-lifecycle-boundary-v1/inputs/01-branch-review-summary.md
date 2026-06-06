# Branch Review Summary

Reviewed branch: `maf-processes-refactor`.

Last completed bundle reviewed: `process-dispatch-projection-model-rule-decoupling-boundary-v1`.

Observed closure signals:

- Execution report says status `Completed` and completed date `2026-06-06`.
- Full solution build, focused unit projection architecture tests, focused integration projection tests, source scans and no-stub scans were reported as passed.
- Browser validation is correctly N/A for runtime/service-only work.
- No `CanDoItAll.Processes.Core` project, no production `IProcessDriverPack`, `IProcessDriverRegistry`, `ProcessDriverRegistry`, or UI proof drift was reported.
- Projection models now exist in `ProcessProjectionModels.cs` and projection context uses projection snapshots instead of direct dispatcher models.
- The remaining direct nested-dispatcher references are concentrated in the projection snapshot adapter (`ProcessProjectionSnapshotBuilderAdapter`), which is an acceptable transitional adapter boundary.

Current residual hotspot:

- `ProcessRunAutomationDispatchService.Dispatch.cs` still owns the main dispatch loop, durable claim acquisition/renew/release, heartbeat lifetime, route ordering, exception closure and failure transition logic.
- `DispatchAsync` still directly sequences hydration, database requirement, upstream materialization, stranded recovery, subprocess, start transition, workflow execution, direct-agent execution, competing execution check, closed-run check, finalizer call and failure exception transition.
- Claim lifecycle methods still use EF directly inside the dispatcher partial (`TryClaimStepDispatchAsync`, `RenewStepDispatchClaimAsync`, `IsStepDispatchClaimHeldAsync`, `ReleaseStepDispatchClaimAsync`).
- Subprocess projection and pre-execution guards have helper boundaries, but dispatch loop ownership is still monolithic.

Architectural verdict:

Do **not** start Process Core yet. The next safest step is module-local dispatch-loop and claim-lifecycle isolation. This prepares future Process Core and future drivers by separating route/lifecycle orchestration from EF claim persistence and route side-effect handlers without creating public contracts or production driver APIs.
