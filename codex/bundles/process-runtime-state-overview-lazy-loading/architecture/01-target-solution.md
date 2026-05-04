# Target Solution

## Service Boundary

Add a scoped process runtime state projection service in `CanDoItAll.Modules.Processes`. The service provides:

- definition-level run status counts for page/list badges
- selected process run state summary for header and Runs tab use
- active run summaries without loading full selected-run detail graphs
- explicit cache invalidation after runtime mutations

The service must not own state transitions. Process mutation remains in `ProcessesService`; runtime read data remains derived from EF/query services and AgentFramework read APIs.

## UI Boundary

`ProcessWorkspace` should load lightweight page state on parameter changes. Full selected-run details are loaded only when `detailTab == "runs"` or when `RunIdQuery` selects a run directly. Switching to the Runs tab can then load the selected run details if a run is focused.

Header/list badges should be fed by the state projection, not by the misleading `ActiveRunCount` query field. Existing component-library primitives remain in use.

## Stop Boundary

Add an explicit `StopBlockedRunAsync` operation on `ProcessesService` or a tightly scoped runtime operation method. It validates:

- run exists
- run is currently `ProcessRunStatus.Blocked`
- cancellation timestamp and update timestamp are applied
- audit journal entry is recorded
- project structure sync receives the terminal run state

The UI calls this through `ProcessWorkspaceRunsTabPresenter` from the run history list and reloads runtime state afterward.

## Lazy Loading Boundary

Do not preload `ProcessWorkspaceRunDetails` when the selected process is shown on Definition/Roles/Steps/Analytics/Exchange tabs. Keep list-level runs and aggregate badges available because they are cheap and needed for navigation.
