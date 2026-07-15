# Project-structure Gantt integration

Status: Accepted for implementation, 2026-07-14

## Boundary

The reusable Gantt package is a controlled projection. `CanDoItAll.Modules.Workbench` maps the authoritative `ProjectStructureSurface` into Gantt contracts and applies emitted mutation requests through a focused project-structure schedule mutation service. The component never receives `ProjectWorkbenchService`, `AppDbContext`, or product-domain models.

Canonical task rows are user-authored `ProjectObjectType.WorkItem` nodes with subtype `task`. Persisted dependency direction is `successor DependsOn predecessor`, so the adapter reverses it for conventional predecessor-to-successor Gantt rendering. Only raw `DependsOn` links participate in schedule propagation.

Process/workflow/person/agent decorations are projections:

- Process definitions come from all user-authored `Uses` links, not only a projected visual parent.
- Workflow definitions come from canonical workflow children.
- Person and AI-agent assignments come from `IProjectPartyIntegrationBridge.ListAssignmentsDetailedAsync`; metadata display names are not treated as identity.
- Local participant links are added when a typed node reference identifies the assignee.

## Mutation ownership

`ProjectStructureScheduleMutationService` owns one atomic command path:

1. acquire the existing project mutation lease;
2. reload the authoritative graph;
3. validate editable canonical task nodes and expected dependencies;
4. reject self-links, missing tasks, invalid intervals, and dependency cycles;
5. normalize start/end/duration consistently;
6. replace dependency edges and update affected downstream schedules in one transaction;
7. return the committed surface for reprojection.

For insertion between A and B, where persisted B depends on A, the command removes `B -> A`, adds `X -> A`, and adds `B -> X` while preserving every other prerequisite of B.

Gantt-only view state uses surface kind `gantt`. It must not reuse CanvasWorkbench X/Y coordinates or mutate task data.

## Dependency direction and testability

```text
ProjectStructurePage -> ProjectStructureGanttPanel
ProjectStructureGanttPanel -> ProjectStructureGanttProjectionAdapter
ProjectStructureGanttPanel -> ProjectStructureScheduleMutationService
ProjectStructureGanttProjectionAdapter -> ProjectStructureSurface + party assignment contracts
ProjectStructureScheduleMutationService -> project-structure persistence boundary
```

The adapter and command validator are independently testable without rendering the page. A component smoke test proves the panel emits and applies typed requests. Integration tests prove the transaction rolls back on a cycle or stale dependency and that the surface is reloaded after success.

## Closure gates

- No new product types are added to the shared Gantt package.
- No new partial file is added to `ProjectStructurePage`; the Gantt host is a cohesive child component.
- Existing graph-canvas behavior and dependencies remain unchanged.
- The Workbench project references a unique, locally proven Gantt package version.
- Targeted tests, Web app build, browser proof, and refreshed CodeAnalytics dependency checks pass without cycles.

