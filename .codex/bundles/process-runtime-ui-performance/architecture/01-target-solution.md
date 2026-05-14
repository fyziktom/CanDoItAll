# Target Solution

## Design

- Add a lightweight process runtime read model for active-run health metrics keyed by run id.
- Keep the read model in the Processes application/service layer, backed by `IProcessRuntimeReadQueryService`, so Blazor components do not learn persistence details.
- Update `ProcessWorkspaceRunDetailsLoader.LoadActiveRunSummariesAsync` to:
  - resolve active runs once
  - list agents once
  - list recent execution runs once with a bounded take
  - group relevant active execution runs by process run id
  - load process-side health metrics for all active run ids in one process service call
  - map existing view models from the batched data
- Update `ProcessWorkspace.RefreshRuntimeWorkspaceAsync` so Runs-tab refresh skips analytics and Analytics-tab refresh still updates analytics.

## Boundaries

- UI remains in Blazor components and presenters.
- Process DB reads stay in Processes services/read-query classes.
- AgentFramework file-backed execution storage stays behind `IAgentFrameworkWorkspaceService`.
- Runtime dispatch semantics remain unchanged.

## Non-Goals

- Do not replace polling with a SignalR push model in this pass.
- Do not rewrite AgentFramework execution storage indexing in this pass.
- Do not redesign process page layout unless measurement shows rendering, not data loading, is the blocker.
