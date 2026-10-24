# P29 — Native Blazor repairs and concurrent editing proof

Status: in progress. Proof tier: Behavioral, with retained real-host/process evidence.
This is a focused successor to P28. It does not close the remaining refresh-delivery work.

## Owned user input

> ok. lets focus on our standard blazor UI now and repairs and proper testing.
> first I tried to run process **Run 1657350f** and it got into escallation. it used to be able to run e2e without escallations. it is simple app development (it must be generic for any type of app, not just the example we use).
> then I need you to try all actions in the project structure and gantt including attaching assets, reading those infos, etc.
> repair bugs on the way.
> then you must also test the concurrency like, there is running process or chat with floating agent and same time you add something via UI into project structure, etc. we need to be sure it is working correctly via our standard UI.

## Requirements and progression

| Requirement | Acceptance and proof | State |
| --- | --- | --- |
| BUI-1 | Diagnose the reported run; repair generic engine causes; prove the normal process path through runtime validation and result recording, with receipts for existing effects before any rework. | Passed: Succeeded / Complete, zero escalations |
| BUI-2 | Inventory every action exposed by native Project Structure and Gantt; exercise each applicable action, assets and information readback; record disabled prerequisites and repair failures. | In progress; action evidence in validation record |
| BUI-3 | Use a real process and floating agent chat while editing the same project through native UI; verify non-conflicting writes survive, stale writes are handled explicitly, drafts and assets remain correct after reload. | Passed: both real overlaps and stale note, file and Gantt editors |
| BUI-4 | Keep tests, docs and incremental signed commits current; preserve the SPA review branch and ordinary data. | In progress |

Work order: generic process repair and focused regression proof; native action inventory
and repairs; overlapping process/chat/browser proof; final static checks and closure.
Browser checks use the existing ordinary host and explicitly owned manual test records.
Automated database tests use the retained isolated PostgreSQL fixture, never the ordinary
database. No global cleanup, deployment, or SPA merge is part of this work.

## Initial evidence and first repair design

Run `1657350f-61a0-4acc-95de-37a6ba26bfa9` is durably Failed, with zero escalations.
Its first step completed. Implementation execution
`5e59661a-cf57-488f-b481-a17e63cd021e` performed 65 tool calls and submitted a valid
typed completion, but completion assessment retained an unresolved `workspace_dotnet_new`
failure. Three scaffold calls were recorded: solution creation succeeded; an application
under that solution root was prohibited; a `name="."` alternative was rejected as invalid.
Subsequent file/build/test/publish effects must be reconciled before rework.

`WorkspaceCommandPlanBuilder.BuildDotnetNew` treats a top-level solution file as an
existing project when creating an application. It also declares a child target but passes
only `-n`, without an output directory. A local SDK `--dry-run` with a generic console
template proves that this writes `./Program.cs` and `./<name>.csproj` into the parent.

The smallest coherent repair stays with the command planner:

1. Keep the parent-project prohibition, but allow child projects under a solution root.
2. Explicitly pass the child output directory for non-solution templates, keeping solution
   files in their declared parent and retaining all path/overwrite guards.
3. Classify `.` and `..` scaffold destinations as prohibited operations with proven no
   effect, directing callers to repair existing projects or choose a named child. Other
   invalid input remains a correctable rejection. Do not weaken global completion rules.

## C# architecture impact

Ownership remains `AgentFramework.Core/Workspace/Commands`; typed effect evidence remains
owned by the existing execution/effect contracts. No new project, reference, interface,
partial class, composition registration, or dependency on a product example is needed.
Changing only the parent guard was rejected because it would allow the CLI to write outside
the declared child target. Ignoring all failed mutations was rejected because unknown or
unresolved writes must continue to block success.

CodeAnalytics baseline `snap-20261024123248-fe315e8b` loaded one Core project, 266 documents,
804 types and 5,945 members with no diagnostics. The scoped graph reports three existing
type cycles and no project cycles. This repair must introduce no dependency changes;
the existing cycles are not a reason to expand this behavior repair into a Core refactor.

## Testability and gates

First repair selection:

- Unit: `FullyQualifiedName~WorkspaceCommandExecutionServiceTests.DotnetNew`, including
  child output, solution-root admission, existing-target refusal, prohibited dot paths,
  and ordinary invalid-input rejection. Build-backed discovery froze 21 cases.
- Host integration: a dedicated `WorkspaceDotnetScaffoldingTests` class using the real
  SDK and local process host. Verify solution plus at least two distinct project templates,
  actual output paths, unchanged solution root, and refusal to overwrite a scaffold.
  Build-backed discovery froze two cases (`console` and `blazorwasm`).
- Completion regression: `FullyQualifiedName~ToolOutcomeCompletionIntegrationTests`;
  no-effect prohibition must not erase uncertain effects or ordinary unresolved mutations.
  Build-backed discovery froze 17 cases.
- Direct Core build and refreshed owning Unit/Integration test assemblies in standard
  Debug/Release outputs, serialized against managed watch. No unfiltered suite is justified
  by this local planner contract; expand only if a newly affected owner requires it.
- Final portability scan includes untracked protected files, reviewed deltas and passing
  enforcement without baseline write; rendering register and docs checks remain required.

Native UI proof targets 1920×1080. Preserve the existing structure/Gantt primary surface,
editor/dialog ownership and scroll behavior; inspect normal and relevant overlay states.
API reads support receipt verification but do not substitute for native UI interactions.
Add focused component/owner tests for bugs found during BUI-2/BUI-3, naming their filters
and expected discovery before execution. Entry gate passes for the first repair only;
later lanes need their concrete action inventory and prerequisite readback before writes.

## Scaffold repair checkpoint — 2026-10-24

The Core Release build and the owning Unit/Integration Release builds pass. All 40
selected cases pass: 21 unit cases, two real-SDK host cases and 17 completion regressions;
none were skipped. Existing unrelated compiler/analyzer warnings remain. The host tests
removed their unique temporary workspaces after execution.

Forced post-edit CodeAnalytics snapshot `snap-20261024124337-fe315e8b` has no diagnostics.
All 1,074 dependency edges (including their kinds and weights) match the baseline, with
the same three type cycles and no project cycles. No module boundary changed.

The portability scan included untracked files. Final enforcement passes with 15,709
reviewed executable-source findings unchanged; no baseline refresh was needed. The
baseline, artifact-secret and rendering validator tests pass (6 + 4 + 7 cases), the
rendering register passes for 1,212 inputs, and documentation validation passes for 438
maintained Markdown files. Logs and TRX files are retained under the ignored proof root.

The ordinary managed host was stopped only after confirming there were no active runs,
then restarted in standard Debug with its explicit 5032/7271 endpoints. These automated
checks establish the scaffold repair; they do not close process E2E or native UI proof.

## Native Gantt lifetime repair entry

During native Gantt-to-Canvas navigation with a floating agent completing, the host logged
`NullReferenceException` in `GanttTaskDragSource.DisposeRegistrationAsync`. Its render
callback and component disposal both used the mutable registration field across awaits.
One cleanup cleared the field while the other still expected it. A pending registration
could also complete after disposal and retain its browser listeners.

Repair the existing shared Gantt component on `codex/native-gantt-lifetime` in Components.
Serialize registration/replacement/disposal with one component-local async gate, mark
disposal before waiting, and detach the owned registration before awaited cleanup. Preserve
the current parameters, markup, JS protocol and application mutation owners. No cross-module
reference, global lock, database access, new service or CSS is justified. The gate covers
only this component's interop lifetime and does not serialize users or project mutations.

Prove delayed registration versus disposal, replacement cleanup versus disposal, parameter
changes during registration, and cleanup failure/resource release with controlled interop.
Build the Gantt library and its test owner in standard Release, discover and run the bounded
lifetime selection, then rebuild the ordinary host in Debug and repeat native navigation
and drag/drop. Existing Gantt scheduling tests remain relevant to the final interaction lane.
The installed Components MCP was queried; it has no GanttTaskDragSource catalog entry, so
the owning component, existing Gantt lifetime tests and current consuming surface supply
the contract. No component selection or composition changes are needed.

## Follow-up display corrections

Native checks found three bounded presentation defects. A rejected cyclic dependency
reaches the UI as an interop exception with a server stack trace; catch only the planner's
typed `GanttScheduleException` before host dispatch, show its safe business message and
keep later valid interactions usable. Test add and reconnect cycles with zero host writes,
unchanged controlled inputs and a subsequent accepted request. Unexpected host failures
must still propagate and be logged.

The Gantt task editor initializes its "Actual progress" input from the stored canvas hint
(28 for Draft), while the bar and summary correctly apply execution state (0 for NotStarted,
100 for Completed and -1 for Cancelled). Keep the original edit model and local editable
draft unchanged; derive only the visible value through the existing execution-progress
policy, including explicit execution-state changes. Lock the input for those three states.
Test five initial states, execution-state transitions preserving the editable draft, and
unchanged submission of the stored hint through the real native dialog host. This avoids
altering the compare-and-swap baseline or silently persisting a display-only normalization.

The inspector's attachment size comes from the immutable attachment reference, not a fresh
storage stat after content edits. Label it "Size when attached" rather than claiming current
size. This preserves the storage boundary and avoids a remote stat on every selection or
render. Current content and revision checks remain owned by governed file interaction.
No new dependency, service, network call or CSS is needed for these corrections.

Build-backed discovery will freeze the GanttChartComponentTests class (including both
cycle cases) in Components and ProjectStructureGanttTaskDialogTests (including seven
progress cases) in the engine before filtered execution. Build their production owners
and test assemblies in Release; refresh rendering hashes only after reviewing the two
changed native rendering inputs, then regenerate and enforce the portability scan.

## Retained evidence and resources

### Native media and unsupported-file repair entry

Native PDF rendering and XLSX cell readback passed. WAV and MP4 instead resolve to the
generic browser fallback, whose fully sandboxed iframe fails to load the nested blob in
Chromium. DOCX and ZIP also show blank fallback frames rather than an explicit unavailable
preview. This is a renderer/composition defect, not a storage or concurrency failure.

Keep bounded byte leases, object-URL ownership and the sandbox for SVG unchanged. Add typed
Audio/Video targets and media profiles in FileTools' existing object-view owner, using
native controls without autoplay and explicit decode-error handling. Do not relax iframe
permissions or put codecs, storage drivers or product dependencies into FileTools.

Provide an opt-in metadata fallback renderer through the existing composition builder;
Workbench and its Content sandbox select it explicitly. The library's existing generic
browser fallback remains available to other hosts. Unsupported Workbench files show their
metadata and host-owned open/reveal actions without reading payload bytes or embedding a
document. Fix host notices to describe native media controls and the existing SVG sandbox.

Before filtered execution, discover FileObjectUrlInteropTests and FileInteractionRenderTests
in FileTools plus ProjectStructureAttachmentPreviewDialogTests in the engine. Cover native
media targets, load/error/replacement/disposal, profile selection, encoded metadata without
payload reads and unchanged SVG isolation. Build the changed production owners in standard
Release; then rebuild the ordinary host in Debug, verify WAV/MP4 metadata/playback and
DOCX/ZIP inert metadata, and rerun the required rendering/portability/docs gates.

Ignored proof root: `.artifacts/concurrent-mutations/blazor-ui-repairs-20261024`.
It contains ownership and the reported process/agent records. Keep raw application data
out of commits; commit the reasoning, generic tests and bounded validation results.
Use the shared DotNetWatch backend for useful hot reload. The final managed watch rebuild
stalled; its owned sessions were stopped. Retain the ordinary application using the same
standard Release outputs and development launch profile after validation. Keep SPA on
`codex/concurrent-mutations`, without source edits or merge.

### Native runtime validation outcome repair entry

Submitting a Console script with neither a command nor a path reaches the runtime metadata
validator, which returns typed `NotCommitted` evidence before the node write. The generic
composer ignores that evidence and presents an unconfirmed write. Recognize only the
existing explicit no-effect exception states in its outcome classification; retain unknown
or committed outcomes as observation-required. Keep the consumed opening and original
authority rules: correction uses a fresh opening, and a replay cannot write again.
Use the real native service and isolated database to prove rejection, absence of a created
node, the actionable validation message, ignored replay and a successful corrected opening.
Retain the existing lost-reply and stale-lifetime regression cases. No new UI/domain
dependency, retry loop or validation bypass is needed.

### Native date conversion repair entry

Meeting creation reached PostgreSQL with a nonzero local offset and was rejected before
insertion. Normalize parsed form dates to UTC in the existing create composer and node
editor, including work-item due dates. Keep omitted optional values and unchanged edit
echoes intact, preserving their original precision and compare-and-swap baseline. Invalid
nonblank creation dates must fail explicitly rather than silently becoming null. No domain,
database, provider or project dependency change is needed. Prove positive and negative
offsets, local input, missing and invalid values, unchanged precision and real native
meeting persistence/readback; build Workbench and discover the bounded owning tests.

### Object-index graph action repair entry

The index selects a node while reconnect is armed and bypasses the original captured
dependency owner when connecting. Route both intents through the same existing reconnect
and dependency workflows used by the canvas. Preserve their captured source, admission,
expected graph/node state, outcome handling and one-shot ownership. Add native-host tests
for both actions and stale-source rejection through the index callback. No new mutation
service or renderer-side persistence is needed.

### Duplicate prompt projection repair entry

Creating Flow, Session or Step persists a native node bound to its prompt artifact; the
gallery contributor then adds a second node for the same artifact. Suppress only redundant
unreferenced gallery projections. Retain a legacy projected identity if it has a saved
layout, a canonical child or a user link, so existing graph references cannot disappear.
Build hash sets once per assembly from the already loaded nodes/links; add no database
roundtrips or writes. Preserve standalone gallery prompts and canonical node kinds, bindings
and positions. Prove each retained-reference case and the ordinary single-node case.

### Floating-window default placement

Toolbox and Object index both default to the same left position, obscuring the controls.
The Components MCP recommends the existing CanvasFloatingWindow/OverlayWindow owner; its
runtime currently implements only left and right defaults. Add the existing placement
protocol's top-center option in OverlayLib and select it for Object index. Saved coordinates,
clamping, safe toolbar inset, resizing and narrow-container behavior take precedence exactly
as before. Verify three simultaneous windows at the supported desktop size, manual drag,
reset and reload through the native UI. No product CSS or additional overlay owner is needed.

### View-state disposal repair entry

Native navigation logged SemaphoreSlim.Release after disposal while an index selection was
still persisting. Canvas callbacks already participate in the page's authoring-operation
drain; an Insights callback reaches the same persistence gate without that tracking. Track
the actual persistence task at its shared entry point, reject retired entries before gate
access, and retain the existing ordered writes and disposal drain. Do not hide disposal
exceptions or remove serialization. Hold an index-originated database write, dispose the
rendered page, prove disposal waits, then release and verify the accepted selection. Retain
the existing window-order, navigation and graph lifetime cases in the focused selection.

Closure: BUI-1 and BUI-3 are proven. BUI-2 final coverage and BUI-4 closure remain in progress.
Historical cleanup denials and open P28 work remain recorded in preceding validation documents.

### Native hierarchy acknowledgement follow-up

The browser found that a successful Add subproject leaves its accepted recovery record
blocking the next reconnect. Preserve the shared pending-operation guard. The native host
may clear only its own exact, durably acknowledged record after the original opening has
reconciled and published a fresh surface. Retired openings, failed refreshes, storage errors
and concurrent record changes keep recovery evidence and an explicit recovery message.
Reuse the existing revision-checked clear operation; add no persistence or dependency layer.
Validate consecutive add/reconnect, retained uncertain acceptance, failed evidence cleanup,
and existing late-completion/disposal cases before repeating the browser sequence.

### Explicit canvas root focus

Native Focus root selects a child when several project-shaped nodes precede the active
project. Keep their shared appearance and ordering. Add an optional root identity to the
existing CanvasWorkbench surface contract; Workbench maps its typed ActiveProject role to
that identity. An explicit missing identity must not focus an unrelated node. Existing
hosts without an explicit root retain their current selection rule. No persistence,
service, project reference or product dependency belongs in Components. Prove active-project
mapping with both parent and child projections, component dispatch and missing identity,
then native focus with both hierarchy contexts after the rebuilt host is ready.
