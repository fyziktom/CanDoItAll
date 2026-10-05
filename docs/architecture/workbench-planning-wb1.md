# Workbench planning presentation boundary

WB1 covers the project Calendar, complete Gantt surface and both native task form
families. The Structure canvas, PM panels, runtime/files surfaces and Processes
remain with their current owners. The [UI seam rules](ui-component-seams.md) govern
the extraction. This is a staged implementation record, not a completed boundary.

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

## Remaining WB1 work

The final W4 operator/Agent/Workflow/multi-instance campaign, independently published
Parity/Fast sandbox and repeated development-loop measurements remain pending.
Large-desktop validation uses 1920 by 1080 at scale 1. New shared contracts, owner
recovery behavior and test/CI registration justify one frozen Stable checkpoint after
the final source is settled. Later changes require explicit affected proof. The
historical AC1 Stable failure remains a historical failure, never a fresh pass.

Final closure also requires refreshed static/docs/secret gates and final image/asset
fingerprints. Remaining Workbench cuts are PM/read panels, the main Structure canvas
and inspectors, assignment/runtime/files and Processes. None is started by WB1.
Release readiness is outside this assignment.
