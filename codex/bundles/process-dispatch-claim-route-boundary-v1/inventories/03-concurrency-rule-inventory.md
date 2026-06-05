# Concurrency Rule Inventory

Live source captured in SB03 from `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`.

| Method | Source range | Current behavior | Target boundary | Parity proof |
| --- | --- | --- | --- | --- |
| `HasBlockingAutomationExecutionRun(executionRuns)` | Lines 34-35 | Wrapper using `DateTimeOffset.UtcNow`. | Preserve wrapper; delegate to helper overload. | Existing smoke tests at `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` lines 63-90. |
| `HasBlockingAutomationExecutionRun(executionRuns, now)` | Lines 37-42 | Returns whether `ResolveBlockingAutomationExecutionRunId` finds a value. | Pure selection helper. | Blocking/stale tests lines 439-535. |
| `ResolveBlockingAutomationExecutionRunId(executionRuns)` | Lines 44-46 | Wrapper using `DateTimeOffset.UtcNow`. | Preserve wrapper; delegate to helper overload. | Existing smoke tests lines 63-90. |
| `ResolveBlockingAutomationExecutionRunId(executionRuns, now)` | Lines 48-62 | Selects latest fresh active automation run by effective updated time, then created time; ignores manual, terminal, stale runs. | Pure selection helper. | Tests lines 439-515. |
| `ResolveBlockingAutomationExecutionRunId(stepRun, executionRuns, now)` | Lines 64-82 | Same blocking selection, additionally current-attempt filtered by `stepRun.StartedAtUtc`. | Pure selection helper. | Test lines 518-535. |
| `ResolveRecoverableAutomationExecutionRunId(stepRun, executionRuns)` | Lines 84-105 | For `InProgress` step only, returns latest completed/failed automation run from current attempt. | Pure selection helper. | Tests lines 2432-2455 and 2597-2742. |
| `ResolveReusableAutomationChatSessionId(executionRuns)` | Lines 107-113 | Always returns null; intentionally avoids session reuse. | Preserve wrapper; move to helper only if it keeps explicit no-reuse semantics. | Tests lines 2608-2670. |
| `TryAdoptConcurrentAutomationExecutionAsync(candidate, cancellationToken)` | Lines 115-142 | Lists execution runs, selects blocking current-attempt run, polls detail twice while non-terminal, returns concurrent execution detail and recovered response. | Async adapter remains in dispatcher; delegate only selection to helper. | Existing and added selection tests; do not move `executionClient` calls. |
| `ResolveCompetingActiveAutomationExecutionAsync(candidate, executionOutcome, cancellationToken)` | Lines 144-166 | Lists execution runs, excludes current execution run, picks latest fresh current-attempt active competitor. | Async adapter remains in dispatcher; delegate pure competitor selection to helper. | New helper parity should cover excluding current run and current-attempt filtering. |
| `ShouldSkipAutomationCompletionTransition(currentStatus, requestedStatus)` | Lines 168-178 | Skips same status and statuses outside active execution lane; allows `InProgress` and `WaitingApproval`. | Pure transition guard helper. | Tests lines 2744-2759. |
| `IsConcurrentAutomationSessionBusyException(exception)` | Lines 180-186 | Recognizes known `InvalidOperationException` session-collision messages after trimming. | Pure exception classifier helper. | Tests lines 2672-2690. |
| `ShouldSkipFreshAutomationDispatch(currentStatus, recoverableExecutionRunId, currentAttemptStartedAtUtc, now, trigger)` | Lines 188-216 | Skips only fresh `InProgress` recovery-scan redispatches without recoverable execution run. | Route decision helper, not execution-run selection helper. | Tests lines 2761-2805. |
| `IsBlockingAutomationExecutionRun(executionRun, now)` | Lines 218-226 | Automation actor, non-terminal state, not stale. | Private pure helper inside selection helper. | Covered through blocking selection tests. |
| `IsStaleAutomationExecutionRun(executionRun, now)` | Lines 228-241 | Pending approvals are never stale; otherwise stale after timeout from updated-or-created time. | Private pure helper inside selection helper. | Tests lines 460-515. |
| `IsRecoveryTrigger(trigger)` | Lines 243-249 | Trimmed ordinal ignore-case match on `runtime-recovery-scan`. | Route decision helper. | Tests lines 2761-2805. |
| `IsRecoverableExecutionRunForCurrentAttempt(executionRun, currentAttemptStartedAtUtc)` | Lines 251-262 | Missing step start allows all; otherwise run start falls inside current attempt window. | Private pure helper inside selection helper. | Tests lines 518-535 and 2693-2742. |
| `ResolveRecoveredExecutionResponseText(detail)` | Lines 264-276 | Prefer last assistant chat message, fallback serialized assistant response, fallback result summary. | Response text helper can remain local; not part of first selection extraction. | Existing recovered-response behavior is used by concurrent adoption. |

## Design Decision

- Introduce a module-local helper under `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch`, not a new project.
- Keep `ProcessRunAutomationDispatchService` wrappers so existing tests and callers remain stable.
- Keep `executionClient.ListExecutionRunsAsync`, `GetExecutionRunDetailAsync`, polling, and delay behavior in dispatcher async adapters.
- Move pure selection/classification logic only after SB04 guardrails pass.
- Add helper-facing tests before or with wrapper migration so stale, active, terminal, competing, current-attempt, fresh recovery, and busy exception semantics remain pinned.
