# Current State

## Existing Runtime Count Behavior

- `ProcessWorkspace.ActiveRunCountText` currently sums `definition.ActiveRunCount` and labels it as active runs.
- `ProcessDefinitionListQueryService` populates `ActiveRunCount` with `run.Status == ProcessRunStatus.Active || run.Status == ProcessRunStatus.Blocked`. This is the direct reason blocked runs appear inside the active-run count.
- `ProcessRunStatusResolver.Resolve` makes blocked and failed process states authoritative at the run level when any step is blocked or failed.
- `ProcessAnalyticsSummary` already separates `ActiveRuns` and `BlockedRuns`, but there is no failed-run count and the page header is not consuming a single runtime state projection.

## Existing Page Load Behavior

- `ProcessWorkspace.LoadWorkspaceAsync` loads definitions, editor, executor options, analytics, improvements, project party options, launch plans, run list, active run summaries, and then selected run details.
- `ResolveSelectedRunId` defaults to `runs.FirstOrDefault()?.Id`, so opening the page with no `runId` still selects a run.
- `LoadRunDetailsAsync` is always called from `LoadWorkspaceAsync` after run selection. This loads full selected-run data even while the active detail tab remains `Definition`.
- `ProcessWorkspaceRunDetailsLoader.LoadAsync` calls `ProcessesService.GetRunDetailsAsync`, escalation reads, AgentFramework execution run queries, and then fetches execution run details for up to 200 execution runs. That is intentionally rich detail data and should not be part of first page paint unless the Runs tab or `runId` route requires it.
- `LoadActiveRunSummariesAsync` only considers `ProcessRunStatus.Active`, but for each active run it calls AgentFramework execution run APIs and `ProcessesService.GetRunDetailsAsync`. This can be heavy when many active runs exist.

## Existing Stop/Cancel Capability

- `ProcessRunStatus.Cancelled` exists and terminal guards already reject late step transitions for completed, failed, or cancelled runs.
- There is no public `ProcessesService` method for stopping a run from the workspace.
- `ProcessWorkspaceRunsLifecycleSection.razor` displays the run history list and is the right UI location for a blocked-run stop action.

## Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Loading.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.LiveRefresh.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunDetailsLoader.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsLifecycleSection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesService.DefinitionListQuery.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.RuntimeReadQuery.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Persistence\Entities\ProcessRuntimeModels.cs`
