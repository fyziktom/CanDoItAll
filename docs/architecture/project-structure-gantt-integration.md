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

## Task creation and row ordering extension

Status: Accepted for implementation, 2026-07-15

The Gantt package exposes two additional interaction intents and still owns no product mutation logic:

- an empty-timeline double click identifies the row task and snapped UTC instant;
- a row-order request identifies the moving task, an anchor task, and before/after placement.

The package does not name either interaction "create task", does not supply default duration, and does not reorder its controlled `Tasks` input. `CanDoItAll.Modules.Workbench` opens the task dialog and applies both operations through authoritative project services. Stable task identifiers are used instead of row indexes so stale requests can be rejected after a controlled refresh.

New unsorted tasks are canonical `WorkItem/task` nodes beneath a canonical root child `ProjectBlock/backlog` titled `Main`. The mutation creates that backlog when absent. A timeline-created task is placed after the clicked row; toolbar-created tasks append after the last task. Row order is a Gantt-only persisted view state keyed by stable task node ids; reordering never rewrites project-structure parentage, Canvas coordinates, or dependency semantics.

Optional task resources use typed selections:

- people and AI agents are canonical node-scoped `WorkItemAssignee` assignments through `IProjectPartyIntegrationBridge`;
- workflows use the existing workflow-node service to create the canonical workflow-definition child;
- processes use the existing typed process-definition `Uses` relationship and never duplicate a catalog definition as an unrelated custom node.

The reusable searchable card picker belongs to `CanDoItAll.AppComponents`. It receives typed items and selectors, supports optional favorite affordances, and owns only search/filter/selection presentation. Product modules remain responsible for loading catalogs, persisting favorites, and applying selections. Existing project-structure person/agent, process, and workflow dropdowns migrate to this picker; process-start agent selection remains on its already richer specialized picker.

### Interaction and visual thesis

The schedule stays the visual hero. One compact `Add task` action joins the existing utility row, row-order arrows stay inside the task-name cell, and the task dialog uses a dense large-screen form followed by an optional searchable card grid. Utility copy explains that double click creates at a time and that dependencies remain unchanged.

Duration is one controlled value expressed in hours. Start plus duration derives end; an explicit end edit recalculates duration. Presets (`1 h`, `4 h`, `1 d`, `1 w`) use the configured eight-hour man-day and five-day work week. Invalid or non-positive intervals block submission instead of being silently normalized.

### Dependency direction and testability

```text
GanttChart -> typed timeline/order intents
ProjectStructureGanttPanel -> task dialog orchestration
ProjectStructure task application service -> ProjectWorkbenchService
ProjectStructure task application service -> IProjectPartyIntegrationBridge
ProjectStructure task application service -> workflow/process services
ResourceCardPicker<TItem> -> presentation delegates only
```

Component tests prove controlled event emission and synchronized dialog fields. Application tests prove backlog creation/reuse, insertion order, append order, assignment/resource integration, and stale reorder rejection. Browser proof covers double click versus pan, toolbar append, presets, typed resource selection, vertical scrolling, and row movement.

### Pattern selection record

The selected design is a typed interaction-intent boundary plus a focused application service. The reusable component emits immutable Gantt contracts; the Workbench panel orchestrates the dialog; `ProjectStructureTaskCreationService` owns lease-protected canonical mutations and explicit compensation. `ResourceCardPicker<TItem>` is a generic presentation component whose callbacks leave catalog loading and persistence with the consuming module.

A strategy registry or provider plugin layer was rejected. The four resource kinds are a closed product contract and an exhaustive enum switch is smaller, easier to audit, and produces a compile-time change point when a fifth kind is introduced. Direct persistence from either Razor component was also rejected because it would split the project structure's source of truth.

The dependency direction is intentionally inward: Gantt package -> typed intents, Workbench UI -> application services, application services -> existing project/workflow/process/party boundaries. No application or persistence type flows back into the reusable Gantt or picker components.

### Concurrency and failure closure

Each mounted Gantt panel owns a unique, stable `ProjectStructureAgentContext`. Task creation and row-order changes pass through `ProjectStructureGanttRowOrderService`, which serializes mutations per project, acquires the project mutation lease, and keeps raw Gantt view-state writes internal. Concurrent task creation therefore reuses one canonical `Main` backlog, and concurrent row changes cannot lose a persisted update.

Row movement carries the task id, expected anchor id, and before/after placement into the leased store mutation. The current persisted order must contain that exact adjacency before the pair is swapped; stale requests fail with a typed conflict and do not write state.

Task creation compensates any resource or row-order failure by deleting the partially created task through the canonical mutation path. Token-driven cancellation performs the same cleanup with a non-cancelable compensation token and then preserves the original cancellation. Party assignments and workflow/process links are removed by the existing cross-module delete pipeline. Sensitive party contact fields are redacted at the application-service boundary before picker options are constructed, while non-sensitive contact descriptions remain available.

