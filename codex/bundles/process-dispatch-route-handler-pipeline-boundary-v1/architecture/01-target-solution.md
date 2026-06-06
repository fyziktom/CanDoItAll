# Target Solution

## Route Handler Pipeline Boundary

- Keep all new route-handler infrastructure internal to `CanDoItAll.Modules.Processes`.
- Keep `ProcessRunAutomationDispatchService.Dispatch.cs` as the outer dispatch facade.
- Move route-stage decisions from `ExecuteClaimedDispatchRouteAsync` into explicit module-local route handlers.
- Preserve `ProcessDispatchRoutePipeline.StageOrder` exactly.
- Keep side-effecting operations visible through handler, coordinator, store, finalizer, execution, or transition type names.

## Prohibited Targets

- Do not create `CanDoItAll.Processes.Core`.
- Do not create process driver packages or production driver registry APIs.
- Do not move EF entities or database ownership out of the module.
- Do not create UI or browser-proof artifacts for this runtime/service refactor.

## Expected End State

- `RouteExecution.cs` delegates route-stage decisions to a composed module-local route-handler pipeline.
- Handler context and result vocabulary are strongly typed and module-local.
- Focused tests and source scans prove route order, behavior preservation, side-effect ownership, and guardrail compliance.
