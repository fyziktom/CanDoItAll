# Assumptions And Risks

## Assumptions

- `ProjectTaskExecutionState.NotStarted` is the only affirmative signal that a task has not happened. New tasks write it; missing legacy metadata is `Unknown` and fails closed.
- A unique primary assignment is preferred for display. Multiple assignments without exactly one primary produce an explicit ambiguous result. The full assignment set remains authoritative.
- Missing resource pricing clears an unstarted task cost and surfaces an unavailable quote; it does not invent a rate or preserve a stale amount.

## Critical Path Risks

- `SB01` is critical: incorrect resolver semantics or strategy selection invalidates every behavior in `SB02`.
- Mixed scalar replacement is forbidden because the existing compensation snapshot can restore only zero or one assignment.
- Server enforcement and UI preview must agree; proof limited to the estimator component would be insufficient.

## Validation Risks

- CodeAnalytics before/after snapshots are unavailable; compiler, direct project inspection, source assertions, and test construction provide the substitute evidence.
- Browser proof needs a runnable local web host and seedable mixed-assignee task. If environment startup is blocked, component dialog/coordinator proof is required and the browser gap is recorded honestly.

## Reopen Triggers

- Any resource kind still branches inside the orchestration service reopens `SB01`.
- A new/changed project reference or cycle reopens the dependency gate.
- A mixed-assignee update that enables direct scalar mutation or drops an unchanged assignment reopens `SB01`.
- Any create/update path that can persist a stale unstarted cost reopens `SB02`.
- Browser evidence contradicting component tests reopens the owning behavior subbundle.
