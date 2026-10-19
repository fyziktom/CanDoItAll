# Projects lifecycle integration

This continues R1/R3/R4/R6 after the native chat browser checkpoint. The original
[P03 plan](../plan/P03-durable-lifecycle-checkpoints.md) and
[durable completion decision](../plan/P03-durable-owner-completion-refresh.md) govern
the intended behavior. The architect input remains proposal evidence.

## Entry decision: accepted-result encoding

**Pass** for the first dependency-ready implementation slice: three new production
files and six pure Unit cases. Integrate `ProjectLifecycleAccepted` into the existing
Projects.Contracts assembly, and `ProjectLifecycleEffectBinding` and the canonical
`ProjectLifecycleOperationCodec` into Projects. Keep the receipt EF configuration
with the upcoming coherent model/migration change. No new project or reference,
runtime registration, endpoint, writer, transaction or background worker is added here.

The original source/dependency pins in `p03-lifecycle-receipt-source/source-inventory.json`
were rechecked against MAIN and the preserved worktree. Actual source review covers
all three types and the six tests, including the existing completion codec and caller
binding they consume. The source proposal has no execution evidence yet.

| Responsibility | Owner and reason |
| --- | --- |
| Immutable accepted outcome, original intent and actual result identities | Projects.Contracts; reusable data without EF, HTTP, authorization or runtime dependencies |
| Canonical encoding, separate lifecycle digest domain and exact governed effect identity | Projects; owner-local interpretation of its effect, using existing caller/namespace contracts |
| Current permission, original transaction and durable replay | Upcoming Projects writer/reader, not this codec |
| Positive owner release and participant dispatch | Upcoming completion owner/gates; accepted deletion does not certify cleanup completion |

The data contracts validate command/result correspondence, original profile/namespace,
project lifetime and finite ordered UTC timestamps. Lifecycle identity remains distinct
from definition-save identity. A governed caller requires its admitted effect digest;
these bytes identify the intent and cannot grant permission. Payload reads reconstruct
and re-encode canonical bytes, rejecting altered scope, duplicate/unknown fields,
unsupported results and noncanonical forms. Failure messages contain no submitted data.

Bounds: fixed-size intent at most 4 KiB; accepted bytes at most 16 KiB; at most 16
participant references with 64 UTF-8 bytes per participant ID. The escaped worst-case
test checks the nested accepted payload. Inputs and digest storage are detached from
caller-owned arrays. This is bounded work without database/network access or locks;
no latency, allocation improvement or sustained-capacity claim follows from byte bounds.
The later SQL slice must still prove contention, aggregate retention budgets and cleanup
fairness. It must not activate the incomplete reserved-cancellation draft.

Dependency evidence: CodeAnalytics snapshot `snap-20261018163708-e010c61b` includes
five projects and 609 documents. Existing MSBuild workspace diagnostics wrap NuGet audit
warnings, so the snapshot is supplementary evidence, not a clean whole-solution gate.
Its four reported cycles are one module and three type cycles, not project cycles.
Direct project files confirm Contracts depends on SharedKernel and Projects already
depends on Contracts/Infrastructure/runtime contracts. This slice changes no reference.
Actual Release compilation is required; no cycle-free whole-repository claim is made.

Validation: build both changed production projects directly, then refresh the Unit
assembly in standard Release with local sibling libraries. Discover
`FullyQualifiedName~CanDoItAll.Tests.Unit.ProjectLifecycleOperationCodecTests` and require
exactly **6** cases before execution. The six cases cover all four result round trips,
original-state intent distinction, caller/scope mismatch, malformed/oversized results,
maximum escaped references and governed effect detachment/mismatch. Run the complete
portability scan including new untracked files, final no-write enforcement and maintained
documentation/rendering checks. No database fixture is needed for these pure cases.

Invalidation keys are the three added production files, the completion/lifecycle/caller
contracts and codecs they consume, and the Unit assembly/dependency mode. Broader tests
become justified when the writer, schema, retention, actual callers or participant gates
are integrated. This slice alone does not protect deletion or hierarchy writers.

## Accepted-result checkpoint validation

Standard Release builds of Projects.Contracts, Projects and Unit all passed on
2026-10-18, using local sibling libraries and serialized shared outputs. Exact discovery
found six cases; execution passed **6/6**, with no failures or skips. The three production
files, test source and Unit assembly hashes remained unchanged across execution.
Full portability scanning included untracked source; final enforcement passed with
**15,525 unchanged reviewed findings**, without a baseline update. Rendering and
documentation checks passed. Command records, exact inputs and the TRX are retained in
`.artifacts/concurrent-mutations/validation/p03-lifecycle-codec-validation`.
These are codec tests, not PostgreSQL, migration, dispatch or lifecycle activation proof.

## Remaining coherent cutover

The next working integration composes the reviewed P03 owner, metadata-reader correction,
durable release successor, governed-source guard, original creation namespace, participant
gates and bounded completion projection. Preserve original source images and verify their
recorded hashes before transfer. Merge overlapping edits against their recorded predecessors;
never replace a newer owner with an older whole-file draft. Compilation and actual SQL tests
remain required after composition. Source-only review records are not execution proof.

Activation requires the same change to include the canonical two-family protocol migration,
transfer residue checks, typed caller cutover, exact creation compensation and reservation
cancellation, and an explicit supported recovery procedure. Incomplete native/Workbench or
Process proposals must be completed and exercised, not registered as permissive substitutes.
The existing global hierarchy exclusion is retained until the separate serialization/cycle
tests justify removing it. No architecture boundary or dependency reference changes are
planned. SPA changes remain isolated on its existing branch.

Integrate the lifecycle receipt family and coherent reader, completion record/release
owner, canonical migration/pool protocol, participant gates, original-state caller
adapters and actual PostgreSQL proof together. Finish compensation/cancellation and
stopped-host reconciliation before declaring lifecycle activation complete. Then continue
runtime policy enforcement and the remaining owner/performance work from the request.

## Owner and canonical migration execution

The composed backend built in standard Release. The codec checkpoint passed 6/6 and
then the ten lifecycle/completion contract selections passed 38/38. The actual
PostgreSQL lifecycle owner selection passed 25/25. Participant and migration selections
first exposed two test setup defects (unregistered direct lifecycle store and positional
INSERT column order); the corrected six-case selection passed 6/6. The second run's
source and runtime identities remained unchanged. Both runs ended with zero observed
processes and no database-name delta; unowned TEMP deltas were preserved and enumerated.

The canonical EF migration is `20261018172443_AddProjectLifecycleOperations`. It adds
bounded lifecycle receipts, durable deletion completions, nullable native reservation
namespace and optional original completion addresses on Agent revocations. Preflight
checks and locks precede DDL; existing opaque receipt bytes and epoch promises survive;
only the Projects pool protocol advances to 2. Historical rows receive no invented
namespace or release authority. Downgrade refuses before deleting evidence. The three
migration cases cover actual predecessor upgrade/restart, opaque and legacy preservation,
foreign-key absence/model agreement, unsafe protocol/trigger rollback and refusal of Down.
The ordinary application database has not been migrated.

Evidence: `.artifacts/concurrent-mutations/validation/p03-lifecycle-owner-selection-validation`
and `p03-lifecycle-fixture-validation`. The latter source digest is
`9389839172E48BFB33E43928CB77E9A035F2BD1D77BB998710580CC3BF19EDAC`, runtime digest
`CB60D47971B88FD39EDF02A05C6336762EDEDF4AF45374C7C9377C057581B0DD`.
Final no-write portability enforcement passed with 15,537 reviewed findings, after an
explicit +12 net baseline review for the canonical generated model and existing detached
host-bound recovery data handling. Rendering, documentation and whitespace checks passed.
This is scoped evidence, not end-to-end closure.

## Native and HTTP caller composition

Proceed with the reviewed source predecessors and current MAIN three-way composition
recorded in `p03-native-http-compose/composition.json`. Projects.UI owns typed retained
submission state and rendering; Web owns IndexedDB; the Projects application adapter owns
local-workspace context and owner calls. No project references are added. The native page
retires the old editor without rebinding its pending operation; accepted evidence is
monotonic and applies only to the original opening. Only exact Found or ClosedAbsent
resolves an entered operation. Cross-tab claims use IndexedDB revision checks, explicit
retry requires the original address to be observed open, and storage failure blocks a
replacement write. The API reuses current human credential observation and bounded strict
JSON decoding; legacy Guid-only mutation routes return 428. Actual native, HTTP and browser
execution is required after composition. Workbench hierarchy and governed callers remain
explicitly unfinished until their captured-opening and source-authority cutovers pass.

## Native and HTTP execution

The first composed selection executed 100 cases: 94 passed and six failed. Four failures
identified replacement of the first retained acceptance object, one exposed hierarchy
feedback losing the selected project name, and the actual HTTP deletion replay found that
System.Text.Json default-constructed the participant ID. Preserve the first canonical
acceptance after validating subsequent bytes, restore target-specific feedback, and mark
the existing validated participant constructor for JSON deserialization. No wire shape or
project reference changed.

The repaired candidate passed 23 unit, 22 component and 54 integration cases (99/99).
The unchanged browser storage asset had already passed its real Chromium two-page test
in the first run. Workbench now captures the original endpoint lifetimes, dataset and
edge identity; retained overlapping submissions block another opening, and a late result
cannot close its successor. PostgreSQL and actual HTTP tests cover original receipt
lookup/replay after deletion, strict decoding, current credential changes and owner races.

Evidence: `.artifacts/concurrent-mutations/validation/p03-native-repair-validation`.
Source digest `5DFD19D2ADC633CDB3C359D76428B884772C97036CE1AB67ED334A375098708D`;
runtime digest `65ACE60A861D0602249A2DE30E7C4EDFE518F3E8CEDE63BD72ED8F9CE0CA18DA`.
Final identity verification passed, with zero observed processes and no database-name
delta. Thirty-three TEMP paths without positive ownership were preserved and enumerated.
Final no-write portability enforcement passed at 15,537 findings; rendering, documentation
and whitespace checks passed. This is scoped caller proof, not complete bundle acceptance.

## Reserved creation cancellation and caller cutover design

Cancellation is a distinct origin of the existing Projects completion protocol. It accepts
only the exact native, never-consumed reservation: profile, namespace, reservation ID,
project/lifetime, requester and original parent/lifetime. It must find no live project and
no residual Workbench, CRM, storage, Workflow or Process state. Its transaction prepares
typed participant obligations, changes Reserved to Cancelled and stores the completion
atomically. It never invents ConsumedAtUtc or a project retirement. Participant completion
remains blocked until terminal owner/source release is independently recorded.

The existing source-before-SQL lock order and bounded participant catalog remain. Exact
replay is read-only; commit uncertainty retains the same request and deterministic address.
Do not mint a new cancellation from current project identity. A consumed creation requires
its original compensation receipt instead. Legacy cancellation and compensation callers
must enter these owners rather than performing direct row changes or grant revocation.

Post-release creation cleanup needs a separate current-authority observation. After exact
full-binding Released evidence, reacquire the original source with its creation reservation
purpose and all original parent/source admissions in a short transaction. This checks
current creation capability, saved Process dispatch and original execution ceiling without
requiring the removed child grant to remain present after its own revocation. No mutation
is committed by that observation, no unrelated project admission is dropped, and the
never-created/deleted target cannot itself supply source authority. Local callers retain
the original profile generation and namespace. Observation/disposal failure blocks further
external cleanup while preserving the acknowledged owner result.

Validate native root/child cancellation, full tuple/namespace refusal, consumed/live target
refusal, residual state, atomic completion-insert rollback, final source/caller revocation,
lost commit acknowledgement, terminal disposal failure, exact read-only replay, same-address
contention and actual Agent/Process grant cleanup. Reuse the existing isolated fixture.

The first cleanup selection executed 124 cases: 118 passed and six failed in caller setup
or old refusal expectations. All 37 owner cases, 40 unit cases and six component cases
passed. The source/runtime identity check passed; no observed processes or database-name
delta remained. Twelve TEMP paths without positive ownership were preserved. Evidence is
`p03-cancellation-validation`; this failed selection is not acceptance.

### Active child reservations block parent deletion

Review found that the old parent-deletion cascade cancelled reserved children without
storing their own participant obligations. That could leave issued Agent grants without
recoverable cleanup. Remove that cascade. Under the existing project/hierarchy exclusions,
before participant preparation, refuse deletion if a native Reserved row targets the
project or depends on it as parent. The indexed existence query returns one boolean and
does not fan out child cleanups. This also applies to creation compensation.

The caller must use each original child's durable cancellation owner, then observe and
explicitly retry its original parent deletion. A committed child cancellation has its own
released completion address even if external cleanup remains pending; parent retirement
does not imply that cleanup finished. Imported historical reservations never grant native
authority. PostgreSQL proof must show refusal preserves the parent, reservation and grant,
exact cancellation removes the grant, the same parent submission can then commit, and
recreating the public parent ID cannot reopen the old child reservation.

The repaired selection passed 78/78 integration cases: 37 owner and 41 caller cases.
Evidence: `.artifacts/concurrent-mutations/validation/p03-cancellation-repair-validation`.
Source digest `117CCFA28F1F1D2BF31993558CCC192F5FB991CADF4B2A4D68E0678DCE3CDD4F`;
runtime digest `3033DE21578C976ACF1E1E9AC55F59AE6226F5900B79886C69C9A6664A35F8D9`.
Final identity verification passed. No observed processes or database-name delta remained;
13 TEMP paths without positive ownership were preserved. Standard Release build, final
no-write portability enforcement at 15,537 findings, rendering, documentation and
whitespace checks passed. Earlier 40 unit and six component cases are predecessor evidence,
not a claim of execution on this later source.

## Displayed canvas dataset binding

The hierarchy dialog previously captured its dataset when opened. An older displayed
canvas could therefore be combined with a newer runtime or restored dataset. Capture the
profile, namespace and runtime generation around the surface read, publish them with that
surface, and require every hierarchy opening to match that binding. Recheck before surface
publication and before applying callbacks. Reading this scope observes the bounded activation
record only; rendering does not rotate a finite-operation epoch or acquire SQL mutation
locks. Validate generation changes both before an opening and during delayed reads, and
verify that existing independent-opening and retained-submission behavior still passes.

The first canvas run passed 24/27. Two failed assertions expected a dialog in the deliberate
load-error screen. The third exposed a host issue: deriving the renderer context ID from
current runtime validity regenerated the ID repeatedly while an old canvas remained visible.
Keep renderer identity tied to the displayed binding, separately test current write validity,
and show a reload error for an action from a changed workspace. Repeat the 27 cases because
the shared renderer dispatch now changes. The failed run's source/runtime verification and
resource reconciliation passed, with zero processes/database delta and eight preserved
unowned TEMP paths; its evidence is `p03-surface-validation`.

The repaired canvas selection passed all 27 component cases. Source digest
`DE5B758C79E2BFA55184F8FD4B287A27A841DA10DC58EF0959D39C535B96342D`;
runtime digest `89FDA4779E125DBABDAE60533C29A9990B3567E90C6D9086BA35B58984C4E5B5`.
Final identity verification passed. No observed processes or database-name delta remained;
six TEMP paths without positive ownership were preserved. Release build, final no-write
portability enforcement (15,537 unchanged findings), rendering, documentation and whitespace
checks passed. Bounded evidence is in `proof/2026-10-18-project-canvas`; full local evidence
is `p03-surface-repair-validation`. The namespace-change case is a deliberate isolated
activation-record fault injection, not proof of the complete stopped-host restore procedure.

## Verified prior invocation boundary

Core exposes the immutable bounded journal prefix before an exact live dispatch claim.
Workbench will interpret project creation semantics and verify actual Projects receipts;
Core must not depend on Projects or infer permission from a result string. Separate receipt
claims expose read evidence, including the original pinned snapshot after interruption,
without granting dispatch authority. No schema or project-reference change is required.
The current query deliberately requires the persisted dispatch claim ID, including after
completion by that same claim. Reacquiring a completed proposal creates a different cached
claim and cannot reuse this mutation query. Cached disclosure must use read evidence rather
than treating a historical result as a new dispatch. Validate exact prefix, foreign/replaced
claims, current read revocation, pinned interruption and released reconciliation leases
alongside the existing journal, effect and recovery tests before consumer integration.

The prefix query and six existing journal/recovery classes passed 96/96 integration cases.
Source digest `1F6B168399BECEF2348A21AE6C53468132F2955AD963A4812CEF33CCC3E4E6BD`;
runtime digest `40543CF44172D70A70D233D4E27900E222351F53774BBCC77F3E3BDC5079CFE8`.
Release build, final no-write portability enforcement, rendering, docs and whitespace passed.
Final source/runtime identity was unchanged. No observed processes or database-name delta
remained; nine unowned TEMP paths were preserved. Evidence is in
`proof/2026-10-18-prior-invocations` and local `p03-prior-invocation-validation`.

## Governed hierarchy integration candidate

The Workbench adapter derives at most three missing endpoints from the verified journal
prefix, scans that prefix once, and reads only the selected creation receipts. It requires
exact approved canonical creation proposals, the same saved session/namespace/epoch/source
ceiling, FoundFull Create receipts, exact returned lifetimes and native consumed reservations.
One bounded batch verifies the reservation rows and current lifetimes; the final owner
transaction repeats the consumed-reservation check. No tool result string grants permission.
Original Agent access lists remain immutable. A separate derived-reservation set carries
provenance and the project-scoped ceiling admits only children of its original source.

The original and current Process initiating principal retain authority over the source
project. Created-target access is checked against the executing Agent, to whom the original
grant was issued. One existing two-Agent catalog lease covers both identities when needed;
no nested catalog acquisition is introduced. Human source authority remains independent.

Normal journal reads acquire publication exclusion. The runtime factory stores project-scoped
runs separately from the organization catalog, so a catalog-only snapshot is insufficient.
Acquire organization catalog, original execution publication, then SQL, in that order. Reuse
the existing publication exclusion only when the two stores resolve to the same physical
lock path under the filesystem path policy. Otherwise only a project or sandbox execution
store may follow the organization catalog; a second organization store refuses. Core passes
its original execution reader to this Persistence capability and validates the exact active
claim, proposal, approval, run, chat/session and lease against the protected snapshot.
Workbench retains both leases until SQL commit/release and disposes in reverse order, keeping
primary and cleanup failures. Unsupported adapters refuse explicitly. No Projects dependency
enters Core, and no background callback runs under these locks. The current coarse locks are
a correctness baseline; selected-key scaling remains a separate measured requirement.

Cached-result disclosure receives a separate verified read-only prefix by exact completed
intent and payload, including checkpoint validation with no current invocation claim.
Interrupted/cancelled reconciliation retains its own read-only claim. Historical catalog-
basis hierarchy lookup is still a separate outstanding integration gate. Origin receipts
must share the operation epoch; expiry does not authorize a new or reconstructed intent.

The post-release hierarchy check captures the dataset and runtime, reacquires source before
SQL, validates every original endpoint and source lifetime in the short enlisted transaction,
and commits no mutation. A failed observation or disposal cannot erase known acceptance.
The existing global hierarchy lock remains; reducing it awaits the separate cycle proof.

This candidate requires actual Agent/Process creation-link-reconnect and recovery tests,
revocation/recreated-lifetime refusal, final claim fencing, bounded lookup assertions and
appropriate existing definition/caller regressions. It is not bundle acceptance.

The initial governed hierarchy candidate executed 102 cases: 78 passed, 24 failed. Seven
failures exposed an invalid proposal semantic version (source preparation requires version
two); one assertion still rejected the newly supported hierarchy binding. The remaining
failures include creation-grant regressions and obsolete Guid-only lifecycle test setup.
The cross-store flaw was found by architecture review before claiming acceptance. Repair
tests must retain the project journal in its original store, rather than copying it into the
organization store. Source/runtime identity remained unchanged for this failed run; resource
reconciliation found zero processes/database-name delta and preserved 18 unowned TEMP paths.
Full evidence is `p03-governed-hierarchy-validation`. Its source digest is
`F16D186901406BC3F3002211DC946125ECE70869EDAE1A2938AA57048BE3514D`; runtime digest is
`26645581BF39D29FB48A0189A1D62010B6BB185412B6C7F32CA50ECE2D71AAD4`.

The repair preserves creation authorization through the actual server reservation, including
callers whose original source was captured before the reservation existed. A newly reserved
Process target does not require a published target grant; subsequent uses of a consumed
target do require the current executor's grant. Tests now capture lifecycle commands before
revocation and verify the real owner outcome instead of exercising the retired Guid-only API.
The source-owner test seam supplies only receipt/post-release observation; actual Agent or
Process source checks and PostgreSQL commit guards remain active. Registered-tool tests
separately exercise the complete admitted proposal path.

Three new portability findings were reviewed in `FileSandboxWorkspaceStore.CatalogReadLease`:
the existing physical filesystem policy resolves the catalog's case semantics, and native
`Path.GetFullPath` normalizes both internally constructed workspace lock paths before that
policy compares them. No hard-coded platform path, string case-folding or OS guess is added.
These storage-boundary uses are intentional. The reviewed baseline moves from 15,537 to
15,540 allowances; final enforcement must run without `--write-baseline`.

The publication repair executed all 103 discovered cases: 95 passed and eight failed. Both
shared-store and separate-project-store claim guards passed, as did all 37 Unit cases and the
project-scoped creation/link/reconnect flow. Remaining defects were generic Structure result
wrapping intercepting the hierarchy owner, a test assuming creation grants left executor
interactive read disabled, and five obsolete outcome/exception expectations. The next
candidate excludes hierarchy owner-receipt tools from that generic wrapper, explicitly
revokes executor interactive flags after grant publication, and asserts conservative
OwnerReleaseUnconfirmed when acquisition fails before the lifecycle owner receives a source
lease. Tests separately verify no hierarchy/receipt change and that catalog access resumes.
Recreated targets now fail earlier at the exact current executor grant check.

This failed run retained source digest
`549CCD889CDEF6BF7651C610FB90B6AA5DEFC07AB39B4E1DC0C706E031867321`
and runtime digest `6CAAA0CD03EBBC8B14B3BB705B05964E1464DDF5401E13B654D235299C10E8E0`.
Final identity verification and resource reconciliation passed: zero observed processes or
database-name delta, with 12 unowned TEMP paths preserved. Full evidence is
`p03-hierarchy-publication-repair-validation`. A complete current candidate must pass before
the historical hierarchy integration can be accepted.

The routing repair executed 21/21 discovered cases: 19 passed and two Process cases
failed only at the executor CanRead assertion. Existing access normalization always enables
read when a project grant is present. The test now preserves that invariant and proves that
interactive write flags remain disabled while the Process delegation authorizes hierarchy
changes. Source digest `D7A4338D75F457999010BA8423E951BB2B46093F366067586A883455702F31B5`
and runtime digest `A90AD723CF39F4F677FD0FDE49EECC359C865CF44371A50189E852D8EE6FB127`
remained unchanged. Build, portability (15,540 reviewed findings), rendering, docs and
whitespace passed. Resource reconciliation found zero observed processes or database-name
delta and preserved nine unowned TEMP paths. Evidence: `p03-hierarchy-routing-repair-validation`.

## Historical hierarchy integration candidate

The historical claim now exposes its owner-verified immutable original journal prefix only
inside the active read scope. It validates the exact session, binding and run/journal
revisions and confers no dispatch capability. Core remains independent of Projects.
Workbench reuses the bounded creation resolver with a typed original-receipt lookup delegate.
The historical Process reader checks the initiating source's original project and the
executor's created-target lifetime grants against the same sealed catalog witness.
The lifecycle provider is registered alongside the definition provider in the existing
composition root; no project references or schemas change. Lookup resolves creation
provenance only for a found hierarchy receipt, preserving expired, absent and dataset-change
classifications without rebuilding an intent. It returns the same accepted receipt codec.

Validation will cover the repaired Process cases, four actual Agent/Process creation-link-
reconnect histories with source or executor-grant revocation, the existing original-bound
historical review, cancellation and HTTP cases, and Core prefix/released-claim checks.
Existing historical lifetime-replacement setup now uses the lifecycle owner. All original
run and receipt bytes must remain unchanged. Actual stopped-process hierarchy recovery and
capacity remain separate open proof requirements; this candidate is not bundle acceptance.

The historical candidate executed 18 cases: 16 passed and two interactive hierarchy fixture
cases failed because their child-creation requests omitted the required original parent
lifetime. Both Process histories, both repaired Process hierarchy cases, and all 12 existing
historical Core/HTTP/catalog regressions passed. The fixture now supplies that exact parent
admission. Source `F2654794821691B0846FA76A91202E50607952175D63B5BE98CC7209607ED471`
and runtime `BD1FF75BF48665742C2655343B7372721C975DF45C548A6799BA66CC7D7C1F8B` stayed
unchanged; all static/build checks passed. No observed processes or database-name delta
remained; ten unowned TEMP paths were preserved. Full evidence: `p03-hierarchy-historical-validation`.

The next proof extends the existing seed executable and actual Web restart fixture with
interactive/Process hierarchy scenarios: three actual child creations, one link and one
reconnect. The last accepted reconnect deliberately lacks journal completion when the seed
process exits. The fresh Web observer must recover all original receipts, preserve execution
files and receipt bytes, refuse revoked source reads and reject a replaced endpoint lifetime.
The original two definition restart scenarios remain regression cases. Both runtime outputs
and the seed executable must be frozen; ordinary background recovery remains disabled only
for this explicitly isolated read-observer fixture, as already documented in its README.

The scoped CodeAnalytics snapshot `snap-20261018200500-1a611543` covers Core and Workbench
and reports no project-reference cycle. Its workspace load is incomplete: 57 existing
package-audit diagnostics prevent resolving some dependent source projects. The snapshot
also identifies existing same-owner claim/lease and Workbench namespace cycles; none is a
new cross-project dependency. This is partial analysis, not a clean architecture gate.
Direct project-reference verification and the normal Release build complement it; full
bundle architecture acceptance remains open with the other runtime/capacity requirements.

The direct source-reference check covers 248 projects and 996 literal references, includes
conditional edges conservatively, finds no cycle or nonliteral unresolved reference and
confirms no working-tree `.csproj` edits. Evidence: `p03-hierarchy-restart-validation/direct-project-graph.json`.

The first restart candidate passed all four in-process histories and the existing interactive
definition restart, but failed both hierarchy seed children (exit 2) and the existing Process
restart's immediate endpoint-close probe. All four owned databases/environments were removed
and all child processes exited. Final source/runtime verification passed, with no observed
process or database-name delta; eight unowned TEMP paths were preserved. Source digest:
`1A003435352C6A3A422502D52B73BD6DF3424D1BFD1AEFBDEC16EE1E91D3BBEC`; runtime digest:
`EA29CDF20AA4C9A3C3709DF034095771615CF8943E05DC1AA59AB7FF99397AAA`.

Source inspection found that the new hierarchy seed decoded the tool's textual ProjectStatus
with HTTP options lacking that converter. Its result reader now uses the explicit enum
converter. Seed failures retain only bounded exception type/method/line metadata, without
messages, values or credentials. Endpoint closure still requires connection refusal after
owned-process exit, now within a five-second deadline rather than on the first probe. The
failed Process port was absent during reconciliation. These repairs require another actual
restart run; the failed candidate is not accepted.

The repaired actual-process run passed all four discovered cases: interactive and Process
hierarchy histories plus both existing definition restart scenarios. Each hierarchy seed
committed three creations, a link and a reconnect, retained the last proposal without its
completion, then exited. A fresh Web process recovered the original accepted receipts,
preserved the original execution and receipt bytes, refused revoked reads and rejected a
replaced target lifetime. The Process seed's original execution claim was expired.
Source digest `ABC55FE4BD41A61173EE501AB5188D814ECBE0A5CE23E5AF2CFFB5E787C59406`
and runtime digest `472FC8D84E71EF8BF4FF82615B5CF256BD30E00F04B6122C2C1F6A74B3EF4022`
were unchanged. Build, portability enforcement (15,540 reviewed findings), rendering,
documentation and whitespace checks passed. All four row cleanup records passed; final
reconciliation found no observed process or database-name delta and preserved three unowned
TEMP paths. Evidence: `p03-hierarchy-restart-repair-validation`; compact retained proof is
[the hierarchy restart record](../proof/2026-10-18-hierarchy-restart/evidence.json).

The next candidate reuses the exact live-claim publication guard for definition Update and
Create, including both their reservation and final owner phases. Native Process callers
without a Core tool claim retain their existing Process source guard. No schema, dependency
or owner boundary changes. Two actual-tool tests pause SQL and verify that the original
execution store cannot publish a replacement until the owner releases it, then prove that
completion retires dispatch authority. Additional creation-provenance checks cover the
three-endpoint bound and refusal of unconsumed, imported, foreign-namespace and wrong-tuple
evidence. This candidate requires compilation and execution; selected-key capacity and the
remaining P03 caller/recovery cutovers stay open.

The definition guard candidate passed all 24 discovered cases: 11 claim/provenance cases
and 13 registered-tool, MAF, replay and Process regressions. Source
`24C9955377332E082FD71CDE534170F02D6E9AE0E2818F49B9E8626A029A72D2` and runtime
`A5518D2A5BB62FD9AF512143D8C7142986ABFF292C16C120657E62FA597EE103` remained unchanged.
Build, final portability (15,540 findings), rendering, documentation and whitespace checks
passed. The documentation validator initially interpreted a quoted branch name as a path;
the repaired final documentation check passed. Reconciliation found zero observed processes
or database-name delta and preserved six unowned TEMP paths. Evidence:
`p03-definition-publication-validation`. This closes the exact definition claim guard;
it does not close the selected-key capacity requirement.

The next vertical follows [the created-definition design](../plan/P03-created-definition-provenance.md).
The shared bounded resolver now depends only on Projects admissions, with a typed original
creation lookup delegate supplied by the active, pinned or historical caller. This avoids
a definition/hierarchy recovery dependency cycle. Definition Update and child creation use
the original operation domain, live claim and final enlisted reservation checks. Existing
targets covered by the original ceiling avoid journal-prefix scans. Accepted Update receipt
disclosure derives its target lazily, preserving absent/expired/dataset classifications.
Completed replay, interrupted/cancelled reconciliation and historical review use their own
Core-issued prior evidence. No new schema or project reference is introduced.

Project-scoped runs still admit only the original project and directly created children.
Source inspection found that grandchild reservation could precede the final creation
refusal; the source guard now rejects that reservation before any grant or row is created.
New proof covers organization creation beneath a created parent, Project-scoped edits,
lost-ack and completed replay, current revocation, historical unresolved updates and
interrupted/cancelled receipt recovery for Agent and Process executions. Initial production
compilation passed; the first test build found two incorrect test snapshot member accesses,
now repaired to use the catalog snapshot's Catalog property. Runtime proof remains open.

The first created-definition candidate executed 39 cases: 32 passed and seven failed.
All four new historical receipt cases and all 28 regressions passed. Three new live tests
incorrectly expected the wire summary to retain its JsonIgnore lifetime property; they
must capture that owner admission explicitly. Four reconciliation cases completed their
real owner reads and journal persistence, then compared nested immutable arrays by reference
after reloading. Their repair compares the serialized original journal prefix. Both are
test-only repairs; the failed candidate is retained without acceptance.
Source `44C6BFE2E316632D9EB2D4FB679393D1A6D87E579126077855E184C1AA3C164F`
and runtime `FA9527F0F26027BBB1DC60F7DCC1D84D5EA457D2F2281AA59300E69DC89FFF20`
were unchanged. Release build, final portability (15,540 findings), rendering, documentation
and whitespace checks passed. No observed process or database-name delta remained; seven
unowned TEMP paths were preserved. Evidence: `p03-created-definition-validation`.

The next candidate repairs those seven fixture cases and extends the actual seed-process
and Web-restart proof with two created-definition scenarios. Each creates three targets,
edits two, and exits after the final accepted edit without journal completion. Its fresh
Web observer must recover the original creation provenance, preserve the execution files
and receipt bytes, and refuse revoked reads or a replaced target lifetime. Existing four
restart scenarios remain regression cases. The transfer is test-only; the 32 passing
cases above retain their exact production-source basis. New runtime evidence is required.

The repaired candidate passed all 13 discovered cases: seven repaired live/reconciliation
cases and all six actual seed/Web restart scenarios. The new Agent and Process scenarios
recover the fifth committed definition edit from its original creation provenance, retain
the original execution/receipt bytes, and refuse revoked source reads and replaced target
lifetimes. All six fixture cleanup records passed. Source
`1BD30855B938231807B225A92D5A5F8CC4046309D6C3E3EA7C3C0CA7998A2A70` and runtime
`FCD6A3376528420459ED2A18B4E199682E59E1508D1685EB6F56E822FC181DBF` stayed unchanged.
Release build, final portability (15,540 findings), rendering, documentation and whitespace
checks passed. Final reconciliation found zero observed processes or database-name delta
and preserved seven unowned TEMP paths. Evidence: `p03-created-definition-restart-validation`.

Observation limitation: the resource observer rejected the edited lane manifest's stale
companion checksum before taking its baseline. The repaired observer started approximately
35 seconds after the first test command. The preceding closed candidate's final database
list supplies the pre-test database baseline; explicit command identities and all six
fixture-owned cleanup records supplement final reconciliation. This is passing functional
and restart proof, not complete periodic allocation observation. The gap is retained in
the compact [created-definition proof](../proof/2026-10-18-created-definition/evidence.json).

Source review before offline recovery found that the governed invocation wrapper adds an
original execution-store publication lease to the canonical catalog lease. Its current
recovery description forwards only the catalog location. Online release already disposes
both leases before marking the completion, but an offline recovery implementation must
capture and probe both original locations. That recovery capture is the next bounded
change; the offline release command remains unavailable until the complete protocol is
implemented and proved. No already-held completion is released by this observation.

The execution-source capture candidate now exposes the actual held file publication as
either shared-catalog or one original Project/Sandbox workspace. Workbench translates it
to a bounded same-host Projects recovery value. Original catalog-only wire bytes remain
unchanged; unknown source implementations, malformed scope keys and disposed leases refuse
capture. No new project reference or database schema is needed. Initial Integration Release
compilation passed. New actual-store cases cover shared/separate roots and disposed leases;
the owner-completion case uses the existing direct source/owner test seam because there is
no registered project-delete tool. It exercises real source guards, SQL deletion and stored
recovery bytes, while receipt/post-release observations remain that fixture's explicit seam.
Runtime validation and the stopped-host command remain pending.

`p03-execution-recovery-source-validation` passed the six codec cases and 47 of 48
integration cases. The new completion fixture queried provider metadata before calling
the existing provider's `CreateToolsAsync`, so it found no tool and never entered the
owner. The successor repairs that setup and reruns the affected publication/owner cases;
the remaining passing lifecycle tests need no repeat for this test-only correction.
Both frozen identities stayed unchanged (9,089 source inputs, digest
`BA4E103DF7D84BB4C67FA8692ECDF862BBFB9E42A7379729204A03C5369657C5`;
5,328 runtime inputs, digest
`F6126B030D49866A42196A0F746F6D975481BF408AA238340A60DBC67A08A9F1`).
Release builds, final portability enforcement at 15,552 reviewed findings, rendering,
documentation and whitespace checks passed. Observation started before the tests;
reconciliation found no observed process or database-name delta and preserved six
unowned TEMP entries. The failed fixture remains recorded as failed.

The first fixture successor passed 10 of 11 affected cases, then exposed a second setup
mistake: the internal invocation factory is constructed by the tool owner, not registered
as a DI service. The fixture now binds the same real source authority and governed claim
directly, matching the boundary it claims to test. Source `6A66E294B0515E1D53049F42528567472834CFA94BE04006C29157513DEC9854`
and runtime `7763C0D77C3A6D066E282DA92B5B3DC95F0BDF9E7493D1662F14BF11F2C89AE5`
stayed unchanged for that failed attempt. Its build/static checks passed; final resource
reconciliation found zero observed processes/database delta and preserved three unowned
TEMP entries. Evidence: `p03-execution-recovery-owner-repair-validation`.

The next candidate adds only the offline exclusion prerequisites and the corrected
source/owner fixture. Durable-file coordination now has an existing-directory path with
no directory creation, case probe or private-directory permission repair. Failed
post-open validation releases the handle before propagating the error. Persistence maps
the original supported workspace scope to the existing lock without reading a catalog;
activation exposes its existing exclusion independently of mutating record reads;
Infrastructure adds the named lifecycle EXCLUSIVE/NOWAIT guard. New tests cover missing
directories, cancellation, real writer contention, corrupt catalog preservation and
SQL ownership/namespace refusal. These helpers do not themselves release a completion;
the offline coordinator, participant validation and command remain open.

`p03-offline-exclusion-validation` passed **44/44**: 37 unit cases and seven integration
cases. The repaired owner test proves that real governed deletion persists the original
catalog and separate Sandbox execution-store binding, and releases both publications.
Real Project, Organization and Sandbox maintenance exclusions block normal execution
writers, preserve catalog bytes and refuse missing/invalid locations. SQL recovery refuses
a held writer immediately and keeps its own exclusion until rollback. Release builds,
final portability enforcement (15,561 findings after the reviewed 11 additions/two stale
entries), rendering, documentation and whitespace checks passed.
The 9,090 source inputs (`8D9BCD28D9DC268F08461B9AD91C7081D758F881917B80EEFB0B94F5E2613678`)
and 5,328 runtime inputs (`D0A32B436F3C59E605FBE1D62177D815F97F81CF06A239EB2548AB8CE85A0567`)
were verified unchanged. Observation preceded execution; final reconciliation found zero
observed processes or database-name delta and retained six unowned TEMP entries.
Reviewed evidence is in [the exclusion proof](../proof/2026-10-18-offline-exclusion/evidence.json).

Offline source review also found that an ordinary operation's lock uses its original
caller kind/key, while retained evidence currently has only the one-way caller binding.
The next change captures that exclusion input separately from authority; older incomplete
evidence remains readable but cannot authorize offline release. No command is exposed
until the entire exclusion and release sequence is implemented and proved.

The ordinary-caller candidate now captures kind/key as a recovery-only value, verifies
its original namespace-bound digest and shares the exact existing operation-key formatter.
It cannot create a current caller scope. Optional canonical encoding preserves the absent
legacy form, whose key resolver explicitly refuses. Four caller-kind codec cases and the
real governed deletion compare retained evidence against the owner's receipt identity;
validation for this candidate follows separately.

`p03-recovery-operation-key-validation` passed **42/42** (41 codec/identity regression
cases and the real governed owner case). The owner completion's retained kind/key,
namespace, epoch and operation ID match its committed lifecycle receipt. Both standard
Release builds, final portability enforcement at the unchanged 15,561 findings, rendering,
documentation and whitespace checks passed. Source `081C43673BACD678BA7718064DE2DECE0FB6E4C41F0A5591DA637FCFDA7DEFF7`
(9,090 inputs) and runtime `FF0401B310A299150162F76DE70DCC6C06A7890D69D99DB2B68EA8F40E79763B`
(5,328 inputs) stayed unchanged. Final reconciliation found zero observed processes or
database-name delta and preserved six unowned TEMP entries. See [the operation-key proof](../proof/2026-10-18-recovery-operation-key/evidence.json).

The participant-inspection candidate introduces the two data-only enlisted inspectors and
a bounded digest result. Agent original-binding validation is extracted from its live
owner. Workbench shares payload decoding and exact original source/completion validation;
its inspection projection checks the two-MiB payload cap in SQL before materialization and
does not load diagnostic error text. Neither inspector starts a callback or releases a
completion. Proof extends the real participant-preparation test, plus independent missing,
foreign, unbound, oversized and competing-row cases. Explicit profile composition and the
offline coordinator remain to be implemented after this boundary is validated.

The first inspector candidate passed six of eight cases. Both competing-row tests reached
the required PostgreSQL `55P03` NOWAIT refusal; Npgsql's normal EF query execution strategy
wrapped it in `InvalidOperationException`, while the tests expected the raw exception.
The test-only successor asserts that exact wrapper and inner SQL state. Production
inspection behavior is unchanged. Source `5CD64BD51886A930527FBA7EB15685C1F87251ECB4FBBD75970D8AB8A57A67C5`
and runtime `A6B6A83B8FC82AD9FAD256917546153C6130EDB957758E5BA009924C84F2F6BD`
stayed unchanged; build/static checks passed. Final reconciliation found no observed
process/database delta and preserved three unowned TEMP entries. Evidence:
`p03-recovery-participant-inspection-validation`.

The test-only successor passed both competing-row cases: **8/8 distinct participant
inspection cases now have passing evidence**. Its Release build, final portability
enforcement (15,561 reviewed findings), rendering, documentation and whitespace checks
passed. Source `F00195B8A2505ADA646988051F0A0DDC8F0001E5F2DCE6B5E1B7DAF13541D08C`
and runtime `F0944CF59A0275B0D71A8F4929243E93F63AA25C3C979F722272F29D09AAE487`
stayed unchanged. Observation began before execution; final reconciliation found zero
observed processes/database delta and preserved five unowned TEMP entries. See
[the retained inspection evidence](../proof/2026-10-18-recovery-participant-inspection/evidence.json).

The explicit existing-profile reader passed **32/32**: 22 new read-only profile/key
cases and ten native profile/Data Protection regressions. The normal profile owner now
shares connection-string construction with recovery. Inspection requires an explicit
current-schema PostgreSQL profile, exact host binding, unique identities, bounded private
files, and consistent completed migration evidence. Existing key reading disables
automatic generation and refuses writes. Tests retain all catalog/key bytes across reads
and refusals, including actual migrated native profiles with whitespace-bearing backups.
An initial build had a missing namespace import; the repaired standard Release build
passed before runtime execution. Twenty-one portability additions were reviewed, rescanned,
and baselined; final non-write enforcement passed with 15,582 findings. Rendering,
documentation and whitespace checks passed. Source `A79D114DD7D7497901438B05844B7DA01FFA1D9D1FD10AB10678CCA147F1CFB7`
and runtime `3DF94E4A6CBF6DC3643431517D80FD34DA22CB5BF619697373A26CA55B01F015`
stayed unchanged. Observation started before tests; reconciliation found no observed
process/database delta and preserved six unowned TEMP entries. This is Windows execution,
not a Unix runtime claim. See [the profile-reader proof](../proof/2026-10-18-existing-profile-reader/evidence.json).
The recovery coordinator, command, legacy installation and broader runtime work remain open.

The first existing-completion offline recovery candidate now implements the Projects
coordinator, bounded explicit-profile composition, early `project-deletion-recovery`
inspect/reconcile command, and complete preparation-registration capture. The real
PostgreSQL proof passed all **15 new recovery integration cases**, including three child
command invocations against native Agent/Workbench records, held lifecycle/preparation/
caller-session exclusions, two reconcilers, acquisition/release failures, and lost commit
or disposal acknowledgements. No callback runs during offline reconciliation. The child
uses a persisted profile despite deliberately invalid ordinary-runtime configuration,
cannot bind the occupied web port, and preserves participant rows, profile/key bytes and
schema. PostgreSQL microsecond precision is retained in the returned release timestamp.

The wider candidate passed 59/59 Unit, 61/63 Integration and 29/47 Components. Two old HTTP
tests still expected the disabled identity-only deletion route to succeed. Component
failures exposed legacy fixture entry points, missing typed participant fakes, and tests
submitting before the combined pending-index readiness. These are retained failures,
not an all-green acceptance claim; a separate fixture repair candidate follows.
Standard Release builds for all three suites passed. Seven portability additions and the
Web early-dispatch rendering fingerprint were reviewed; final non-write enforcement
passed with 15,589 findings, alongside rendering, documentation and whitespace checks.
Source `0133E0A6A41A7D316D29595F7926A315D5F045E265E72328D49C66D26A19E36E`
and runtime `18B67E813B35699CF83F6595120BC5ED0A2751D2321D35A40FFEDEABC2EF5F16`
remained unchanged. Observation started before execution; reconciliation found zero
remaining observed processes/database delta and preserved twelve unowned TEMP entries.
Evidence: `p03-offline-recovery-validation`. Legacy installation, activation/readiness,
the complete captured-source command matrix, and the broader runtime policy remain open.

The first fixture repair candidate passed both native HTTP cases and 42/47 component
cases. Five retained failures were stale refusal/notice expectations and one event
dispatched outside the renderer against a retired handler. A refused attempt does not
prove final absence of the original operation: the editor must retain its reference,
including across same-ID recreation. A late accepted deletion must not announce that a
successor editor was closed. The next fixture candidate asserts the exact stored receipt,
pending participant identity and blocked replacement write rather than restoring those
old expectations. No production guard changed. Release builds, static enforcement
(15,589), rendering, documentation and whitespace checks passed. Source
`56338CD2221F424BF99404C53B64B71F5C8DF255D759DC224C66BCEEDEA72140` and runtime
`ABF339CC39574DC3075288F486111C329E5C256E0E412DAC984FD9136FDC9FE9` remained unchanged;
observation preceded tests, with zero remaining observed processes/database delta and
eight unowned TEMP entries preserved. Evidence: `p03-offline-recovery-fixture-repair-validation`.

The next five-case retention rerun passed three and retained two assertion failures:
retiring the editor before its response can leave the attempt unconfirmed rather than
observing its refusal, and a failed custom participant can coexist with native Workbench
cleanup still pending. The successor test now asserts the selected participant separately
and preserves the other exact cleanup target. Its success notice says "Selected cleanup
completed" rather than implying completion of every participant. Source
`7D0354F4204FE42512AE4CAC180FB30D8DE0628EB6FDEA7E0C02BA1128999AE1` and runtime
`05DE9C8722590D58E68D5716B1C30269726AE79B05179CDD988234FB998B4CE3` were unchanged during
the recorded run. Final static enforcement passed at 15,589. Observation preceded tests;
zero observed processes/database delta remained, and three unowned TEMP entries were
preserved. Two compile-time assertion member-name mistakes were repaired before that
runtime candidate; their unsuccessful build records remain retained. Evidence:
`p03-offline-recovery-retention-validation`.

The captured-source command candidate adds three real Agent-source cases: canonical
catalog only, catalog plus Project execution, and catalog plus Sandbox execution. Each
captures the actual held source through the existing native owner seam and injects only
loss of its disposal acknowledgement. Child commands must refuse a missing original
directory, a held catalog/execution publication and a changed plan, then release exactly
once after those conditions clear. Native participant/receipt/retirement rows and source
file bytes must stay unchanged. All four offline-command cases and all five retained-editor
cases passed. Release builds, final portability enforcement (15,589), rendering,
documentation and whitespace checks passed. Source
`E82CCFD714810B563C95E108B5D37F63B96FA6AC3BA183E13F41706407587D02` and runtime
`47F09E53F276AE846DF95D32D77C19C224D8D715FECC7085D8C4FE24B6EB339E` remained unchanged.
Observation preceded execution; reconciliation found zero remaining observed processes
and zero database-name delta. Seven unowned TEMP entries were preserved. Evidence:
`p03-offline-recovery-source-validation`. An initial missing-using build failure was
repaired before freezing this successful candidate; its command record remains retained.

## Exact previous dataset selection restoration

The first restoration candidate passed 43/43 unit and 25/26 integration cases. All twelve
direct restoration cases, the actual child restore/release chain, four original-source
command cases and eight existing activation cases passed. The remaining existing
installation-audit fixture wrote protocol 2 as its unsupported version; Projects now
installs protocol 2. Both unsupported-version fixtures now derive a different version
from the installed pool. No production protocol guard was weakened.

The architecture review also identified a service/lease type cycle. The restoration
lease now receives only its bounded observation and cache-invalidation operations,
matching the ordinary lease's ownership direction. The next candidate reruns eleven
activation-service unit cases, twelve restoration cases, the failed installation audit
and five actual offline-command cases (29 cases total).

First candidate source `F3F41B4B14C106581D73DF3779A25B235CEBD67F8B23F803BAC9BED5623D29DD`
and runtime `8762196CC4CA3C5519FD88D6483593C9742094F2D708CB04D379529A004632F5` remained
unchanged. Standard Release builds, static enforcement (15,592), rendering, documentation
and whitespace checks passed. Reconciliation found zero remaining observed processes
and zero database-name delta; seven unowned TEMP entries were preserved. Two initial
compile errors (a named argument and missing namespace import) were repaired before this
freeze. Evidence: `p03-dataset-restoration-validation`. The candidate's one runtime
failure and architecture repair remain visible; it is not presented as full acceptance.

The repair candidate passed all 11 selected unit cases and all 18 integration cases,
including every branch of the repaired installation audit. Source
`8F34B2088801199DC808DD02CBCC2A74C0FDE1D8EF48DD0A6FB4490F20580635` and runtime
`C8B713C6AA287E40AB98352DD4CF75A022D8F728EB81589033F45C3B7A58C5D5` remained unchanged.
The affected Infrastructure project and both test assemblies passed standard Release
builds. Fresh portability enforcement passed at 15,592 without rewriting the baseline;
rendering, documentation and whitespace checks passed. Reconciliation found zero remaining
observed processes and zero database-name delta; thirteen unowned TEMP entries were
preserved. Evidence: `p03-dataset-restoration-repair-validation`.

CodeAnalytics `snap-20261019000956-83b97284` confirms the new type cycle is gone. Its
two-project scope contains 186 documents, 612 types and 1,249 dependency facts. The
existing Infrastructure module cycle remains. All 189 workspace error diagnostics refer
to existing NuGet vulnerability advisories; ordinary Release builds pass, so the analysis
is explicitly degraded. See [the restoration architecture gate](2026-10-18-dataset-restoration-architecture.md).
Restoration is proved; native pending inventories, readiness/rotation guards, legacy
installation and the broader agent/process policy remain open.

### Native inventory and dataset guard candidate

The next candidate implements the planned SQL-only owner inventories, shared completion
batch interpreter and readiness/rotation checks. Native reads use 128-header keyset pages;
Workbench additionally bounds raw SQL payload materialization at 2 MiB per record and
8 MiB per page. Completion batches contain at most 64 identities. The migration
`20261019003214_IndexUnfinishedProjectDeletion` adds only three partial indexes and changes
no business row or binding. Direct production, migration and test Release builds pass;
the canonical pending-model check passes and idempotent SQL contains only those indexes
and its migration-history row.

Discovery confirms 102 integration cases: 19 new readiness cases, nine activation cases,
twelve restoration cases, three lifecycle migration cases, eight native participant cases
and 51 lifecycle operation cases. Acceptance is pending execution on a frozen candidate.
The new tests include malformed bindings, more than one header page, a byte-limited short
page with later invalid work, oversized first records, deletion after preflight, previous
selection restoration, SQL-committed rotation resume and five actual EXPLAIN plans with
10,000 terminal records per owner. Evidence root: `p03-dataset-readiness-validation`.

CodeAnalytics `snap-20261019003445-cfc506a8` reports no new guard/inventory cycle. The scoped
analysis retains the existing module/type cycles and 189 NuGet-advisory workspace errors;
there are no other workspace error diagnostics. No project reference changed. The prior
two restoration candidates now have compact retained proof, including their unsuccessful
fixture result, under `proof/2026-10-18-offline-recovery`.

The fresh portability scan adds four reviewed generated model descriptors only:
`RootPathState`, `RootPathSyntax`, `RaisesEscalation` and `EscalationCount` in the new
migration designer. They repeat existing mapped properties and introduce no physical path
or process-launch behavior. There are no stale findings. The reviewed baseline advances
from 15,592 to 15,596; final enforcement runs without baseline-writing enabled.

The frozen candidate passed all 102 integration cases in 329.80 seconds. Source
`27CD39D7B67CACCE9926FD833EDD0A95AFFE20DA8FF43206FD46A06A2C7A8491` and runtime
`D29AE9521AAD7EF9EE983341641787C14F1E663CB7EF3053660BA07DC892114B` remained unchanged.
Final reconciliation found zero observed processes and zero database-name delta; four
unowned TEMP entries were preserved. The five indexed existence/header probes took
0.026–0.060 ms with 10,000 terminal records per owner. Those measurements do not assert
constant overall readiness latency or the plan of every parameterized adapter query.

The next focused candidate addresses initial Bootstrap/Attach with legacy work and
captures actual first/subsequent-page adapter query plans. This is a follow-up to the
proved inventory/rotation foundation; legacy installation and full runtime policies
remain open.

The follow-up is authored as `p03-dataset-selection-readiness-validation`. Selection
publication retains finite-pool validation and locked rotation checks, then readiness
runs before the caller can continue startup. Four fresh/interrupted Bootstrap/Attach
cases require the same repairable selection and unchanged legacy records while readiness
still refuses. Both inventories now emit an explicit range predicate for subsequent
pages. The plan test captures six actual adapter commands and their parameters, checks
their partial indexes/range conditions and rejects terminal-table sequential scans.
Discovery confirms 45 cases: 23 readiness, nine activation, twelve restoration and one
real offline restoration/release command chain. Direct affected production and test
Release builds pass; portability enforcement passes at 15,596 without a baseline change.
Runtime acceptance remains pending this candidate's frozen execution.

The selection candidate passed 44/45 cases in 167.99 seconds, including all four
fresh/interrupted Bootstrap/Attach cases. Its performance gate failed: with 131 pending
records PostgreSQL chose a sequential scan for the Agent existence probe. The matching
row happened to be first (0.017 ms), so this is evidence of an unsafe growth assumption,
not a measured latency regression. Source
`E474EC19F5DA29498AC301EBE472C763375C4DFA41C8109AEA29B18B42F7CAB0` and runtime
`FC551F56DBD1C402D071149D8771269A0861F67F65B85D1C184C32D3AFB069BA` remained unchanged.
Reconciliation found zero observed processes, zero database-name delta and six unowned
TEMP entries preserved. The failed candidate and its actual plan are retained in compact
proof. CodeAnalytics `snap-20261019005101-cfc506a8` has the same 5,191 dependencies and
cycle set, with 189 advisory workspace errors and no other workspace error.

The repair candidate `p03-dataset-indexed-probe-validation` replaces existence predicates
with a count over an ordered subquery limited to one matching identity. The test now
places pending records after terminal history and captures seven actual production
commands, including the Projects Awaiting probe. It requires partial indexes, cursor
range conditions and no sequential scans of terminal-bearing tables. Planner settings
are unchanged. The same 45 behavioral/performance cases remain the intended proof floor;
build-backed discovery and runtime acceptance must be reconfirmed.

The indexed-probe candidate passed all 45 cases in 169.92 seconds. The seven actual
production plans use the intended partial indexes, and cursor queries include range
index conditions; no terminal-bearing table uses a sequential scan. Execution times
are 0.032–0.363 ms on this fixture, not a production latency guarantee. Source
`1844D943EF95171B7431AB39D830CB62AE5A0CED0E244681BCB7F9EE8504252E` and runtime
`2B65AB0BE1A6D3AC469BC358A5B41D969F62C52E0AD6BD35AE4BAA0C654AFBEE` remained unchanged.
All affected Release builds and final portability enforcement (15,596) pass. Cleanup
finds zero observed processes, zero database-name delta and three unowned TEMP entries
preserved. Compact proof includes all seven plans and per-case outcomes. Initial
selection/readiness and actual cursor-plan follow-ups are closed; legacy installation
and runtime snapshot policies remain open.

The next candidate, `p03-legacy-owner-validation`, introduces the native binding
foundation only. Workbench's additive migration adds a nullable namespace/completion
pair with a nonempty, paired, DeleteProject-only constraint. The payload reader is shared
by all participant/processor/recovery/inventory entries; installed headers must agree
with any embedded context. No legacy release permission or installer command is enabled
by this candidate. Agent and Workbench data-only inspectors share validation with their
legacy inspection/binding methods. Workbench inspects bounded headers before reading
one bounded payload at a time; its SQL claim digest avoids returning error text.

The command cleanup fix preserves Unconfirmed commit knowledge. The pure outcome test
uses the Integration assembly's existing Web friend access instead of widening the Web
assembly API. Two initial test compilation failures (an incorrect fixture profile
property and an inaccessible Unit test helper) are retained in the candidate's command
records and corrected before discovery. Production builds passed on the first attempt.

The portability delta is four generated model descriptors in the new migration designer:
RootPathState, RootPathSyntax, RaisesEscalation and EscalationCount. They repeat existing
mapped properties without introducing path or process behavior. There are no stale
findings; the final scan and reviewed baseline enforcement are required before execution.

Build-backed discovery confirms 19 Unit cases and 102 Integration cases, including 17
new native-owner/outcome cases. Direct Projects, Agent Framework, Workbench, Web and
migration builds pass, as do the corrected test builds. The pending-model check is clean;
idempotent SQL contains only the two nullable columns, their check constraint and the
migration history row. Final portability enforcement passes at 15,600 reviewed findings.
CodeAnalytics `snap-20261019011625-bb93e51f` covers the three owners, Composition and Web:
945 documents, 2,631 types and 4,039 dependencies. No project reference changes or new
shared module/type cycles are present. This scope includes Web instead of Infrastructure,
so total edge counts must not be compared with the earlier inventory scope. All 189
workspace errors are NuGet advisories; direct builds provide compiler evidence. Runtime
acceptance remains pending frozen execution.

The legacy-owner candidate passed 19/19 Unit cases and 102/102 Integration cases;
integration execution took 277.78 seconds. Source
`D1E840B57E13945C51FF58762064CFE51C004B3D535F74C2611A5CA08D427F7A` and runtime
`B919FB70922C736831BFCDCBE9B1E91FB31157CF1BDEBFBDEADFB6B03B5CBE6D` remained unchanged.
Cleanup found zero observed processes and zero database-name delta; six unowned TEMP
entries were preserved. Compact proof retains all command attempts, 121 case outcomes,
architecture limitations and final reconciliation. The native binding foundation is
proved; the offline legacy installer and full runtime snapshot behavior remain open.

## Legacy installation candidate

`p03-legacy-installation-validation` adds separate inspect-legacy/install-legacy commands
and a Projects-owned installer. It validates native retirement and the current dataset,
selection and canonical catalog; holds the existing file/source/lifecycle/preparation
exclusions; and atomically creates an Awaiting gate with both native header bindings.
It retains the inspected plan through retries and independent lost-acknowledgement
observation. Release remains a separate explicit inspect/reconcile operation. Composition
constructs data-only inspectors without resolving live participant dependencies.

The final candidate discovers 36 Unit and 125 Integration cases. These include the real
offline child command followed by held and released live processor entry, competing
installers, lost commit/cleanup acknowledgements, rollback of the second binding, changed
plans and unsupported subjects. Both observe and exclusive subject queries are captured
for actual EXPLAIN ANALYZE against 10,000 terminal records. Agent uses its existing
project/lifetime index; Workbench adds an all-DeleteProject subject index, including
completed partners while excluding node history.

Direct affected production and test Release builds passed. The migration model check is
clean; the idempotent script adds only the partial index and migration history row. The
reviewed portability delta consists of the same four generated property descriptors
in the new migration designer, with no stale findings or new runtime path/process
behavior. Final enforcement passes at 15,604. CodeAnalytics
`snap-20261019015122-0fd52be5` reports 948 documents, 2,638 types, 4,065 dependencies and
no new shared cycle or project-reference change. Its 189 workspace errors are package
advisories; direct builds provide compiler proof. Runtime acceptance remains pending
frozen execution and terminal resource reconciliation.

The first installer candidate executed all 161 selected cases: 36/36 Unit and 122/125
Integration passed. The real offline child install/retry/release/native-dispatch path
passed. Three new test arrangements failed: both query-plan tests used GetInt32 for
PostgreSQL 18's decimal Actual Rows value (`17.00`), and the competing-installer test
held its first actor behind a barrier while waiting for the second actor's selection
timeout, exhausting both five-second budgets. The observed operations safely refused.
The successor reads the numeric row count as a double and releases the first actor
promptly after starting a competing distinct completion; both tasks drain before
fixture cleanup. It must prove one binding and an exact retry without new acquisition.

Source `1C3A4FB3D1AFD0E521875AAA00FBAD836D692AD95FAEB1428825C5E0ED91A831` and runtime
`F08597DED6570870FD00477DDE64D2E3ED5A6A45CE9078C9EA898F8DEBDB74AB` stayed unchanged.
Reconciliation found zero observed processes and database-name delta, preserving seven
unowned TEMP entries. The unsuccessful candidate and all failures remain in compact
proof. Production source is unchanged by the test repair; the focused successor must
record that exact bridge, discover and rerun the three failed cases, and pass fresh
portability enforcement before this installer is accepted.

The focused successor passed all three cases in 17.59 seconds. Its bridge verifies all
5,201 production source paths and hashes are identical to the initial installer run.
Source `868012231E687745580899812AB2872309980BA3D618556BFE3242B5FF8C664B` and runtime
`6303BEAC86C17BB89726D13C165D6D9DBC43827798670F2D4768306D97C25B79` stayed unchanged.
Four actual owner query plans pass the expected-index, project-range, no-sort/no-scan
and bounded-row assertions. Reconciliation found zero observed processes and database
delta, preserving three unowned TEMP entries. Final portability enforcement remains
15,604. The supported native legacy installation/release slice is accepted by
[its architecture gate](2026-10-19-legacy-installation-architecture.md); broader runtime
policy, governed caller coverage and sustained capacity remain open.
