# manager-artifact-recovery

## Status

- `Completed`

## Objective

- Route missing required completion-artifact recovery through the process manager instead of the same step executor.

## Success Criteria

- Manager recovery resolves a manager technical agent when available.
- A manager directive journal entry is recorded before manager recovery execution.
- The recovery prompt tells the manager to use previous step history, upstream artifacts, execution run ids, tool receipts, and current-run evidence.
- If manager recovery cannot run or cannot produce artifacts, the step blocks with exact missing artifact names.

## Covered Inputs

- R001, R002, R003, R004, R005, R006.

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.Dispatch.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.Models.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\Observation\ProcessManagerChatService.cs`

## Deliverables

- Manager-targeted recovery helper(s).
- Manager recovery directive content grounded in process history.
- Journal evidence for manager recovery request.

## Dependency Impact

- Validation proof depends on this implementation. Weak routing proof would leave the original stuck-loop behavior intact.

## Validation Depth

- Process-critical closure.

## Implementation Steps

1. Update completion artifact recovery to resolve and use a manager technical agent.
2. Record a manager directive before executing manager recovery.
3. Project manager recovery artifacts through the existing projection path.
4. Return completed only when missing required artifact expectations are recorded.
5. Return blocked with exact reasons when recovery is unavailable or insufficient.

## Scope Exceptions

- No UI changes.
- No manual artifact fabrication for the live run.

## Do Not Do

- Do not weaken artifact validation.
- Do not rerun broad implementation work for missing handoff documents.
- Do not add a hidden fallback to the original executor when no manager is available.

## Acceptance Checklist

- Manager recovery code path exists and is reachable from missing completion artifacts.
- Directive text includes missing artifact names and prior execution evidence.
- Remaining missing artifacts block explicitly.

## Proof Required

- Code references in execution report.
- Targeted test assertions or build proof.

## Browser Validation Logging

- N/A.

## Progression Gate

- The recovery code no longer calls the same executor for completion-artifact gaps when a process manager can be resolved.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
