# Assumptions And Risks

## Working Assumptions

- The first repair should target the observation/read model, because the user reports page slowness while multiple processes run.
- Active-run cards need summary counts and active-agent metadata, not full selected-run detail payloads.
- The selected run detail pane may still load full detail because the user expects the full execution, coordination, evidence, and control sections for that one run.
- Playwright proof should use `/processes` because that is the global process management route.

## Critical Path Risks

- Changing dispatch logic while fixing UI slowness could accidentally alter process semantics; dispatch changes are out of scope unless measurement points there.
- Batching active-run summaries must preserve the current health signals: pending outbox, dead-lettered outbox, blocked or failed steps, active agents, and pending approvals.
- AgentFramework execution listing is file-backed and broad. A naive per-run query remains expensive even if process DB reads are optimized.

## Validation Risks

- Wall-clock timing in Visual Studio or debug builds can be noisy. Use repeated stopwatch measurements and compare relative improvement, not one exact number.
- Browser timing includes Blazor Server circuit startup, local database state, and dev server startup; capture route timing after the app is already ready.
- Test fixtures with no execution runs may understate AgentFramework file-read cost, so include enough active process runs to catch the repeated process DB load.

## Reopen Triggers

- Reopen core repair if active-run summaries still call `GetRunDetailsAsync` per active run.
- Reopen UI repair if Runs-tab refresh still reloads analytics when not on the Analytics tab.
- Reopen browser validation if Playwright cannot reach `/processes`, cannot identify the Runs tab, or timing is captured only during cold startup.
- Reopen execution closure if any targeted process runtime, read-query, or process Playwright smoke test fails.
