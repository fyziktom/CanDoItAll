# 03A - Internal Workbench Tabs, Visual Project Structure, and Calendar Orchestration

## 1. Purpose

This document closes three gaps that are too important to leave implicit:

1. the internal tabbed workspace model
2. the project structure canvas based on the playlist-builder canvas architecture
3. the project events calendar based on the events-calendar canvas architecture

These are not optional enhancements. They are part of the core operating model of the application.

PromptStudio is expected to become a daily project-control workstation. Without an internal tab system, a recoverable workbench state, and visual project orchestration surfaces, the application will not scale comfortably in real use.

## 2. Why internal tabs are mandatory

The application uses Blazor Web App with Interactive Server rendering. That brings a specific operational reality:

- every browser tab creates another circuit
- every open browser tab keeps its own render tree and state
- heavy workbench screens can multiply memory and connection pressure quickly
- browser-level tabs are a poor substitute for application-level workflow management

Therefore, the product must provide its own internal tab workspace so users can keep many work items open inside one browser tab while the application controls resource usage deliberately.

## 3. Workbench goals

The workbench must let a user:

- open many project-related work surfaces without opening many browser tabs
- switch quickly between active tasks
- reorder tabs by workflow importance
- move inactive tabs into a low-cost sleeping state
- restore the whole workspace after browser close, crash, reconnect, or refresh
- deep-link into project artifacts while still opening them inside the internal tab workspace
- keep visual work such as canvases and prompt sessions tied to the same project context

## 3A. Workbench shell model

```text
+-------------------------------------------------------------------------------------------+
| Top bar: workspace | project | phase | search | provider health | background tasks       |
+-------------------------------------------------------------------------------------------+
| Left rail           | Internal tab strip                                                  |
| - dashboard         | [Overview] [Structure*] [Calendar zZ] [Prompt Session] [+] [...]   |
| - projects          +---------------------------------------------------------------------+
| - prompt gallery    | Main workbench surface                                              |
| - prompt factory    |                                                                     |
| - validation        | Structure tab: canvas + outline + inspector                         |
| - test lab          | Calendar tab: widget + detail drawer                                |
| - settings          | Prompt tab: wizard + preview + linked artifacts                     |
|                     |                                                                     |
|                     +---------------------------------------------------+-----------------+
|                     | Right drawer / inspector / actions / save state   | notifications   |
+-------------------------------------------------------------------------------------------+
```

Legend:

- `*` means dirty
- `zZ` means sleeping
- `[...]` means overflow or restored tabs menu

## 4. Internal tab model

### 4.1 Core concepts

The architecture should model four logical states:

- `Opened`: currently mounted and interactive
- `Background`: not active, but still eligible to stay warm
- `Sleeping`: not mounted, state reduced to a snapshot
- `Closed`: removed from the session

The implementation can use interfaces, records, or discriminated models, but the semantics must exist explicitly.

### 4.2 Recommended contracts

The design should include contracts conceptually equivalent to:

```csharp
public interface ITab
{
    string TabId { get; }
    string TabKind { get; }
    string Title { get; }
    Guid? ProjectId { get; }
    bool IsDirty { get; }
    bool CanSleep { get; }
    string RestoreKey { get; }
}

public interface IOpenedTab : ITab
{
    ValueTask<TabSnapshot> BuildSnapshotAsync(CancellationToken cancellationToken = default);
    ValueTask OnActivatedAsync(CancellationToken cancellationToken = default);
    ValueTask OnBackgroundedAsync(CancellationToken cancellationToken = default);
}

public interface IBackgroundTab : ITab
{
    DateTimeOffset LastActiveAtUtc { get; }
}

public interface ISleepTab : ITab
{
    TabSnapshot Snapshot { get; }
    DateTimeOffset SleptAtUtc { get; }
}
```

The exact shape can change, but the application must preserve:

- stable tab identity
- typed tab kind
- restorable snapshot
- dirty-state awareness
- explicit sleep eligibility

### 4.3 Required supporting services

The solution should include services conceptually equivalent to:

- `ITabHostService`
- `ITabRegistry`
- `ITabPersistenceStore`
- `ITabSnapshotSerializer`
- `ITabSleepPolicy`
- `ITabWakeCoordinator`
- `ITabDeepLinkResolver`

These services belong to the workbench architecture, not to arbitrary page components.

## 5. Tab lifecycle rules

### 5.1 Opening rules

When a user opens an artifact:

- if the exact tab already exists, activate it instead of duplicating it
- if the request is a deliberate duplicate, open a new instance with a clear instance token
- opening from search, activity, notifications, canvas, or calendar must route through the same tab host

### 5.2 Backgrounding rules

When a tab loses focus:

- keep lightweight pages warm when cheap
- mark heavy pages as sleep-candidates
- flush unsaved UI state into a tab snapshot before sleeping

### 5.3 Sleeping rules

The sleep policy should be deterministic and user-visible.

A tab may be auto-slept when:

- it is not active
- it is marked sleep-capable
- it exceeds configured idle thresholds
- background jobs tied to it are long-running
- the shell crosses a configured open-tab pressure threshold

A sleeping tab must keep only:

- route identity
- project context
- essential UI state
- last known selection or focus target
- any required rehydration keys

It must not keep heavy component graphs alive unnecessarily.

### 5.4 Wake rules

When a tab wakes:

- restore the last meaningful local state
- refresh data if the snapshot is stale
- show a clear indicator if background data changed while it slept
- avoid silently dropping dirty local state

### 5.5 Close rules

When a tab closes:

- warn on unsaved changes
- allow close, close-others, close-right, and close-all-background
- preserve pinned tabs unless the user explicitly closes them

## 6. Persistence and crash recovery

### 6.1 Local storage is mandatory for tab state

Tab session state must be persisted in browser storage through an explicit abstraction. The design must not rely on in-memory server state only.

Recommended abstraction:

- `IBrowserWorkspaceStateStore`
- default implementation backed by `localStorage`

### 6.2 What must be persisted

Persist at minimum:

- workspace session id
- ordered tab list
- active tab id
- pinned tab ids
- sleeping tab snapshots
- unsaved-draft references
- last selected project
- shell preferences relevant to tab restore

### 6.3 Versioning rules

Persisted state must include:

- schema version
- app version or compatibility marker
- snapshot timestamp

If persisted state becomes incompatible, the app must degrade safely instead of failing the whole shell.

### 6.4 Restore rules

On startup or reconnect:

- attempt to restore the previous internal tab session
- validate each snapshot
- reopen compatible tabs
- mark incompatible tabs as recoverable failures in a restore report
- never lose the whole session because one tab snapshot is invalid

## 7. Workbench UX rules

The shell must provide:

- a visible internal tab strip
- active, background, sleeping, dirty, and pinned indicators
- reorder by drag or keyboard shortcut
- tab search or overflow menu when many tabs are open
- a sleep badge or icon that is understandable without a tooltip
- explicit restore feedback after crash or reconnect

The tab strip must not become a raw browser-tab imitation. It should behave like an enterprise workbench.

## 8. Default tab kinds

The first release should be designed for tab kinds such as:

- dashboard tab
- project overview tab
- stack profile tab
- resources tab
- prompt detail tab
- prompt factory session tab
- validation run tab
- test evidence tab
- project structure canvas tab
- project events calendar tab
- settings tab

## 9. Project structure canvas

### 9.1 Core architectural decision

The project structure surface should reuse the canvas strategy documented in:

- `C:\repositories\CanDoItAll\docs\canvas-playlist-builder\README.md`
- `C:\repositories\CanDoItAll\docs\canvas-playlist-builder\rebuild\blazor-jsinterop-component-plan.md`

Do not rebuild the renderer in C# for version one.

Use a Blazor wrapper around a reusable JavaScript canvas engine and keep the heavy rendering, hit testing, viewport math, and drag behavior in JavaScript.

### 9.2 What the project structure canvas represents

This is not only a visual tree of folders. It is the operational graph of project work.

The canvas should support nodes such as:

- project root
- phase
- milestone
- feature or epic
- prompt blueprint
- prompt session
- prompt step
- validation run
- test plan
- test evidence
- resource
- decision
- note
- external link

Connections should support semantics such as:

- depends on
- uses
- produced by
- validates
- blocks
- follows
- derived from

### 9.3 Prompt-wizard integration

Prompt execution is one of the main reasons this canvas exists.

The project structure canvas must support:

- displaying prompt sessions as nodes
- displaying prompt steps as child nodes or linked nodes
- branching from any step into a new follow-up prompt
- linking each step to source resources, templates, and prior outputs
- showing status such as draft, running, waiting, failed, validated, superseded

This is how complex prompt chains remain explainable and reusable instead of dissolving into ad hoc chat history.

### 9.4 Required host surfaces around the canvas

The canvas needs more than the canvas itself. The workbench should include:

- an outline tree
- an inspector pane
- a command toolbar
- context actions
- status badges
- save and autosave indicators
- selection details
- linked-artifact quick actions

The canvas engine stays in JavaScript. These supporting surfaces should live in Blazor.

### 9.5 Required behaviors

The project structure canvas should support:

- selection
- multi-selection if the engine can support it safely later
- collapse and expand
- pan and zoom
- context actions
- keyboard navigation for primary commands
- autosave of layout state
- fit-to-view
- persistent node positions
- restore of selected node and viewport after reopen
- undo and redo at the workbench-command layer

### 9.6 Persisted canvas state

Persist separately:

- semantic project structure
- visual layout state
- viewport state
- selection state
- collapse state

Do not confuse visual arrangement with semantic ordering.

## 10. Project events calendar

### 10.1 Core architectural decision

The calendar surface should reuse the strategy documented in:

- `C:\repositories\CanDoItAll\docs\canvas-events-calendar\README.md`
- `C:\repositories\CanDoItAll\docs\canvas-events-calendar\rebuild\blazor-jsinterop-component-plan.md`

Version one should wrap the full JavaScript widget instead of partially rewriting it.

### 10.2 Calendar purpose in this product

The calendar is not generic decoration. It is the scheduling surface for project delivery work.

It should track events such as:

- phase windows
- milestone dates
- review sessions
- prompt deadlines
- validation deadlines
- release windows
- test runs
- follow-up reminders
- linked external meetings or delivery checkpoints

### 10.3 Required linkages

Calendar entries should be linkable to:

- project phases
- project structure canvas nodes
- prompt sessions
- validation runs
- test plans
- evidence artifacts

### 10.4 Required behaviors

The calendar should support:

- day, week, month, year, and list views
- CRUD for events
- drag and resize
- timezone-aware display
- linking and unlinking to project artifacts
- opening related artifacts in internal tabs
- export hooks
- persisted view preferences per project

## 11. Shared component strategy updates

The component system must now explicitly cover:

- `AppTabStrip`
- `AppTab`
- `TabOverflowMenu`
- `DirtyStateDot`
- `SleepStateBadge`
- `SaveStateIndicator`
- `CanvasWorkbenchShell`
- `WorkbenchInspectorPane`
- `WorkbenchOutlinePane`
- `CommandBar`
- `TimelineChip`
- `DateTimeEditor`
- dialog and overlay foundations
- validation-aware field wrappers

These additions are necessary to make the shell and workbench credible. They should be planned as part of the component roadmap, not as incidental page-local markup.

## 12. Performance and resource rules

Because the application uses Interactive Server rendering, the following rules matter:

- only the active heavy tab should keep full interactive state when practical
- sleeping tabs should release expensive render trees
- canvas data transfer between .NET and JavaScript should be incremental where possible
- restore should prefer snapshots plus targeted data refresh over reconstructing every screen from scratch
- tab persistence should write small snapshots frequently and larger snapshots deliberately

## 13. QA and release gates for this area

The product is not ready if any of the following remain missing:

- internal tab open, close, reorder, and restore
- sleep and wake lifecycle with visible UX
- local-storage-backed workspace restore after refresh or crash
- project structure canvas wrapper with stable typed contracts
- prompt-step branching represented visually and reopenable
- project calendar wrapper with artifact linking
- tests covering tab restore and heavy-tab sleep behavior

## 14. Delivery conclusion

The internal tab workbench, project structure canvas, and project events calendar are core product features.

They should be implemented with these principles:

- wrapper-first reuse of proven JavaScript engines
- explicit tab lifecycle control
- local-storage-backed restore and crash recovery
- strong typed contracts at the Blazor boundary
- clear linkage between project artifacts, prompt sessions, validation, and scheduling

If these capabilities are treated as later polish instead of core architecture, the product will be technically functional but operationally weak.
