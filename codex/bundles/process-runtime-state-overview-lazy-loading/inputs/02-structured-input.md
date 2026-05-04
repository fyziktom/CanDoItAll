# Structured Input

## Core Objective

Make process runtime state visible, accurate, and reusable while keeping expensive run details lazy.

## Hard Constraints

- Active/running counts include only `ProcessRunStatus.Active`.
- Blocked and failed counts are shown separately.
- The generic state service is a projection/cache over existing persisted/runtime state, not a second source of truth.
- Full selected-run details are not loaded unless the Runs tab or a direct `runId` query needs them.
- Stop blocked run means explicit cancellation with audit trail, not deletion.

## Source Artifacts

- `inputs/00-original-request.md`
- Current code references listed in `inputs/01-source-artifacts.md`

## Input Coverage Signals

| Note | Exact user signal | Normalized requirement |
| --- | --- | --- |
| N001 | `"55 active runs" but most of them are blocked or failed` | Active run counts must include only currently running process runs. |
| N002 | `Add also other badges with info about how many failing we have or blocked.` | Header and list UI must expose blocked and failed run counts separately from active. |
| N003 | `provided by some generic service about state of the running processes and their details` | Add a reusable runtime state projection service. |
| N004 | `controlled cache ... must not split the source of truth` | The service may cache scoped projections but persistence/runtime read queries remain authoritative. |
| N005 | `option to stop blocked processes in list in selected process "Runs" tab` | Add a UI action on blocked run list items that cancels blocked runs through a service operation. |
| N006 | `Analyze how we load the data when we open processes page ... lazy loading` | Opening the page must not preload full run details unnecessarily. |
| N007 | `If I do not open Run tab ... it should not have preloaded data` | Full selected-run detail loading is allowed only when the Runs tab is active or a direct `runId` query requires it. |

## Dependency And Sequencing Signals

- Runtime state overview service unlocks badges, lazy reload orchestration, and future Manager-agent observations.
- Lazy loading depends on that service so list/header projections remain available without selected-run details.
- Stop action depends on the runtime reload flow so the UI refreshes counts after cancellation.

## Validation Expectations

- Integration tests for active/blocked/failed counts and blocked-run stop behavior.
- Build or targeted test run for the affected projects.
- Browser proof against the processes page when the local app is available.

## UI Validation Strategy

- Use a large desktop browser pass on `https://localhost:7271/processes` or the project-scoped processes route.
- Confirm active/blocked/failed badges are readable and do not overlap.
- Confirm blocked run history items expose the stop action and non-blocked runs do not.
- Confirm no Blazor error UI appears.

## Browser Validation Analytics

- Record route, viewport, actions, assertions, screenshot paths, and result in `reviews/01-execution-report.md`.

## Working Assumptions

- "Stop" means `ProcessRunStatus.Cancelled`.
- Scoped caching is enough for the current UI. Durable cross-process cache is deferred.

## Primary Risks

- Accidentally moving canonical state into the new projection service.
- Keeping hidden full run detail loads in first page paint.
- Letting stop/cancel bypass existing terminal state guards.
