# Source Hotspots

## Primary files from latest branch review

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModels.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationService.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerApplicationService.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchStepTransitionService.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRunClosureGuardService.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchClaimLease.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteFacets.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlerFactory.cs`

## Expected changed-source rules

- The bundle may add module-local `ProcessDispatch*` services/models under `CanDoItAll.Modules.Processes`.
- The bundle must not create `src/CanDoItAll.Processes.Core`.
- The bundle must not create `IProcessDriverPack`, `IProcessDriverRegistry`, `ProcessDriverRegistry`, `ProcessDriverPack`, or production driver packages.
- No `.razor`, `.css`, `.js`, `.ts`, `.tsx`, image, screenshot, small-screen, medium-screen, mobile, phone, or tablet proof files are expected.
