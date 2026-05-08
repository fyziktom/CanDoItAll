# Current State

## Runtime Execution Path

- `ProcessesService.StartRunAsync` creates a `ProcessRun`, assignments, step runs, work briefs, journal entries, project-structure sync records, and outbox records, then processes the run-start outbox.
- `ProcessRunAutomationDispatchService.DispatchAsync` repeatedly loads the next dispatch candidate, claims a step, executes an agent or subprocess, projects artifacts, and transitions the step.
- Dispatch candidate loading is intentionally rich because it must build prompts, artifact inputs, branch outcomes, role context, and technical-agent bindings. That path is not the first UI slowness target unless measurement proves it.

## UI Observation Path

- `ProcessWorkspace.OnParametersSetAsync` calls `LoadWorkspaceAsync` when route or query parameters change.
- `LoadWorkspaceAsync` loads definitions, runtime status overview, editor, executor and manager options, analytics, improvements, party options, runtime pane data, canvas state, and optionally manager chat.
- `ProcessWorkspace.LiveRefresh` starts a periodic four-second loop while the Runs or Analytics tab is active and there is active runtime work.
- `RefreshRuntimeWorkspaceAsync` currently reloads analytics unconditionally, force-refreshes runtime overview, reloads runtime pane data, refreshes canvas, and calls `StateHasChanged`.
- `LoadRuntimePaneDataAsync` loads launch plans, all runs for the selected process, active run summaries, selected run details, and then reselects runtime state.

## Identified Bottlenecks

- B001: `LoadActiveRunSummariesAsync` does a full `processesService.GetRunDetailsAsync` for every active run, even though the active-run card only needs outbox counts and blocked or failed step counts.
- B002: `LoadActiveRunSummariesAsync` calls `workspaceService.ListExecutionRunsAsync` once per active run. AgentFramework storage filters after listing run records, so repeated calls multiply file-system reads.
- B003: `LoadActiveRunSummariesAsync` calls `processesService.ListStepRunsAsync` again to resolve step titles after already loading full run details for the same run.
- B004: Runs-tab refresh reloads process analytics even though analytics is not visible on the Runs tab.
- B005: Selected run detail still enriches execution runs with full execution-run details. That is valuable for the detail pane, but it should not happen for every active run summary.

## Deep Scan Notes

- Async usage is generally proper; no synchronous `.Result` or `.Wait()` was found in the read path inspected here.
- The hot path has repeated LINQ materialization and per-run service calls. This is an architectural batching problem more than a micro-optimization problem.
- `ProcessRuntimeStateOverviewService` already batches status counts and caches by definition set; it is not the primary issue.
