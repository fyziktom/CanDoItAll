# Current State

The previous claim/route bundle created useful module-local helper boundaries:

- `ProcessDispatchRouteSnapshot`
- `ProcessAutomationExecutionRunSelection`
- `ProcessDispatchGuardLease`
- `ProcessDispatchLeaseHeartbeat`
- `ProcessDispatchStartTransitionPlanner`
- `ProcessDispatchRoutePlanner`
- `ProcessDispatchFinalizerContextFactory`

Current remaining hotspot:

- `ProcessRunAutomationDispatchService.Dispatch.cs` remains about 1998 lines and still contains the large candidate hydration method.
- `ProcessRunAutomationDispatchService.Concurrency.cs` remains about 1414 lines, though pure selection helpers have already been extracted.
- The next cutline from the previous bundle recommends candidate selection and hydration.

The system is closer to a future Process Core, but it is not ready yet. Candidate hydration still couples EF read models, process-specific runtime shape, assignment resolution, technical-agent binding, project-structure access mutation, and candidate construction.
