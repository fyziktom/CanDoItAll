# Normalized Requirements

## Requirements

| Id | Requirement | Notes |
| --- | --- | --- |
| R001 | Active/running run counts must include only `ProcessRunStatus.Active`. | Blocked and failed runs must not be included in "active runs" wording. |
| R002 | The process page header and definition list must expose active, blocked, and failed run counts separately where relevant. | Use existing `StatusBadge`/component-library patterns. |
| R003 | A generic runtime state projection service must provide process run state counts and reusable runtime detail summary data for UI and future Manager-agent scenarios. | It must derive from existing persistence/runtime read APIs and must not mutate or own canonical process state. |
| R004 | The projection service may cache scoped snapshots to avoid duplicate reads during one workspace load, and UI mutations must invalidate/reload snapshots explicitly. | No silent stale fallback. |
| R005 | Initial process page load must not load full selected-run details unless the user opened the Runs tab or a direct `runId` query requires a run detail focus. | Lists, badges, and launch-plan summary data may still load. |
| R006 | Active-run summary loading must avoid full per-run detail loading when list-level projections already contain the needed status counts. | Keep detail loading for the selected run only. |
| R007 | The selected process Runs tab must let an operator stop blocked runs from the run history list. | The service validates that only blocked runs are stoppable from this action. |
| R008 | Stopping a blocked run must mark it `Cancelled`, set completion/update timestamps, add an audit journal entry, and use existing project-structure sync if applicable. | It must fail predictably for non-blocked or missing runs. |
| R009 | Add focused tests for count semantics, lazy detail behavior where practical, and stop blocked run behavior. | At minimum, integration tests must cover service behavior. |
| R010 | Capture browser proof for the processes page badges and blocked-run stop action when the local app is available. | If the app is unavailable, record the exact blocker. |

## Constraints

- Do not introduce magic strings for runtime state decisions; use `ProcessRunStatus` and `ProcessStepRunStatus`.
- Do not add XML documentation comments.
- Keep UI in existing component-library style; do not replace with raw ad hoc markup beyond the existing section/list patterns.
- Do not split canonical process state away from `ProcessesService`, EF entities, and runtime read queries.
