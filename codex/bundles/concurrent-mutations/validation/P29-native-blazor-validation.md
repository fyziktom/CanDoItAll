# Native Blazor validation — P29

Status: in progress. This records observed native UI behavior, not a substitute for the
remaining action matrix. The ordinary application and its retained database were used only
for explicitly owned manual records. Automated tests used temporary SDK workspaces.

## Process recovery

The reported run initially failed implementation without an escalation. The generic
scaffold planner repair and its 40 selected passing tests are recorded in
[P29](../plan/P29-native-blazor-repair-and-validation.md). Rework was requested through the
standard Live Processes UI after inspecting the prior effects. Existing product files were
reconciled rather than scaffolded again.

On 2026-10-24 at 13:06 UTC the run completed successfully. Its current durable summary is
Succeeded / Complete, with no completeness warnings, failed steps, incidents or escalations.
Contract resolution, implementation, runtime QA and final project writeback completed;
unselected branch steps are not counted as executed. QA produced browser and offline proof.
The implementation and QA retained fresh build and four product-test receipts.

A note created through Project Structure during execution survived final writeback. A
second browser circuit committed a newer edit while the first retained a draft: saving the
stale draft was rejected, its text remained in the editor, and canonical readback retained
the newer value. The stale editor was cancelled after capturing evidence.

## Native action evidence so far

| Surface | Observed result |
| --- | --- |
| Projects | Create project and open its structure passed. |
| Structure | Create note, edit from a second circuit, stale rejection and draft retention passed. |
| File attachment | Upload text, metadata inspection and exact 115-byte SHA-256 readback passed, including Unicode. |
| File interaction | View, edit, undo, redo, preview and revision-aware Save passed. |
| Gantt creation | Add tasks with person/agent assignments, delivery and effort; double-click empty timeline to create at the selected UTC time passed. |
| Gantt editing | Inline rename, row reorder, bar movement and both endpoint resizes passed. |
| Gantt dependency | Drag output to input passed; moving/resizing the predecessor shifted its successor while retaining successor duration. |
| Gantt export | Mermaid source download and PNG download passed; the PNG was inspected. |
| Floating agent | Native chat read the project and attached file and created its own note. A second execution ran 13:10:49–13:11:27 UTC; a UI note was created at 13:10:49.968 UTC. The agent updated only its own note and canonical readback retained both changes. |
| Image attachment | Attach the native Gantt PNG, open its authorized preview, zoom/reset and maximize passed; decoded image dimensions match the export. |
| Text authoring | Create Markdown and Mermaid assets from text and render both through the governed file interaction passed. |
| Progress summary | Inline Review status and stored XLSX/Mermaid exports passed. |
| Structure conversion | Convert a note to Decision, retain its text and edit its headline/context passed. |
| Task details | Double-click opens the authoritative editor; missing actual start is rejected; a valid Started state and 40% progress save. Attach a process definition while retaining the agent assignee passed. |

## Gantt disposal defect

The initial browser sequence also exposed an asynchronous component cleanup defect: a
render callback and disposal raced while the floating agent completed after navigation
away from Gantt. The ordinary host logged a null-reference exception in
`GanttTaskDragSource.DisposeRegistrationAsync`; the managed runtime health correctly
reported that error. Successful data writes above do not imply that the original UI
lifetime was healthy.

Components commit `b6a1b6e9` on `codex/native-gantt-lifetime` serializes registration and
cleanup per drag-source instance. No application dependency or mutation boundary changed.
The owning library and tests build in standard Release with zero warnings/errors. All
24 discovered lifecycle cases pass: six new drag-source cases and 18 existing chart
cleanup/disposal cases. The initial test observation used a render-triggered waiter for
an interop-only completion; it was corrected to await explicit completion signals.
Four Gantt/Canvas navigation cycles on the rebuilt Debug host passed with healthy runtime.
Native source/target reconnection, insertion of an eight-hour task into a dependency,
successor reflow and dependency removal passed. A cyclic request left the graph unchanged,
but exposed a server stack trace in the UI; the follow-up repair below addresses that display.

## Follow-up display repair checkpoint

The existing Gantt owner now catches its typed cycle-validation exception before dispatch
and displays its business message. Both add and reconnect rejection are covered, including
zero host callbacks, unchanged controlled data and a subsequent valid correction. All 55
discovered `GanttChartComponentTests` cases pass, none skipped. Its owning library and tests
build in Release with zero warnings/errors using the engine's pinned SDK.

The native task editor derives visible progress from the existing execution policy for
NotStarted, Completed and Cancelled, while preserving the raw editable draft and original
comparison baseline. All 16 discovered `ProjectStructureGanttTaskDialogTests` cases pass,
including seven new progress cases, none skipped. Planning.UI and Workbench production
owners and the owning component test assembly build in Release; existing unrelated warnings
remain. The attachment-size label now describes its attachment-time metadata accurately.

The rendering register passes for 1,212 current inputs after reviewing and refreshing only
the two affected native entries. A regenerated scan including untracked files passes final
portability enforcement with 15,709 reviewed executable-source findings unchanged. Browser
verification of these three display changes is pending on the refreshed ordinary host.

The generic scaffold fix is signed commit `e8747b22e` in the engine. Components and SPA
remain on their separate branches; neither was merged.

Remaining: plain cycle-message browser verification, stale task details and workflow resource
attachment; structure selection/link/subproject/delete and presentation actions; additional
asset and authoring paths; final readback, browser diagnostics and durable coverage.
Catalog inspection found 88 create entries; inspection alone is not execution proof.

## Evidence and retention

Raw execution records, readbacks, screenshots, exports and test logs remain in the ignored
`.artifacts/concurrent-mutations/blazor-ui-repairs-20261024` directory. `ownership.json`
identifies the retained manual validation project and the one note in the reported project.
Do not copy private application records into maintained documentation. Preserve the ordinary
5032/7271 host for manual testing. No SPA source was changed or merged.
