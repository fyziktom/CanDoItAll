# Workbench planning presentation boundary

WB1 covers the project Calendar, complete Gantt surface and both native task form
families. The Structure canvas, PM panels, runtime/files surfaces and Processes
remain with their current owners. The [UI seam rules](ui-component-seams.md) govern
the extraction. The sections below distinguish the extracted boundary from its
native owners and record each validation checkpoint.

## S0: inherited quote lifetime

Entry was `b059f433aaa6bd6e6795f5c1248b26831458cb36`, with Components
`2eccdddd05a9b1b0c90935ddee49dc559fd5eec1` and FileTools
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`. The intervening main change after
AC1 was the sealed WB1 package. All six AC1 checkpoint signatures verify; the
19 protected renderer graphs and all 247 recorded Agents source hashes match.
The historical AC1 Stable result remains 16,489 passed, one failed, zero skipped.

Nine controlled cases reproduced WB1-Q1 against the original quote widget,
including the actual Gantt form in `DialogHost`: a quote completed after execution
state became ineligible and overwrote the retained amount, availability or error
status. The first attempt's two masked error assertions and all subsequent failed
attempts remain in the private evidence ledger.

The widget now binds each request to project, database profile, project lifetime,
dialog opening, resource, estimate, execution eligibility, resolver and callback.
Every input transition retires the old operation, including A-B-A transitions.
An old completion cannot publish data, errors or busy state into its successor.
The operation disposes its cancellation source only after its resolver unwinds.
A replacement input lifetime also gets a new refresh-button event owner.

The context passed by native coordinators contains presentation identifiers only.
It grants no admission or mutation authority. Native Save still revalidates
assignment, pricing and historical cost through the existing owners. Lazy quote
resolution, valid caching and explicit refresh remain supported.

Current focused proof is 77 quote/form cases, 32 native/component continuity
cases, three real PostgreSQL Scheduler restart cases and four historical pricing
policy cases, all passing with fresh discovery and current assemblies. The two
Gantt detail fixtures now seed and capture a real project lifetime before opening
the form. Scheduler tests verify two replacements and retained future triggers;
the final firing journey remains part of W4. PostgreSQL 18.6 uses a new owned
container, volume and private database; no retained fixture setup was replayed.

CodeAnalytics, Components and dotnetwatch MCP were unavailable. Source inspection,
evaluated MSBuild graphs, actual CLI builds/watch and Playwright provide the
package-authorized fallback. Full artifacts, test filters, discovery counts,
source fingerprints and failed attempts are private under
`artifacts/workbench-planning-wb1/evidence.json`.

## W1: Calendar boundary

`CanDoItAll.Workbench.Planning.UI` owns the complete Calendar rendering, normalized
view-state parser, timezone display and visible-list CSV/XLSX export. It depends
only on neutral BaseLib and CanvasLib (five projects in its evaluated closure).
The native page retains route admission, database reads, project-lifetime checks,
ordered state writes, linked-artifact navigation and Agent context. Its Razor
code-behind is the existing route host, not a second rendering implementation.

Each renderer callback captures its original presentation identity and receiver.
Superseded reads and queued writes cannot affect a successor view. Same-owner
writes remain ordered; accepted writes can finish after navigation. A failed
acknowledgement is shown as unconfirmed and recovery reads durable state without
replaying the write. Stale facts cannot authorize editing or artifact navigation.

The independent planning sandbox has six projects in its closure and owns the
synthetic Calendar specimens removed from the production route. It mounts actual
shared Calendar controls, including independent instances and failure states.
Its Parity and Fast asset modes follow the existing sandbox host convention.
The 19 protected renderer closures are unchanged; Workbench adds only the new
planning library. Calendar's former public parser type is forwarded for compatibility.

Proof includes 24 independent presentation/export cases and 15 native Calendar
cases, including real PostgreSQL ordering, rejected lifetimes and lost commit
acknowledgement. Large-desktop browser checks exercised all five views, saved-view
reload, separate display and browser timezones, the DST fall-back interval, and
decoded CSV/XLSX downloads. Export rows are resolved against the accepted event
snapshot; caller-supplied field values cannot replace those facts. The native
calendar remains read-only. Independent published-host and final consumer proof
will be completed with the full planning family in W4.

## W2: Gantt boundary

The planning library now owns the entire Gantt chart, task drag source, toolbar,
summary and error states, dependency-removal controls, scoped CSS and Mermaid
preview. Title, schedule, dependency, insertion, ordering and both double-click
intents capture the displayed occurrence and receiver. The native panel retains
projection, expected schedules, original project admission, mutations, row-state
persistence and bounded Agent observations. Pure Mermaid encoding moved into the
leaf; its native adapter still supplies the authoritative accepted projection.

Queued callbacks cannot rebind after A-B-A navigation. Accepted writes retain their
result before parent readback; a confirmed commit is never rolled back by a failed
reload. Unknown outcomes block further mutation until a read. Both task editor
coordinators remain native pending the form and exact partial-outcome work in W3.

Large-desktop native proof created real tasks, changed a title, moved a bar, resized
both endpoints, added/reconnected/removed dependencies, inserted a task on an
existing link and changed its row order. Canonical database readback confirms
the exact task and link identities, propagated dates and byte-identical unchanged
neighbor. The actual PNG and Mermaid downloads match the accepted ordered rows;
copy completion and both native dialog entry points were exercised separately.

That journey exposed overlapping shared Gantt canvas updates: a newly saved row
could appear in the table while the canvas retained an earlier model. Components
commit `dc573e2b438621599401a28968acef3682d14e63` serializes interop updates,
retains changes arriving during an await and waits for late creation on disposal.
Three controlled interop failures now pass; all 88 Gantt component tests, routing
and asset verification pass. This signed commit is local only, without a remote
push or package publication. Final image fingerprints must include that source.

## W3: both task editor families

The leaf owns the complete Gantt and general Structure create/edit forms and their
actual estimate, execution, resource-picker and price-preview children. Their
different fields and creation rules remain intact. The two existing native dialog
types are small adapters around those renderers. General create/edit callers use
`ProjectStructureCanvasTaskDialogCoordinator`; Gantt creation uses its native panel
and edits use `ProjectStructureGanttTaskEditCoordinator`. The independent sandbox
mounts those same forms, including two stacked independent openings.

The dependency-free `Workbench.Planning.Contracts` project contains only existing
estimate, execution, progress and resource values shared by the renderer and native
services. Public namespace and assembly-qualified identities are preserved through
type forwarding. The mixed Workbench entity/service models remain native. The real
checked resource-type filter moved to the existing light RecordBrowsing library,
with its previous public identity forwarded. The renderer's evaluated closure has
nine projects and the sandbox ten; all 19 protected existing closures are unchanged.
The recursive assembly guard rejects unresolved or backend implementation edges.

The native opening retains project admission, original assignment revision and
historical pricing basis. The form captures its draft before submission, retires
quotes and prevents duplicate submission. Each adapter closes only its original
dialog reference. Partial/unknown outcomes keep the draft, exact known identities
and individual task, assignment, pricing, attachment, ordering and compensation
facts. Explicit readback performs no write and cannot refresh a replacement view.
It does not grant permission to replay a create or reuse stale expected revisions.

Commit order remains with existing owners:

| Path | Native phases and recovery |
|---|---|
| General create | Revalidate pricing, create task and canvas follow-up, then assign. A returned task identity survives failed placement/readback. Assignment failure compensates creation; both failures are retained if recovery fails. |
| Gantt create | Create the task through the application owner, attach the optional definition/direct assignment and price it, then save row order. Existing compensation removes the task after a later phase fails; cancellation also retains its native recovery receipt. |
| Both edits | Validate original execution/estimate/cost/assignment revision, reload authoritative pricing, optionally replace the direct assignment, then persist task fields. Gantt also validates original title/progress/schedule. Only afterward may an additive definition be attached and priced. |
| Readback | Preserve successful phase receipts before reloading. A failed refresh cannot relabel a task as unsaved or close another opening. |

Controlled PostgreSQL failures exposed two unsafe compensation paths. An edit whose
task write committed but lost its acknowledgement could restore the old assignment
and cost onto the newly saved task. Compensation now compares the captured task
fields under the existing native assignment lock before restoration. A definition
attachment whose pricing transaction committed but lost acknowledgement could be
removed while leaving its price. That uncertain commit now preserves the attachment
and exact identities for readback. Known pre-commit pricing rejection still uses
native compensation. The stale Gantt exception contract is retained alongside its
compensation facts; no new durable operation protocol or schema was introduced.

Failing-first raw-input tests also cover invalid dates, progress, effort and cost;
unknown execution; null versus zero; sub-minute timestamp precision; queued saves;
late quote/input changes; and cancellation. Correcting invalid cost no longer rescales
man-day effort. Native reopening formerly rounded estimate decimals and converted
UTC due dates to server-local minutes. Its task projection now retains exact values.
The actual 1920 by 1080 browser journey created and edited through all four entry
points, rejected invalid raw due text, reached both footers and saved with keyboard
focus. Canonical readback preserved exact task metadata/schedules, zero GBP historical
cost after starting execution, all unrelated task rows and both existing links.

Fresh proof passes 251 native family cases and all 44 independent planning cases.
This includes the original quote matrix, native assignment/attachment/compensation
tests, the real coordinator/dialog outcome tests and public identity/wire checks.
The earlier 247/248 run, two native reopening failures and a discovery attempt blocked
by the owned app's locked output files remain separate failed evidence. Current
source and delta secret matches are reviewed against entry; private credentials,
TRX, screenshots and full logs are excluded from tracked delivery.

## W4: independent hosts and current census

The current caller census is bounded by behavior, including the native adapters
that intentionally remain in Workbench:

| Presentation | Production caller and retained owner |
|---|---|
| `PlanningCalendarSurface`, time display, parser and list export | `ProjectCalendarPage` retains route admission, canonical event reads, ordered view-state writes, linked-artifact navigation and Agent context. The obsolete `ProjectEventsCalendar` compatibility wrapper has no new active caller. |
| `PlanningGanttSurface` and Mermaid export | `ProjectStructureGanttPanel` retains canonical projection, mutation admission, original schedules, accepted identities, row ordering and Agent observations. It is still mounted by the native Structure host. |
| `PlanningGanttTaskEditor` | The existing `ProjectStructureGanttTaskDialog` adapts create from the Gantt panel and edit from `ProjectStructureGanttTaskEditCoordinator`. |
| `PlanningStructureTaskEditor` | The existing `ProjectStructureTaskCreateDialog` adapts general create and edit from `ProjectStructureCanvasTaskDialogCoordinator`. |
| Estimate, execution, resource picker, quote preview and save outcomes | Both extracted forms use the actual shared children. Native callbacks retain assignment revisions, rate/history reads, attachments, compensation and readback. |
| Synthetic specimens | The independent sandbox mounts these same controls and forms with explicit synthetic owners. Calendar boundary demonstrations remain separate from production capability. |

The leaf owns its scoped form/Gantt styles and `planning-download.js`. Shared
CanvasCalendar, Gantt, Mermaid, dialogs and inputs retain their Components owners.
Final evaluated graphs still contain nine renderer projects and ten sandbox
projects; all 19 protected closures match entry exactly. No backend or database
services are registered by the independent sandbox.

Separately published Fast and Parity hosts both passed the 1920 by 1080, scale-one
browser campaign. This includes both form families, raw invalid input, long titles,
keyboard-accessible footers, independent stacked openings, held quotes, historical
zero cost, partial/unknown/refused results, explicit readback, Calendar failure and
stale states, and two independent calendars/Gantts. Physical Gantt title, move,
both resizes, dependency add/remove/reconnect, insertion, ordering, double-click
and disposal/remount gestures passed on both hosts. UTC and Asia/Kathmandu display
state remained independent of the browser's America/Los_Angeles timezone.

CSV and XLSX contain the accepted visible rows. The actual Mermaid copy/download
matches the preview source; SVG rendering and decoded PNG exports passed. All 507
published shared assets match between hosts, including 166 files compared directly
with their source owner. Planning/Contracts/Gantt/Calendar/Mermaid assembly bytes
also match between the two publications. No browser errors or missing assets were
observed. Earlier selector, clipboard-permission, drag-coordinate and overlong-title
test failures remain separate evidence; the real validation rejection was preserved.

Three measured edits of each kind used the actual `dotnet watch` process:

| Edit | Observed milliseconds to visible result | Observation |
|---|---|---|
| Razor | 915, 925, 913 | Automatic browser navigation; same server process. |
| C# | 530, 467, 428 | Automatic navigation and a newly opened Mermaid preview; same server process. |
| CSS | 822, 685, 705 | Computed border changed in place, without navigation. |
| JavaScript | 318, 711, 834 | Actual Calendar download filename changed after automatic navigation. |

Every probe was restored byte-for-byte, including a final ordinary download after
JavaScript restoration. Initial incomplete instrumentation and file-sharing delays
are retained separately. The custom watch configuration does not define `DEBUG`,
so its generation counter is not evidence against hot reload. Baseline native-app
and independent-host paths differ; these observations do not establish a controlled
speedup. The warm sandbox build took 6.45 seconds and startup 12.428 seconds.

The native campaign uses a newly owned source/two-client topology and durable
PostgreSQL 18 storage. No retained AC1 fixture, ordinary application or historical
setup was reset. The first image, `bbd64e4f66fa388767b706756676d7486f37ee60cd45972c387305314f4bd7ac`,
contains the W3 source plus the recorded five end-of-file cleanups and Components
`dc573e2b438621599401a28968acef3682d14e63`. It retains its original input label and
all first-image evidence. The later API documentation repair described below has
its own fresh image and consumer campaign.

Source and both clients use the same image and relevant assembly/JavaScript bytes.
Across Linux image and Windows publications, 332 assets are byte-identical and 160
compression variants decode identically. Fifteen generated CSS variants differ
only in line endings and a verified one-to-one scope mapping; the native DOM uses
the matching scopes. Generated application CSS matches source and Parity exactly.

The first-image operator/Agent journey passed through Projects UI, Gantt creation,
exact task-update approval, native file write/attachment/readback/download and an
explicit denied attachment. The canary was read from storage, never supplied in
the prompt. Canonical reads retain the task identity, exact schedule/decimal
metadata and unchanged neighbors. Refreshed Gantt and persisted Calendar List show
the accepted title; the actual CSV contains both the task and dated meeting.
Resources independently lists the same project file, and Workspace API status
remains lazy. Both clients also exercised default and nondefault published routes
through native provider controls with separate local identities.

The first planning browser attempts retained a start/end update race and a missing
metadata-read option in the harness. A later attempt stopped at the file-only
evidence reader before approval. Its exact pending proposal was reviewed and
approved once; native schema validation then refused numeric enum arguments with
a `NotCommitted` receipt. The final test uses named enum arguments and passes
without weakening target, approval, receipt or canonical-data assertions. An
explicit evidence-reader option adds only task-update inspection for an owned
single-project task grant; its default file allowlist still refuses that tool.

Both first-image saved Workflow cases pass: accepted output creates the exact native artifact,
while known-incomplete output cannot reach the asset executor. The finite enabled
Scheduler plan survives two native application restarts with identical run/history;
the future plan fires once and the completed plan remains enabled. Original image,
container, database and accepted version identities are retained throughout.

The frozen Stable checkpoint found a real extraction regression: the moved
`ProjectTaskEstimate` XML comments were intact, but the new contracts project did
not emit its documentation file. Enabling the same documentation output as the
original Workbench assembly restores the generated API descriptions. A fresh Web
build and isolated `WB1Docs` Integration build pass; all nine unchanged API
documentation pipeline tests pass, including the failing cross-assembly case.
The frozen run and its old output files remain separate evidence.

The replacement image is
`b58ca3d21e219d5355d2feae017d789afe63bc727f1662e7ea20caac37e917e7`, with exact input
fingerprint `8f6b6249f1a57f1277a915e6b0b0cb85dc793f76b43ff36fe9814ff97fde07ab`.
Only the three owned app containers were replaced. Database and upstream container
identities, data mounts and saved fixtures were retained. Of 4,588 frozen production
files, the sole later change enables XML output in Planning.Contracts. The native
Workbench, Planning, Contracts and Gantt DLLs and their JavaScript remain byte-identical;
the Web DLL changes for generated API documentation. Fresh Fast and Parity publications
each retain all 556 previously tested files byte-for-byte and add only the XML file.

The native Runtime/Usage attempt first reached the intended partial state: this
fresh profile required explicit usage-index initialization. The existing maintenance
command initialized its derived index, without migration/rebuild flags or model
calls. All 394 canonical JSON payloads stayed byte-identical. The original failed
grid assertion is retained; it was not weakened to accept an empty view.

Resuming the provider journey exposed a browser helper's substring selection:
it selected the saved `PP2 OpenAI image generation` profile when the original
`OpenAI image generation` was requested. The helper now matches the name exactly
while retaining the first occurrence among the tree's legitimate tag duplicates.
The failed substring attempt, over-strict single-occurrence assertion and native
DOM evidence remain recorded. Native provider identities/defaults assertions stay
unchanged. These test-only deltas are outside the application Docker input.

All nine final-image native cases pass with fresh exact discovery: source/two-client
provider defaults and routes; Runtime/Usage cancellation and reopening; floating
chat detach/follow/close; History request identity and content authorization; the
original file journey; the planning operator/Agent journey; two accepted/incomplete
Workflow cases; and finite enabled Scheduler restart. The planning journey creates
its project through Projects UI, preserves the exact task schedule and decimals,
approves the exact task-update payload, and observes the accepted title and progress
through native Gantt, Calendar, CSV and persisted List view. File read/write/attach,
canonical content readback, denial and actual download retain their exact receipts.
The two file journeys report no browser errors or failed requests.

The final Scheduler plan stays enabled through two application restarts with the
same accepted version, single run and complete detail. A future neighbor fires
once, with no duplicate run or provider dispatch. A separate read-only check also
confirms that the first-image completed plan survived the image replacement and
these additional restarts: its original run and detail remain identical.

Final client-A Usage is complete: 50 canonical observations across eight Agents
match the API and all three native detail dialogs, including 900 tokens and the
recorded decimal cost. All 50 observation files remain unchanged. The accepted
seven-day window, independent fourteen-day view and independent dialog closing
pass. Source and client B retain honest empty, partial indexes; no initialization
was performed there merely to produce complete badges. The original failed native
run remains represented in Usage. No paid calls were made, and the unchanged full
19-vector shared-provider protocol campaign was neither replayed nor claimed.

## Regression and delivery

New shared contracts, native recovery behavior and test/CI registration justified
one frozen Stable checkpoint. It is still running; later repairs have separate
fresh proof and do not rewrite its results. In addition to the API documentation
repair, the old Workspace status test clicked a handler retired by startup reads.
It now waits for both native reads to settle before clicking; its exact two-case
selection passes with unchanged lazy-read and reopening assertions.

Final portability enforcement passes without baseline-write mode: all 15,227
reviewed executable-source findings match. The sole W4 baseline delta is the
reviewed evidence-reader condition fingerprint, with no allowance-count change.
The portability, secret-tooling and package contract tools pass 38 self-tests.
Final documentation/secret-delta closure and signed delivery remain pending the
frozen regression disposition. All browser validation uses 1920 by 1080 at scale 1.

Remaining Workbench cuts are PM/read panels, the main Structure canvas and
inspectors, assignment/runtime/files and Processes. None is started by WB1.
Release readiness is outside this assignment.
