# SB13 Source Assertions

- Added `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.FinalizerContextFactory.cs` with private nested `ProcessDispatchFinalizerContextFactory`.
- The factory owns route-specific `ProcessStepCompletionFinalizerContext` construction for manager artifact recovery, direct agent, workflow-backed role, and subprocess parent routes.
- `DispatchAsync` no longer constructs `ProcessStepCompletionFinalizerContext` inline; it calls `ForManagerArtifactRecovery`, `ForDirectAgent`, `ForWorkflow`, and `ForSubprocess`.
- The factory preserves route field parity: executor kind, candidate, completion status/reason, selected branch outcome, execution detail, workflow run id, subprocess run id, response text, artifact projection flags, manager recovery flag, trigger, renew lease callback, recovery execution id, and recovered-for execution id.
- The factory does not execute finalization, transitions, EF, workflow, subprocess, execution-client, logging, or service-scope operations.
- No Process Core, production process driver API, UI, or small/medium/mobile proof artifacts were introduced.
