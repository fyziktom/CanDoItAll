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
verification passed: plain cycle rejection, NotStarted progress locked at zero, and the
attachment-time size label all match the repaired behavior.

The generic scaffold fix is signed commit `e8747b22e` in the engine. Components and SPA
remain on their separate branches; neither was merged.

Two circuits also passed stale Gantt detail rejection with retained draft, disabled Save and
explicit Read current project. Workflow attachment, all four time scales, zoom, panning and
pointer column resizing passed. File edits passed a separate two-circuit conflict: fresh
readback retained the newer committed value, the rejected draft remained available, and an
explicit manual reconciliation saved against the current revision.

Remaining: structure selection/link/subproject/delete and presentation actions; additional
asset and authoring paths; final readback, browser diagnostics and durable coverage.
Catalog inspection found 88 create entries; inspection alone is not execution proof.

## Native media repair

Native PDF rendering and XLSX cell readback passed. Audio/video and unsupported DOCX/ZIP
attachments exposed blank generic browser frames. FileTools now selects typed native audio
and video controls, keeps autoplay disabled and releases playback and owned object URLs.
Workbench explicitly selects an inert metadata renderer for unsupported formats; it opens
the authorized lease for metadata without reading payload bytes. SVG retains its existing
fully sandboxed frame. No storage, authorization, persistence or project dependency changed.

Build-backed discovery and execution passed all 40 selected FileTools render/lifetime cases,
16 native attachment-dialog cases and 21 native interaction-policy cases, none skipped.
The changed production owners, sandbox and component/unit/browser test owners build in
standard Release. Existing unrelated engine warnings remain. The rendering register passes
for 1,212 inputs. The final portability enforcement passes with 15,712 reviewed findings:
the three intentional additions are explicit ordinal MIME comparisons and a documentation
reference. The reviewed CI checkout label replaces one existing finding without changing
the count. Documentation validation passes for 438 maintained Markdown files.
Native WAV and MP4 playback passed on the rebuilt host: both report their one-second
duration, start only after a control click, and close with playback paused, source removed
and readyState reset to zero. DOCX/ZIP show metadata with no embedded document. SVG renders
its text in the unchanged sandbox; the browser blocks its synthetic script and its script
marker remains absent. Fresh text-file readback retains the explicitly reconciled changes.

FileTools is signed commit `7885f6098e1bc0e202f8e2cc8ee26a9e89945af7` on
`codex/native-media-preview`. CI now records this exact revision and Components
`567b41d922bbbbfe7fdc3c122445357e25698ce3`, so builds consume both repairs. Both feature
branches were pushed and their remote heads verified against these revisions. This record
does not claim a remote CI run. No sibling branch has been merged.

## Runtime validation outcome

An incomplete Console script is now shown as a known validation rejection when the runtime
owner explicitly reports no effect. Unknown and committed outcomes retain their existing
observation requirements. The consumed opening cannot replay; cancelling and opening a
corrected form commits exactly one script. Native browser readback confirmed both absence
after rejection and presence after the corrected submission.

Workbench and the component test owner build in standard Release. All 16 discovered
ProjectStructureComposerLifetimeTests cases pass, none skipped, against the reused isolated
PostgreSQL 18 fixture. The first execution identified an obsolete direct-delete test setup;
the fixture now dispatches the existing admitted native lifecycle command. Final rendering
validation passes for 1,212 inputs and regenerated portability enforcement passes with
15,712 reviewed findings unchanged.

## Evidence and retention

Raw execution records, readbacks, screenshots, exports and test logs remain in the ignored
`.artifacts/concurrent-mutations/blazor-ui-repairs-20261024` directory. `ownership.json`
identifies the retained manual validation project and the one note in the reported project.
Do not copy private application records into maintained documentation. Preserve the ordinary
5032/7271 host for manual testing. No SPA source was changed or merged.
