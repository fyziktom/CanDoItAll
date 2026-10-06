# Structure rendering

`StructureWorkspace` owns the real CanvasWorkbench stage, toolbar, toolbox,
accessibility mirror, generic composer and structural dialogs. It takes projected
presentation data and emits typed intents carrying a host context or dialog opening.
It has no Workbench, Projects, persistence, runtime, or application reference.

The native host owns catalogs, dynamic options, original project admission, node
expectations, graph writes, accepted outcomes and reconciliation. Composer opening
notifications capture the native target before editing. Cancel retires that opening;
submission retains its original outcome independently of the next editor. Native
task, secret, text/media and other specialized editors retain their own owners.

Planning, Insights, support windows and deferred dialogs have explicit composition
slots. The canvas itself is not a host fragment. Compatibility type forwards retain
the existing toolbar, toolbox, overlay and catalog presentation names.

Hosts load CanvasLib head/body assets once. This leaf owns its scoped feature styles;
CanvasLib owns the canvas and composer runtime. The three `--structure-*-height`
variables allow representative sandbox chrome without changing native defaults.

The independent `CanDoItAll.Workbench.Structure.UiSandbox` renders this leaf twice.
Its simulated receipts demonstrate rendering and dispatch only. Native persistence
and owner admission are covered by the application component tests and operator
journeys recorded in `docs/architecture/workbench-structure-wb3.md`.
