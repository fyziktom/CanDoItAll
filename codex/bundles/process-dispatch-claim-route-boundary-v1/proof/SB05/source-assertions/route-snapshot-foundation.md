# SB05 Source Assertions

- Added `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteSnapshot.cs`.
- `ProcessDispatchRouteSnapshot` captures process run id, step run id, run status, step status, step kind, technical agent id, recovery execution run id, current attempt start time, and trigger facts.
- `ProcessDispatchRouteEligibility` owns the existing run/step eligibility rules while `ProcessRunAutomationDispatchService` keeps wrapper methods.
- `DispatchAsync` now uses route snapshot facts for fresh recovery skip inputs, agent automation classification, subprocess route classification, and start-transition necessity.
- No EF, storage, workflow, subprocess, agent execution, or finalizer side effects moved into the route snapshot helper.
