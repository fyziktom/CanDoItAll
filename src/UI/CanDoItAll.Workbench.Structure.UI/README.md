# Structure rendering

`StructureWorkspace` owns the real CanvasWorkbench stage, toolbar, toolbox,
accessibility mirror, generic composer and structural dialogs. It takes projected
presentation data and emits typed intents carrying a host context or dialog opening.
It has no Workbench, Projects, persistence, runtime, or application reference.

`StructureChangesWindow` renders a nonmodal update inbox from an immutable presentation.
Its refresh, item-selection and geometry intents retain the original context identity.
The native Workbench observer owns subscription lifetime, cursor, self-filtering and
refresh acknowledgement. The Structure sandbox includes remote-update and read-failure
scenarios using the same renderer, including a change affecting multiple nodes.

The native host owns catalogs, dynamic options, original project admission, node
expectations, graph writes, accepted outcomes and reconciliation. Composer opening
notifications capture the native target before editing. Cancel retires that opening;
submission retains its original outcome independently of the next editor. Native
task, secret, text/media and other specialized editors retain their own owners.

Existing node comparison uses typed text, number, date and keyed choice values.
`StructureNodeComparisonDialog` renders original/local/current and a local result;
it owns no reads or writes. Unfinished result input blocks Apply and Refresh and
stays mounted when unchanged fields are hidden. A refreshed comparison carries
choices only for an unchanged triple and retains the previous result for copying.
Apply and Undo edit the existing composer; the native Save remains separate.

The host supplies the native close decision and current raw composer snapshot to
`StructureComposerLeaveGuard`. Continue retains the same controls; discard must
still match the opening, input revision, comparison and submitted outcome. Canvas
remains mounted across view switches. Planning and Insights mount on first visit
and remain until the context changes. `EditorMessage` and `EditorActions` render bounded status text and native Undo
above the keyboard hint, outside the toolbar so neither can shrink its controls.

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
