# SB06 Source Assertions

- Added `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationExecutionRunSelection.cs`.
- `ProcessAutomationExecutionRunSelection` owns pure blocking, stale, recoverable, current-attempt, competing-active, fresh-recovery-skip, completion-skip, and concurrent-session-busy rules.
- `ProcessRunAutomationDispatchService.Concurrency.cs` keeps the existing wrapper method names and delegates into the selector with `AutomationActor`, stale timeout, and fresh recovery grace period.
- `TryAdoptConcurrentAutomationExecutionAsync` and `ResolveCompetingActiveAutomationExecutionAsync` still own execution-client queries, detail retrieval, polling, and `ConcurrentAutomationExecution` construction; the helper does not call EF, storage, workflow, subprocess, or agent execution APIs.
- Completion artifact recovery still reaches stale-run classification through the existing private partial-service wrapper, so SB06 does not broaden artifact-recovery scope.
- Focused tests assert current-attempt competing selection, stale approval behavior, and fresh recovery/completion skip parity through `SB06_INV_001`, `SB06_INV_002`, and `SB06_INV_003`.
