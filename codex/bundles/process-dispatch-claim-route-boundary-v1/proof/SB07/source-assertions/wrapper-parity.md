# SB07 Source Assertions

- SB07 made no additional production-code movement; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs` already delegates wrapper logic to `ProcessAutomationExecutionRunSelection` after SB06.
- Existing wrapper names remain present on `ProcessRunAutomationDispatchService`: `HasBlockingAutomationExecutionRun`, `ResolveBlockingAutomationExecutionRunId`, `ResolveRecoverableAutomationExecutionRunId`, `ShouldSkipAutomationCompletionTransition`, `ShouldSkipFreshAutomationDispatch`, and `IsConcurrentAutomationSessionBusyException`.
- New parity tests compare wrapper return values against `ProcessAutomationExecutionRunSelection` for blocking selection, current-attempt blocking selection, recoverable terminal selection, fresh recovery skip, completion skip, and session-busy classification.
- Async adapter responsibilities remain outside the selector: execution-run listing, detail fetch, polling, task delay, and `ConcurrentAutomationExecution` creation still live in `ProcessRunAutomationDispatchService.Concurrency.cs`.
- No EF, storage, workflow, subprocess, agent execution, UI, Process Core, or production process driver API was introduced for this subbundle.
