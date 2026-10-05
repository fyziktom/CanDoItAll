# Workbench Insights WB2

WB2 is complete for the bounded Insights and Selection family, including the
rendering boundary, native consumer campaign and reviewed regression closure.
The original broad run retains four failures: three have passing focused repairs;
one is the unchanged historical synthetic secret-scan control. This is not a
passing full-suite claim or release readiness. The sealed package is
`codex/bundles/CanDoItAll_Workbench_Insights_WB2`; original attempt logs and manifests
are retained under ignored `artifacts/workbench-insights-wb2`.

## Entry and boundary decision

Entry main is `23393e402cc2f4c48d1338a7602586461f00ff73`; its changes after the
reviewed WB1 checkpoint only add the WB2 package. Components entry is
`dc573e2b438621599401a28968acef3682d14e63` and FileTools is
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`. All three checkouts were clean before
the new regression tests. The previously completed Agents, Workflow and WB1
boundaries remain preservation constraints.

CodeAnalytics, Components and dotnetwatch MCPs are unavailable in this session.
Source inspection, evaluated MSBuild restore graphs, CLI builds and actual
Playwright browser interactions provide the corresponding evidence. Entry graphs
and native/planning watch file sets were captured before moving renderers.

The new `CanDoItAll.Workbench.Insights.UI` leaf owns the full Manager Summary,
Activity, Selection Panel, Object Index, Signals, Canvas Health and advanced detail
renderers and their CSS. BaseLib, CanvasLib and Charts remain the owners of their
real child widgets and JavaScript. No main Structure canvas or Processes renderer
is included in this extraction.

The native Workbench module retains report scope resolution, bounded queries,
arithmetic, runtime profile selection, process cursors, access checks and all
project/task/file/runtime effects. A small Insights.Contracts assembly holds
only shared report values; native preflight, process cursor and mixed workbench
model types stay with their existing owners. Public moved values retain their
namespaces and explicit type forwards.

Each view gets its own session and cancellation lifetime. Settled report values
and options may be restored from the bounded profile/project store, but pending
drafts, confirmation openings, activity cursors and in-flight work cannot be shared
between panels. Report charts and activity use the accepted report's exact options
and time window. Typed projections and captured origins cross the UI boundary;
native entities, services and raw metadata do not.

Selection and support actions capture the project lifetime, selection generation,
exact target IDs and offered action before dispatch. The native adapter rechecks
that origin and passes the original admission to mutation owners. Current selection
must never substitute for the targets of an old callback. File preview, Mermaid,
task editing and runtime execution remain explicit native actions.

A new independent Insights sandbox and test project exercise the same real
renderers without constructing the Workbench module. This project boundary is
needed because moving files inside the module would retain its Foundation, MAF,
Processes and application dependency graph. A generic service bag, new parent
partial-class facade or wholesale move of mixed contracts would not establish the
required boundary.

## Checkpoints and proof plan

1. S0: failing-first Gantt cleanup, exact shared owner repair, source/published/native
   close/remount proof, signed Components checkpoint.
2. W1: complete reporting and Activity, independent stale-result and lifecycle
   tests, native arithmetic/preflight/cursor proof, signed checkpoint.
3. W2: all selection/support renderers and native captured actions, child and
   mutation/refusal/partial-result tests, signed checkpoint.
4. W3: Fast/Parity source and publish, three reversible watch samples per owned
   file kind, native operator/Agent/file/Workflow/Scheduler/multi-instance campaign,
   final dependency/census/static gates and signed closure.

All new visual validation uses 1920×1080 at device scale 1. New owned resources use
the WB2 artifact root and isolated build configurations; the ordinary application
at port 5032 and historical fixture data are not used. No paid model calls, pushes,
merges or releases are authorized. New shared contracts, solution/CI registration
and native composition changes are the named trigger for one broad Stable gate
after the final source set is frozen. Earlier checkpoints use focused owning tests.

## S0 observations

The first helper attempt incorrectly read a nested interop field and is retained
as a failed proof attempt. With that helper corrected, unmodified production code
failed eight of fourteen new cases: six pending/already-completed cancellation and
fault cases, combined update/cleanup failure, and independent chart cleanup.

Disposal now observes the pending operation, independently attempts cleanup and
preserves the original exception (both exceptions if cleanup also fails). Actual
browser removal also reproduced a null element reference during delayed disposal.
A per-instance identity now releases the acquired JavaScript chart after its DOM
element has been removed. The callback reference is released in the outer finally.

All 102 tests in the Gantt owner assembly pass, with build-backed discovery and
pre-run DLL/source manifests. Source Fast, independently published Parity and the
real native WB1 host pass held-update close/remount. The two-chart sandbox leaves
the other chart usable and applies the newest queued scale. Table/canvas task
identities and dimensions agree. Browser screenshots were inspected at the stated
desktop size. Served Gantt JavaScript bytes match the changed Components source
across all three hosts. Initial portability enforcement passes with 15,227 reviewed
findings unchanged. These results establish S0 evidence only; W1–W3 remain open.

## Reporting checkpoint

Insights.UI owns the complete Summary and Activity renderers and their per-view
sessions. The small Insights.Contracts assembly contains only reporting values;
native scope resolution, analytics, arithmetic and Process continuation protocols
remain in Workbench. The host retains settled snapshots by the original bounded
profile/project store and checks actor and project lifetime before restoring them.
Each scope admission is checked before and after native reads, including descendants.

Draft changes leave accepted chart scales, cost meanings and report cutoff intact.
Unloaded Activity totals display an em dash. A failed reload retains and qualifies
the accepted values. Each opening owns its native cursor/aggregate cache; replaced
read progress, errors and finally blocks cannot publish into the current view.
The completing operation releases its cancellation source after unwinding.

At this checkpoint, 13 independent reporting cases and 11 native component cases
pass with fresh discovery and pre-run source/DLL hashes. The real application
reports the existing task's 6.5-hour schedule and separate EUR future cost and
preserves its report over tab disposal. The independent large-desktop browser
checks all four Activity types, 20-row pages, 43 all-match and 11 filtered totals,
fresh reopening, delayed replacement, confirmation and retained failure state.
Four actual chart instances render across two independent views, without page errors.
Screenshots were inspected. Native all-source arithmetic, cursor/as-of continuity,
publish, watch and final multi-instance journeys remain part of W3; this checkpoint
does not close those proof groups or the WB2 family.

The four added portability findings are the sandbox's named watch iteration and
optional ownership diagnostic configuration reads. They are portable and match the
existing sandbox convention. Two further findings match the README's PowerShell
code-fence label; its commands require no platform-specific behavior or elevation.
The reviewed baseline has 15,233 findings and final no-write enforcement passes.

## Selection and support checkpoint

Insights.UI now owns the complete Selection Panel, its actual node-detail child,
Object Index/TreeView/menu, Signals and Health, including their feature CSS. Shared
canvas/window styles remain in Components. Workbench prepares safe display values;
the leaf contains no tracked structure nodes, metadata parsing, project writes,
file-content authority or runtime services. Existing public value/component names
have type forwards at their original assembly. The native task, file, Mermaid,
runtime, Workflow and Process integrations retain their original hosts.

Each rendered action carries the original project/profile/lifetime, view identity,
selection revision, exact targets and receiver. An acknowledged menu carries its
opening identity and offered actions. Selection A-B-A increments the revision even
when the visible labels are unchanged. Native admission is checked before writes;
accepted native node results, command artifacts and exact deletion/partial-recovery
outcomes are retained before view reconciliation. Refresh failure does not repeat
a write. Delete confirmations carry their exact prompt, original native admission,
actor and selection revision. Window placement and selection/border state use a
new mandatory-admission overload on the existing native view-state owner.

Health's existing native capability is explicitly unavailable: its former callback
was a no-op and `CanValidateSelected` was false. No validation run is invented.
The independent controlled host exercises the optional explicit callback; native
Health shows the original classifier's counts and spotlights without that action.

At this checkpoint, 23 leaf cases and 28 native lifetime/integration cases pass with fresh
discovery and pre-run source/assembly manifests. Native cases include stackable
marker add/remove, progress/priority, unchanged neighbors, status/group frames,
selection A-B-A, forged targets, recreated project lifetime denial, exact delete
confirmation and the existing summary/transcript/secret action lifetime family.
The native Workflow-status read rejects A-B-A completion, and the original Workflow
attachment and canonical party-assignment cases pass. Native pending-delete denial
keeps its exact prompt and visible error; the deferred render key includes the error.
The failed error-display attempts are retained. Storage catalog server roots stay at
their native owner; the renderer receives a configuration status. Activity punctuation
is normalized to UTF-8 and the leaf suite was rerun on those bytes.
Initial failures remain in private evidence: fixtures incorrectly assumed a zero
progress default and a 25% Signals preset, then compared replacement progress with
the old node rather than the replacement's own pre-write readback.

The real application selected its retained WB1 task through Object Index, persisted
Signals effects, opened and canceled the exact native delete confirmation, and
edited the task through the new Selection Panel. Native readback retains dates,
3.1256789 hours, 125.123456789 EUR, assignment revision and all untouched task fields;
the intended description is stored in the canonical work-item metadata. Parent
effective priority changes are native derived values, not extra writes. Both
independent support instances render the actual windows and detail child. A
large-desktop screenshot exposed the legacy `copy` icon alias; the leaf now maps
that presentation alias to the shared icon's `content_copy` glyph.

Portability review identifies five relocated case-insensitive display searches and
one relocated marker compatibility comparison. All use explicit ordinal policy;
none normalizes a filesystem path. Eight stale findings correspond to removed or
moved code. W3 still owns final publication/watch measurements, complete native
report math/cursors, actor/grant and consumer journeys, final graphs and closure.

## Current renderer, caller and asset census

| Surface | Extracted rendering and children | Native caller and retained authority |
|---|---|---|
| Manager Summary | `Reporting/ProjectManagerSummaryPanel`, real `CdaChart`/series, metrics, options, load/progress, confirmation, warnings and notes | Workbench's small same-name adapter owns project admission, native query source and retained profile/project snapshots. |
| Activity | `Reporting/ProjectManagerActivityDialog`, real `DataGrid`, kind/status controls, totals and page footer | Each native activity source owns accepted UTC cutoff, aggregate provenance and Process continuation cursors. |
| Selection | `InsightsSelectionWindow`, full `ProjectStructureSelectionPanel`, actual `ProjectStructureNodeDetailPreview`, badges and compact statistics | `ProjectStructurePage` captures original selection and actor; existing inspector commands, task forms, assignments, approvals and mutation owners perform effects. |
| Object Index | Real `TreeView` and nodes, search, exact offered context menu, shared floating window | The page acknowledges original target membership and menu opening; native actions retain authority. |
| Signals | Full marker/progress/priority sections and shared floating window | Native admitted node mutation methods preserve additive markers and precise changed identities. |
| Canvas Health | Existing classifier counts, spotlights and shared floating window | Native validation remains unavailable as at entry; the optional controlled callback does not claim a native validation run. |

The production caller is `ProjectStructurePage`; its Summary adapter composes a
per-view `ManagerSummarySession`. The sandbox's `Insights.razor` contains two report
sessions, and two `SupportSpecimen` instances render the actual support windows and
advanced detail child. `CanvasFloatingWindow`/`OverlayWindow`, `TreeView`, Charts,
dialogs, shared theme, fonts, icons and browser runtimes stay in Components. Feature
CSS moved with its renderers. Insights has no feature-owned JavaScript.

File bytes, previews, direct file interaction and Mermaid remain native FileTools
integrations. Workflow status/start/attachment, Agent launch, transcript/export,
party assignment, task editing, project hierarchy, deletion confirmations and
main-canvas operations retain their existing page/application owners. The leaf has
safe display projections and captured intents, never a runtime service bag.

Evaluated graphs preserve all 21 existing renderer/sandbox roots. Workbench changes
from 94 to 96 projects by adding Insights.UI and its contracts. The new UI closure
has seven projects; its independent sandbox has eight. Loaded-assembly tests verify
all 40 moved public identities through the original Workbench assembly and reject
product implementation dependencies from the complete Insights assembly closure.
AppComponents, Foundation and Processes have no reverse product-renderer reference.

## Independent host and developer-loop proof

Source Fast/Parity and independently published Fast/Parity pass the actual report,
four-chart, Activity, full support and advanced-detail journeys at 1920x1080/DPR1.
Every served JavaScript, stylesheet and font response in these runs has a recorded
hash. Both published modes additionally pass window dragging, resizing, stacking,
keyboard minimize/restore and index typing. Screenshots were inspected, including
scrolling and Activity footers. Initial harness assumptions about HTML canvas,
border-inclusive drag coordinates and reset placement are retained as failed
attempts; Charts uses SVG and window reset uses the configured default placement.

SDK 10.0.303 watch observations contain three edits and byte-exact restorations per
owned file kind. These are warm-cache observations, not a cold-build comparison or
an inferred speedup ratio:

| Host and mode | Razor edit/restore | C# edit/restore | CSS edit/restore |
|---|---|---|---|
| Small Fast host, hot reload | 1.081-6.199 s | 0.156-0.171 s | 0.664-1.013 s |
| Native host | 2.183-3.595 s, hot reload | 1.002-1.409 s, hot reload | 29.374-37.517 s, restart mode |

All small-host and native managed-code observations preserve PID and require no
manual navigation. Eleven small-host Razor/C# observations include an automatic
page navigation; its CSS observations and all native managed-code observations
have none. Same PID does not mean every update happened in place. Native default
CSS hot reload crashes inside the SDK's
`HotReloadClients.ApplyStaticAssetUpdatesAsync`. The documented static-file
suppression option does not apply CSS in this native graph. The completed native
CSS measurements therefore use `dotnet watch --no-hot-reload`: they record actual
process restarts and five explicit navigations across six observations. Original
SDK failures and two navigation-harness failures remain separate. No SDK or product
workaround hides the limitation. See the [official watch reference](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-watch).

## Native reporting evidence

Fresh focused owner selections pass 54 Unit and 71 PostgreSQL Integration cases.
They cover scope bounds, report arithmetic/currencies, deletion partial results,
native admission, execution tracking and Workflow/Simple Chat report persistence.
Two added persisted paging cases pass on actual stores: canonical Simple Chat
operations use offset pages; root Process runtime commits, assignments, projections
and native facts assembly use continuation pages. Both span 20 plus five records,
retain accepted totals on backward navigation and exclude later records from their
original as-of window. No report DTOs stand in for native persistence.

The first Process fixture omitted facts assembly and launch assignment project
identity; its failed result is retained. Once the actual assembler ran, cancelled
never-executed runs correctly produced known zero cost, not missing pricing. The
Simple Chat fixture separately verifies exact decimal charges and a later accepted
window. Forty original public identities and the complete allowed assembly graph
have their own two-case passing selection.

The retained native project also opens a stored `.mmd` asset through Selection's
existing governed preview. The native file owner supplies exact canonical bytes,
the original Mermaid renderer produces SVG, and every neighboring node remains
equal. The initial generic-node request was explicitly refused with
`ManagedAssetCreationRequired`; the subsequent managed-asset creation is recorded
once and reused. Metadata's omitted notes never substitute for content authority.

## W3 qualifications and retained attempts

The owned source/two-client topology passes all 19 protocol vectors on image
`sha256:d4829fdc2a2db772065d11dc00f6d617ec21f78c988d2a810d2a63535d73b1c9`,
with source fingerprint
`bdf4a6ac4b2e50d02c60d29f6c13e838c597b5aa7f69478bbaa894f70ddbd721`.
Its application source is W2 `531a8039` paired with Components `24d182c6`.
Later test and documentation edits have separate input manifests; they do not
change the image's recorded identity or its production source. The defaults and
custom-catalog browser campaigns each pass one current case. No paid calls occur.

The first full Insights consumer reaches the final descendant readback after its
native approvals, rejection, file readback/download, task edit, Calendar/Gantt and
accepted report/Activity assertions. Its whole-child-tree equality incorrectly
rejects the expected parent relationship just added by the hierarchy command.
The corrected test asserts that exact parent, preserves both original task rows,
then compares the settled attached tree before and after the read-only report load.
The original failed run and accepted project/run/approval identities remain retained;
its routing and History assertions below the failure were not reached.

The second full attempt completes its Agent/file, approval, rejection and task
prefix, then fails an overly broad header equality check: changing draft options
intentionally adds the `Options changed` badge. The corrected check waits for that
badge and compares the accepted metrics and exact `As of` text. A continuation on
the retained project, task and readback run passes the current report, hierarchy,
route and History helpers. Both parent asset hashes and the untouched sibling's
asset hash remain equal. No Agent/file proposal is replayed. This is passing native
continuation proof; neither original full xUnit failure is relabeled as a pass.

The initial continuity batch reports two failures before an intentional stop.
Scheduler fails before saving its draft; a retained unsaved probe verifies native
input/JSON synchronization. Waiting for the schema default alone still fails on a
future draft after two successful restarts. The current test also clicks the input
and verifies focus before typing; the complete campaign then passes. Its completed
plan stays enabled across two actual app restarts with the exact original run and
detail unchanged. A future neighboring plan executes once; provider captures remain
unchanged. No Scheduler production change or timeout increase is involved.
Floating context, detach/follow, distinct handles and close
with a pending approval pass; the final rejection reply exceeds its 60-second
assertion while persisting. Readback then verifies the exact original run/session,
rejected proposal, no write receipt, unchanged Agent configurations and the stored
reply, which is also visible after reopening the native chat. No rejection repeats.
The interrupted batch had already accepted the next standard-chat case's initial
turn; that exact completed turn and capture are read before its fully consumed
plan is cleared. The original aborted batch remains failed. Subsequent consumers
run individually.

The standard-chat case passes runtime details, execution log, pending-run
cancellation and original-transcript reopening, then encounters the fresh profile's
explicit incomplete Usage index. Its original grid assertion stays intact. The
existing `tools/UsageIndex` command initializes 31 records for only this fixture's
client-A organization scope, without rebuild or legacy-migration flags. All 602
canonical JSON payloads remain byte-identical. This derives an index from native
evidence; it neither seeds report rows nor makes a model call. The initial failed
partial-view assertion remains part of the evidence.

The separate current standard-chat/Usage, default and nondefault Simple Chat,
accepted and incomplete Workflow/TestLab, human-response and canvas-gesture cases
pass. The source disable/retire/reimport and two-circuit local-settings cases also
pass: unavailable routes refuse dispatch, native imported identity survives
reimport, and explicit conflict resolution preserves the other operator's unedited
enabled value. Original local settings are restored and read back.

The first final History case passes lazy metadata reads, provider/global identity,
caller-key filtering, paging and denied content authority, then fails credential
rotation. The shared test helper checked for an existing secret while native
metadata was still loading and created a duplicate name. It now waits for the
native refresh completion and requires unique identity instead of selecting the
first ambiguous label. Exact readback shows the original source binding and valid
credential remained intact; the duplicate had no references and held the revoked
test credential. Only that orphan is deleted through the native secret editor,
with an exact committed receipt and unchanged original hash/binding. Two private
cleanup attempts omitted the startup database confirmation and produced no native
receipt or database effect; their observations are retained. The corrected private
helper completes that confirmation before selecting the exact record. No SQL
writes, source rebinding or setup replay are involved.
The corrected one-case History follow-up passes in 4 minutes 4 seconds, including
revocation HTTP 401, a completed native turn through the replacement credential,
exact caller-key History identities and cleanup restoring the original secret.
Browser errors and failed requests are empty. The original three-case final batch
remains recorded as two passes and one failure, with the History follow-up separate.

Central, client A and client B each serve the fingerprinted Gantt runtime with
SHA-256 `C1F03766D8AC098A336F8AB8BDC51FDC10C7C8A6BA6196FD38D3A21A746EDED8`,
identical to the repaired Components source. Local image provenance and served-byte
readback are separate from remote dependency delivery.

The broad Components assembly executes 2,649 cases: 2,646 pass and three event-
dispatch cases fail. The mutation test now combines each DOM lookup and click on
the dispatcher; the Workspace status test queries its actual tabs renderer. A
fresh isolated assembly passes all three with the original persistence, read-count
and unavailable-status assertions intact. The first isolated attempt accidentally
used the old copied DLL; another lacked the Web dependency. Those attempts are
preserved separately from the verified current three-case result. Production
source is unchanged by these test repairs, and the original broad result stays failed.

The single frozen Stable checkpoint builds the 186 production and 29 Stable
project memberships, retaining 20 existing build warnings. It discovers 16,607
cases and executes 16,662: 16,658 pass, four fail and none are skipped. Seven
deferred theory groups explain all 55 additional executions: five in Integration,
16 in Memory and 34 in Unit. Every discovery method is accounted for. Native
Integration passes all 3,289 executions. The remaining Unit failure reports only
the same synthetic negative control in four retained historical JSON files. Their
complete hashes, control fingerprint and unchanged scanner source match the WB1
review. Neither the artifacts nor scanner rules are altered to obtain closure.

The frozen checkpoint has pre-run source and assembly manifests. A later
observation confirms all 29 primary test DLLs still match those input hashes;
isolated repair outputs never replace them. Current native and focused proof,
reviewed failure dispositions and final static gates establish bounded WB2 closure
without relabeling the original failed run. No further broad run was needed after
the test-only repairs and documentation updates.

Current portability enforcement passes all 15,231 reviewed findings without
baseline-write mode. Documentation validation passes 368 maintained Markdown files.
The changed-source secret scan retains 20 heuristic candidates, all with the same
fingerprints and counts as existing entry CI test values/expressions; it finds no
introduced candidate. This is a scoped comparison, not a clean complete-worktree
claim. The separate complete-worktree snapshot scans 109,612 text files and reports
1,936 heuristic candidates, including historical artifacts, test controls,
dependency copies and ignored owned-fixture credentials. It has no oversized text
exclusion. Of 6,954 reported unreadable paths, 4,876 are non-text candidates; a
supplemental scan reads 2,074 text files through extended Windows paths or explicit
BOM decoding, with no candidates. Four retained historical files remain undecodable
under that UTF-8/UTF-16 procedure. Originals and scanner rules are unchanged. Neither
report is represented as a clean whole checkout. A further reversible byte mapping
of those four historical files also finds no ASCII-rule candidates; it does not
claim to recover their original encoding. The final changed-source snapshot covers
105 text files. The post-native owned-task snapshot scans 5,502 text files and
retains 130 reviewed candidates: 120 existing CI-copy values, seven synthetic
discovery controls and three ignored owned-fixture credential matches. The final
owned-task scan includes completed Stable output; its exact coverage and reviewed
candidate fingerprints are recorded separately in
`w3-final-owned-secrets-review.json`. These scoped reviews do not change the
complete-worktree qualification.

The C# architecture gate finds no blocking boundary or composition issue. The
remaining qualifications are local-only dependency delivery and the measured SDK
CSS hot-reload limitation. All owned source, published, watch and multi-instance
hosts and the final PostgreSQL test fixture are stopped. Their containers,
volumes, project data and original evidence remain retained. The ordinary
application on port 5032 and historical fixtures are untouched. All new browser
proof uses 1920x1080 at scale 1, with zero paid model calls.

## Signed checkpoints and dependency delivery

| Repository | Verified signed checkpoint | Scope |
|---|---|---|
| Components | `24d182c664d0b1f293098643e52caed7384a5d50` | Acquired Gantt interop cleanup after pending cancellation/fault or DOM removal. |
| Main | `4722d6888f1939e5023dba87cddcf8e020bf0134` | Complete Summary and Activity rendering boundary. |
| Main | `531a80391f6efef3c72a3e3ab9bfa07c8f03726f` | Complete Selection and support boundary. |
| Main | `379efe1d3a238b758e2045799b58e90e5db27357` | Native consumer proof, public identities and focused test repairs. |

All four signatures verify with fingerprint
`96E836FAA8854EE98ABC10903C206549E1D7EAD6`. Components delivery is verified locally
through project-reference builds, loaded assemblies and exact served asset bytes.
The new shared commit has not been pushed or published. FileTools remains at
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`; no sibling source reset or remote delivery
is implied. Release readiness remains false.

The final signed main checkpoint changes only this closure record after
`379efe1d3a238b758e2045799b58e90e5db27357`. Its exact final main/Components/FileTools
pair, clean checkout observations, signature verification and artifact hashes are
recorded in the external `evidence.json` and `w3-final-source-pair.json`. Native
image provenance remains tied to W2 `531a8039`; later test and documentation
commits are not presented as a different tested image.

## Workbench roadmap

| Family | Current boundary |
|---|---|
| Calendar, Gantt, both task editor families | WB1 completed; preserved native planning and pricing owners. |
| Summary, Activity, Selection, Index, Signals, Health | WB2 complete, including the actual advanced child, independent/native proof and qualified regression closure. |
| Main Structure canvas | Not started by WB2. |
| Broader runtime, assignment and file presentation | Original owners retained; no new extraction started. |
| Processes product surfaces | Not started by WB2. |

Completed Agents AC1, Workflow and provider boundaries remain preservation
constraints. This roadmap does not declare all Workbench complete.
