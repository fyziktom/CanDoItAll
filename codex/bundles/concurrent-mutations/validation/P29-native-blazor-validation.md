# Native Blazor validation — P29

Status: behavioral acceptance passed for the documented Windows desktop lane. Exact
Python-folder cleanup remains blocked. The reconciled action matrix and operation coverage
below define the proof's scope and limits. The ordinary application and retained database
were used for authorized process recovery and explicitly owned manual records; automated
tests used temporary SDK workspaces and the separate PostgreSQL 18 fixture.

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

## Native action evidence

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

The later checkpoints below extend this initial evidence to structure, hierarchy, content
and presentation actions. The create catalog contains 88 entries; inspection alone is not
execution proof.

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

## Date, graph and projection repair checkpoint

Generic create/edit dates now preserve the entered instant as UTC. Invalid nonblank create
dates fail explicitly; unchanged edit echoes retain the original precision. Online and
onsite meetings save through the native forms. Changing a meeting to 12:15–13:45 local
time persisted 16:15–17:45 UTC; a work-item due date at 09:30 persisted 13:30 UTC.

Object-index reconnect and dependency actions now use the canvas's existing original-owner
workflow. Native reconnect, dependency creation and parent disconnection passed, with
canonical graph readback. Unreferenced duplicate gallery projections are suppressed while
saved layouts, canonical children and user links retain their legacy projected identity.
The three native Flow/Session/Step records now appear once with their correct node kinds.

Build-backed discovery and final execution passed 40 composer/graph lifetime cases, 13 node
editor cases and nine owner projection integration cases, none skipped. The first graph
execution found an obsolete direct-delete fixture; the corrected fixture uses the captured
native lifecycle command. Standard Release builds passed for Workbench, Insights.UI,
OverlayLib and all three test owners; existing unrelated engine warnings remain. Rendering
validation passes for 1,212 inputs and portability enforcement for 15,712 reviewed findings.

Components commit `39d6cac3b156068dd5de7aa4d069218612325918` adds centered default placement
to the existing floating-window runtime. Native reset places Toolbox, Object index and
Inspector separately at the supported desktop viewport; saved coordinates remain supported.
The signed commit was pushed on `codex/native-gantt-lifetime`, and the engine CI pin follows
it. No sibling branch has been merged. The ordinary rebuilt host is healthy.

Additional native evidence: Python environment creation with an owned temporary environment;
clipboard node id readback; canvas image export saved as an attached artifact and decoded
at 1,730 × 952 pixels.

## View-state disposal repair

Diagnostics exposed a selection save releasing its semaphore after page disposal. The
shared view-state persistence entry now tracks its actual operation in the existing page
drain and rejects retired entries before touching the gate. A controlled database-write
test proves disposal waits for an index selection, then releases cleanly and retains the
committed selection. All 25 discovered graph-lifetime cases pass, none skipped. Workbench
and its component test owner build in standard Release. Rendering passes for 1,212 inputs;
regenerated portability enforcement passes with 15,712 reviewed findings unchanged.

Native follow-up also passed JSON/log/plain-text authoring, screenshot attachment, a new
synthetic Generic vault record and its reference, and one configured image-provider
generation through the native dialog. The file browser's filter and inline read-only view
retain the reconciled text and Unicode. Stored summary exports render their Review/78%
workbook row and the unscheduled Mermaid timeline. Its empty action menu is reported as
unavailable; no download action is claimed for that collection.

## Consecutive hierarchy edits

Native descendant transfer created an owned child with exactly one moved Evidence node.
Creating and attaching a second owned project succeeded. Its immediate reconnect exposed
an accepted recovery record blocking the next edit. The host now clears only its own exact
accepted record after reconciliation and publication by that original reload. A superseded
reload cannot acknowledge another read. The shared pending-operation guard is unchanged;
retired openings, failed retention, failed refresh and changed or failed cleanup retain
recovery evidence. Cleanup failure still reports the write as committed.

All 28 discovered hierarchy-lifetime cases pass, none skipped, including consecutive
add/reconnect, acceptance retention failure, cleanup failure and retirement during refresh.
The first 27-case execution passed before the explicit publication callback and additional
retirement case. Final Workbench and component-owner Release compilation passed with
existing warnings. Rendering passes for 1,212 inputs and regenerated final portability
enforcement passes with 15,712 reviewed findings unchanged. On the rebuilt host, native
recovery observed and cleared the original accepted record without resubmitting it.
Three consecutive native parent reconnections then committed successfully, ending with
the owned leaf directly under the validation project. No pending acceptance blocked the
next operation. Signed engine checkpoint: `5cef0fe19`.

## Final action extensions

Native copy/paste created one Decision branch under Architecture. Cut/paste moved that
same copied branch under Backlog, preserving its identity and content; native deletion then
removed only that test copy. Earlier system clipboard id/tree readback passed. Final Copy
info displayed its success receipt, but system clipboard roundtrip could not be verified
in the final browser session: a direct browser-only synthetic write/read also returned an
empty value. This is a recorded environment limit, not a claimed clipboard-content pass.

The screenshot asset's metadata and notes were edited, its parent changed to Architecture,
and its preview still decoded at 1,552 × 184 pixels. Delete node and file then removed the
canonical node and returned 404 for its authorized content route. JSON, log and plain-text
previews retained their markers and Unicode; the generated image decoded at 1,024 × 1,024.

Create transcript saved a scaffold and its recording relationship with the explicit notice
that no audio recognition was requested. The three provider actions saved a summary, the
requester's Friday checklist task, and Avery's Thursday delivery separately while preserving
the synthetic source text. These actions save analysis; they do not create project tasks.

The Console script's native Run launched `dotnet --version` in the owned fixture directory,
returned SDK 10.0.303 and exited with code zero. Open terminal acknowledged its presentation
launch; its exact owned PowerShell process was identified and stopped after use. No native
terminal pixel inspection or elevated launch is claimed.

Help pages, menu-size adjustment and restoration, minimap, diagnostics, zoom, maximize/dock
and recomposition passed. Fit showed all 100 current nodes at 18% in the live diagnostics.
These are functional observations, not a performance benchmark.

## Explicit root focus repair

The last control check found Focus root selecting a child project because every related
project uses the root visual family. Components now accepts an optional root node identity
from its host. Workbench supplies the typed ActiveProject identity without changing node
order or appearance. A missing explicit identity does not select another node; existing
unbound hosts retain their selection rule. No persistence or project references changed.

Components signed commit `cd50ee735bc27b90a845402844b1391559984bfd` is pushed on
`codex/native-gantt-lifetime`; the engine CI pin uses that exact revision. All 12 discovered
root-focus/state-commit cases pass, none skipped, with a zero-warning Release build. The
first execution exposed a test fixture without the focus toolbar enabled; the corrected
fixture enables the real control. All six discovered engine adapter cases pass, none
skipped. The owning component test assembly and Web production graph build in standard
Release with existing warnings and no errors. Native Focus root selects the active project
both from the parent project (with two children) and from a child (with its parent present).
The moved Evidence node remains readable in that child's inspector.
Final regenerated portability enforcement passes with 15,712 reviewed findings unchanged.

## Floating-window lifetime repair

Reset followed immediately by Hide reopened Signals in all three native trials. Captured
browser events confirmed both button clicks, 79 ms apart. Three controlled regression
cases reproduced geometry publication while Hide was pending, while hidden and after
disposal had begun. OverlayWindow now rejects those late notifications; ordinary and
minimized geometry still publish, and a host rejection of Hide still propagates and leaves
the visible window usable. The guard is component-local and adds no dependency or lock.

Components signed commit `14cf7b15fe0902cfc0b460e661aa30b809d03edb` is pushed on
`codex/native-gantt-lifetime`; the remote head and engine CI pin match. The exact filter
`FullyQualifiedName~OverlayWindowLifetimeTests|FullyQualifiedName~LayoutNavigationOverlayBehaviorTests`
discovers 15 cases: six lifecycle cases and nine existing state/contract cases. Before the
repair, the three race cases failed; the final standard Release build has zero warnings or
errors and all 15 pass without skips. The Web production graph also builds in Release,
with 194 existing warnings and no errors. Its copied OverlayLib binary matches the tested
library hash. This component correction alone did not close the native rapid sequence:
the host also retired the renderer origin after a view-state echo, dropping queued Hide.
The existing viewport update now shares one helper with the other three view-state updates,
preserving only the exact original content/selection/actor/lifetime context. Data changes
and navigation continue to invalidate old actions. The focused native-host proof includes
geometry followed by a queued hide and rejection against a successor project.

The first 35-case host execution exposed two obsolete recreated-project fixture calls and
the remaining feedback-only origin invalidation. The fixture now deletes through its
captured native lifecycle submission. Save feedback updates the selection presentation
without retiring an otherwise-current origin; any prior content, selection, actor or
lifetime change still prevents that reuse. The corrected standard Release Web build passes
with 183 existing warnings and no errors. The refreshed component test owner passes with
215 existing warnings and no errors. These include existing NuGet vulnerability warnings;
this repair does not resolve the repository's package-upgrade work.

Build-backed discovery for
`FullyQualifiedName~ProjectStructureGraphLifetimeTests|FullyQualifiedName~ProjectStructurePageActionLifetimeTests.Insights_`
finds exactly 35 cases. All 35 pass, none skipped, including both new queued-window cases,
selection A/B/A, forged targets, recreated lifetime rejection and disposal during persistence.
Final browser proof passes three rapid Reset/Hide sequences, minimize/expand/hide and two
Canvas/Gantt round trips, ending on Canvas with no page errors. The persisted maximized
canvas was docked before using its underlying view tabs. The final host logs contain no
unhandled, null-reference or disposed-object errors.

Fresh portability enforcement passes with 15,712 reviewed executable-source findings
unchanged; no baseline update was needed. Rendering validation passes for 1,212 inputs.
The portability-baseline, artifact-secret and rendering-validator checks pass 6, 4 and 7
cases respectively. Final canonical readback retains the successful process, both overlap
markers, four persisted task schedules, one dependency, reconciled Unicode file edits,
transcript outputs and transferred child Evidence. The fifth task remains explicitly
projected until its schedule is edited. The 88-entry creation matrix reconciles again.
Documentation validation passes for 438 maintained Markdown files. The maintained bundle
secret scan covers 1,010 text files with zero findings and no oversized or unreadable skips.

## Creation catalog and operation coverage

The [88-entry creation matrix](P29-native-create-action-matrix.csv) reconciles native
receipts with final canonical records. There are 87 successful creations: 85 remain
readable, while the screenshot and Python nodes were explicitly deleted after their proof.
The POSIX shell entry was rejected as unsupported on this Windows host before insertion.
The inventory comprises 23 blocks, 19 assets, 12 runtime entries, nine infrastructure
entries, six people, six work items, four meeting/recording/transcript entries, three prompt
types, and two each for assurance, capture and planning. The native Task toolbox dialog was
exercised separately from Gantt task creation.

| Operation group | Observed result and boundary |
| --- | --- |
| Structure editing | Typed creation, metadata/notes edits, note conversion, block type change and reversal, markers/priority/progress modes, copy/paste, cut/paste and deletion passed. |
| Graph relationships | Connect, source/target reconnect, disconnect, descendant transfer, add existing/new subproject and consecutive parent reconnects passed; fresh child readback retains moved Evidence. |
| Navigation and inspection | Object-index search/selection, active-root focus in parent and child, child structure tab and project-scoped Test Lab navigation passed. Open on a generic record retains its inspector; specialized assets use their preview. |
| Assets | All 19 create entries are accounted for. Native metadata, authorized content, supported previews, text edit/history/save/conflict recovery, file browsing/filtering, export and delete-with-file behavior passed as detailed above. Unsupported DOCX/ZIP previews show explicit metadata. |
| Process | The reported run completed through normal implementation, QA and writeback. Native Estimate reached nine resolved roles with provider pricing; Start showed the correct task target and was cancelled before another costly execution. |
| Workflow | An owned deterministic three-step fixture was prepared through the definition API, then linked, launched and observed through native Blazor controls. It completed 3/3; observing the original start retained the same single run. The existing shared file-diff definition was only reviewed. |
| Transcript | Recording-to-transcript scaffold, source text edit, summary, requester tasks and others' deliveries passed; no speech transcription or task creation is implied. |
| Runtime | Console direct execution returned SDK 10.0.303/exit zero; terminal presentation was acknowledged and its owned process cleaned up. Python environment creation passed. Other runtime records establish creation/readback, not execution of every configured runtime or elevation mode. |
| Gantt | Creation, inline/detail edits, assignees and process/workflow attachment, ordering, move/resize/reflow, dependency add/reconnect/insert/remove, cycle rejection, execution progress, scales/zoom/pan/columns/table toggles, exports and stale-draft recovery passed. |
| Concurrency | Real process/UI and agent/UI overlaps retained both changes. Two-circuit note, file and Gantt stale writes were rejected with retained drafts and explicit current-state recovery. |

The workflow fixture uses no provider, network, external command or external file path.
Its run is `4e0c61d3-43d7-46f4-8c93-080253a86d5a`; canonical runtime records and project
status agree on Completed, three of three steps, with persisted executor artifacts.
The Start dialog intentionally retains an Observe original start action after acceptance;
an initial automation wait for automatic closure was corrected to inspect that receipt.

This Windows desktop lane does not claim external infrastructure provisioning, payments,
message delivery, elevated/UAC execution, every configured runtime launch, remote CI, an
unfiltered full suite or cross-platform native UI coverage. Creation of those typed records
is distinct from performing their external effects. Final Copy info content verification
remains limited by the browser/system clipboard roundtrip described above. These explicit
limits do not replace the covered mutation and stale-write contracts.

## Resource closure limits

The temporary Python node is deleted. Its seven-file test environment remains at
`.artifacts/concurrent-mutations/blazor-ui-repairs-20261024/asset-fixtures/python-env`.
Automatic approval review rejected the original cleanup attempts and the user's explicitly
authorized retry with "blocked by policy". No cleanup success is claimed for that folder.
The exact target, owned host and stopped build/terminal processes are recorded in
`ownership.json` and companion inventories. Retained manual projects and synthetic vault
reference support the user's follow-up testing; ordinary databases and installed tools
remain intact. Managed watch rebuilds stalled, so final manual testing uses the ordinary
standard Release no-build host with its existing development profile and database.
The existing shared isolated PostgreSQL 18 fixture remains reserved for open P28 work;
P29 created no replacement container, volume or network. Its current loopback endpoint and
retained database inventory are recorded separately from the ordinary development database.

## Evidence and retention

Raw execution records, readbacks, screenshots, exports and test logs remain in the ignored
`.artifacts/concurrent-mutations/blazor-ui-repairs-20261024` directory. `ownership.json`
identifies the retained manual validation project and the one note in the reported project.
Do not copy private application records into maintained documentation. Preserve the ordinary
5032/7271 host for manual testing. No SPA source was changed or merged.
