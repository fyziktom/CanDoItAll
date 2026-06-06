# Source Hotspots

## Already completed by previous bundle

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
  - reduced to dispatch facade plus residual subprocess/pre-execution helpers.
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchClaimLease.cs`
  - owns claim store/coordinator and heartbeat interaction.
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs`
  - owns claimed dispatch execution flow.
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExceptionClosure.cs`
  - owns claim-lost, heartbeat-lost and generic failure closure.
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePipeline.cs`
  - owns canonical route stage order.

## Next cutline

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs`
  - currently contains private nested handler classes and uses `new ...RouteHandler(this)`.
  - should be split into top-level module-local handler classes.
  - handler dependencies should become explicit route facets, not the entire dispatcher.

## Must not touch

- UI/Razor/CSS/JS/TS files.
- Process Core packages/projects.
- Production driver packages/APIs.
