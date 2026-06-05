# Next Dispatch Cutline

This bundle stops at module-local helper boundaries. The next extraction should
remain inside `CanDoItAll.Modules.Processes` until a separate initiative proves
that a broader runtime contract is stable.

## Recommended Next Seam

The next seam should be candidate hydration and dispatch candidate selection:

| Candidate seam | Current owner | Why next |
| --- | --- | --- |
| Candidate header selection | `ProcessRunAutomationDispatchService.Dispatch.cs` | It still combines dispatchability, failed-run recovery, and sequence ordering. |
| Candidate hydration | `ProcessRunAutomationDispatchService.Dispatch.cs` | It loads run, definition, step, work brief, assignments, artifacts, expected inputs, branch outcomes, and technical-agent binding in one large method. |
| Technical-agent binding/read-access preparation | `ProcessRunAutomationDispatchService.Dispatch.cs` | It mixes candidate hydration with execution-client agent editor mutation. |
| Missing upstream artifact input detection | `ProcessRunAutomationDispatchService.Dispatch.cs` | It remains side-effectful and should be separated only after candidate facts are easier to test. |

## Do Not Extract Yet

- Do not create Process Core.
- Do not create production process-driver APIs or driver registries.
- Do not move EF writes, workflow calls, subprocess calls, execution-client calls, finalizer calls, or transition execution into pure route helpers.
- Do not promote private dispatcher/finalizer types into public contracts until a consumer actually requires that boundary.

## Current Stable Local Boundaries

- `ProcessDispatchRouteSnapshot`
- `ProcessAutomationExecutionRunSelection`
- `ProcessDispatchGuardLease`
- `ProcessDispatchLeaseHeartbeat`
- `ProcessDispatchStartTransitionPlanner`
- `ProcessDispatchRoutePlanner`
- `ProcessDispatchFinalizerContextFactory`

## Validation Required For The Next Seam

- Candidate selection and hydration parity tests.
- Failed-run recovery dispatchability tests.
- Technical-agent binding/read-access mutation proof.
- No Process Core or production driver API scan.
- Full build plus focused dispatch integration tests.
- No UI or small/medium/mobile proof artifacts.
