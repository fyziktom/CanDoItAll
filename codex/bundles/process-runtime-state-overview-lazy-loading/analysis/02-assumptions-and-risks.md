# Assumptions And Risks

## Assumptions

- `ProcessRunStatus.Active` is the only run state that should be called "active" or "running" in badges.
- Blocked and failed runs remain visible in run history, but they are not active/running.
- Stopping a blocked run maps to `ProcessRunStatus.Cancelled`, not deletion or step completion.
- The runtime state service can use a scoped in-memory projection cache to prevent duplicate UI reads during one workspace load, but the database and existing runtime services remain authoritative.

## Critical Path Risks

- If the new state service owns mutable process state instead of deriving from existing read queries, later UI and Manager-agent use would split source of truth.
- If run details still load during initial page open, UI badges may be correct but the slow-load complaint remains unsolved.
- If a blocked-run stop operation bypasses terminal guards or does not journal the decision, process auditability regresses.

## Validation Risks

- Integration tests must prove count semantics and stop behavior. Browser proof must confirm the badges and stop action render in the actual Blazor page.
- The current local dataset may not include the exact production-like `55 active runs` state, so tests must create controlled active, blocked, and failed states.
- Playwright may be blocked if `https://localhost:7271/` is not running; record the blocker instead of claiming visual proof.

## Reopen Triggers

- Reopen subbundle 01 if any later phase needs run status counts not exposed by the state service.
- Reopen subbundle 02 if browser or tests show full run details still load on page open without Runs tab or `runId`.
- Reopen subbundle 03 if cancelled runs can still transition steps or if stop lacks a durable journal entry.
