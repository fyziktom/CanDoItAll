# P03: deletion and hierarchy implementation checkpoints

Status: source-only design in the continuation worktree. No P03 implementation,
migration, discovery or execution has occurred. This card follows
[original-state intents](P03-original-state-intents.md), the accepted P02 definition
protocol and the separately reviewed retention candidate. It does not declare Projects
deletion or hierarchy protected today.

## Decisions and the existing seams

The four intents are ordinary deletion, ensure hierarchy link, remove observed link
and reconnect observed link. Full/scalar definition saves keep their existing protocol.
There is no field-patch language, universal mutation coordinator or new cross-module
transaction abstraction in this slice.

| Source seam | Consequence for this implementation |
| --- | --- |
| `Projects/ProjectModels.cs`: `DeleteCoreAsync` | Preserve participant preparation, retirement, enlisted search/routing deletion and postcommit participant completion. Require the original definition expectation before any preparation. |
| `Projects/ProjectMutationAuthority.cs`: `ProjectSourceMutationScope` | Source lease acquisition precedes BEGIN; source validation and complete sorted owner keys remain in their existing order. Its final required-admission check cannot be applied blindly to the project just deleted inside that transaction. |
| `Projects/ProjectDefinitionReceiptReader.cs` | Its Found path requires current target availability. It is deliberately unsuitable for the minimal accepted-deletion receipt. Keep that definition disclosure policy unchanged. |
| `Projects/ProjectDefinitionOperationRecords.cs` | Definition receipts require an accepted root revision and definition command kind 1–3. Do not put a fake revision or a deletion/hierarchy payload into this table. |
| `Projects/ProjectOperationEpochService.cs` | Reuse dataset admission, issued epoch deadlines, retirement and bounded maintenance. Every receipt-family existence/purge predicate must account for the added family before that family can write. |
| `Projects/ProjectDeletion.cs` and its two participant implementations | Recovery IDs already identify durable Workbench and agent-access cleanup. Completion is owned there. The operation receipt does not certify filesystem, provider or access-revocation completion. |
| `Projects.Contracts/ProjectPortfolioModels.cs` | Existing hierarchy DTOs omit the stored edge Guid and are not a coherent original-state witness. Add a bounded mutation opening instead of claiming independently loaded lists form one snapshot. |
| `Web/wwwroot/js/projectPendingSubmissions.js` | Current IndexedDB database/version and four-part key are definition-specific. New lifecycle records need an explicit family boundary and additive storage evolution; old unresolved definition records must survive unchanged. |

Paths abbreviated above are under `src/Modules/CanDoItAll.Modules.*` or
`src/App/CanDoItAll.Web` as appropriate. No Infrastructure dependency on Projects,
Workbench or AgentFramework is added. Composition continues to supply trusted caller
and access adapters. Contracts remain transport/data contracts without EF or host auth.

## P03a: witnesses and owner-local durable evidence

Implement the following foundation before exposing a protected lifecycle command.

1. Add immutable typed original-state contracts. Delete carries dataset namespace,
   project lifetime and original definition revision. Ensure-link carries namespace
   and both original endpoint lifetimes. Remove carries the original edge Guid,
   endpoint pair and lifetimes. Reconnect carries the original source edge plus the
   child, old-parent and new-parent lifetimes. No root scalar revision is required for
   relationship-only intent. Reject missing/empty/duplicate/mismatched members before
   copies, normalization or serialization.
2. Add a bounded coherent mutation-opening read in Projects. For a requested subject
   and explicitly selected endpoints, one repeatable snapshot supplies dataset
   identity, endpoint lifetimes and the requested edge identities. Page/list discovery
   may remain separate; it is not the witness. Changing selection requires a new
   explicit opening for that selection before confirmation, not a dispatch-time
   expectation refresh. Native and governed reads retain the witness they actually
   showed or evaluated. Preserve current display DTOs unless a concrete consumer needs
   their additive edge identity.
3. Add one Projects-owned lifecycle receipt table and codec, with four typed command
   and acknowledgement variants. Reuse namespace/epoch/caller identity types; keep a
   distinct `project-lifecycle-v1` protocol family, digest domain and operation-lock
   key. Family is part of the address even when implicit in the typed endpoint/table.
   The same UUID in the definition family is a different address, not evidence for
   this effect. One lifecycle address cannot change its command kind or digest.
   Do not expand `ProjectDefinitionAccepted<T>` or weaken its existing constraints.
4. Reuse the existing epoch admission implementation through a small internal Projects
   admission seam only where both concrete protocols need it. It carries bounded
   address/version/digest data, not arbitrary mutation delegates or injected effect
   callbacks. A lifecycle-specific transaction entry preserves Serializable isolation
   for hierarchy/deletion and the current source/key order. No global isolation change.
5. Add a lifecycle-specific coherent observation query: one MVCC statement reads exact
   activation, owner watermark, epoch and caller-partitioned receipt. Copy the bounded
   bytes, release the database connection, then apply current disclosure policy. Do not
   hold an owner transaction connection while opening an independent receipt reader.

Persist schema, normalization and result versions separately, along with command kind,
semantic digest, accepted timestamp and bounded typed payload. The table has no foreign
key to a live project: deletion must not cascade away its evidence. The address key
starts with namespace/epoch, supporting exact lookup and bounded retirement scans.
Target/edge IDs in the acknowledgement are enough for indexed validation; do not copy
names, descriptions, storage locators, credentials, whole graphs or participant errors.

Initial bounds are owner versioned limits, not the much larger definition payload limit:
at most three endpoint admissions, one source edge, and the registered deletion
participants' bounded recovery references. Validate participant count and ID byte length
at composition/preflight before preparation; the existing participant ID constructor
has no length cap. Select the concrete byte limits from encoded worst-case fixtures in
this checkpoint and persist the codec version; do not accept unbounded strings merely
because today's two participant IDs are short.

The migration adds this owner table without rewriting existing definition receipts or
rotating the namespace. Before writes are enabled, maintenance must purge across both
receipt families under the same aggregate row, payload-byte and cycle budgets. An epoch
or inactive owner-state row is removable only when neither family retains evidence.
Use a bounded fair selection across families so a sustained definition backlog cannot
starve lifecycle cleanup; do not double the existing per-cycle limits. Target transfer
residue includes either receipt family, while an empty initialized epoch alone remains
transferable. Business transfer DTOs never transport receipts or operation epochs.

## P03b: ordinary deletion and its complete caller cutover

Expose a concrete `ProjectDeletionCommand` / `ProjectDeletionAccepted` owner boundary,
with typed Accepted, Rejected and Unconfirmed outcomes. The envelope identifies the
original family/address, caller binding and versions. Adapt the native Projects page
and HTTP delete route together; a route ID without original expectation must refuse.
No currently discovered governed tool performs ordinary project deletion, so this
checkpoint does not invent one. Creation compensation continues through its existing
reservation/lifetime/fingerprint contract and remains distinct from ordinary deletion.

Under the existing deletion preparation keys, project key and hierarchy key, validate
the original root revision/lifetime and active namespace before participant Prepare,
reservation cancellation, search deletion or routing deletion. Preserve the root's EF
original concurrency token for the delete. A missing/recreated target rejects a new
command; it no longer silently invokes orphan cleanup. Existing cleanup recovery remains
available through exact participant/recovery routes. This is an intentional old-request
cutover, not an undocumented compatibility fallback.

Admission must distinguish the target being retired from still-live authority sources.
`ProjectSourceMutationScope.CommitAsync` currently requires all captured admissions
again after SaveChanges; adding the deleted target to that list would reject every
successful delete. Use a deletion-specific transaction sequence: validate and lock the
target before effects, delete with its original CAS, and retain current source checks
for any source the operation actually uses. Do not disable the final admission check
globally or remove a live hierarchy endpoint from it. Initial ordinary delete has no
governed source authorization; creation compensation keeps its existing sequence.

In the same database transaction, write the retirement, delete owner rows/search/routing,
prepare participant obligations and insert the accepted-deletion receipt. The immutable
acknowledgement contains only original project identity/lifetime/revision, accepted time,
replay promise and ordered `(participantId, recoveryId)` obligations. Its outcome is
**DeletionCommitted**. Empty obligations mean no participant work was prepared; nonempty
obligations mean work was scheduled at acceptance, not that it is still pending now.

After known commit, preserve the exact accepted acknowledgement before transaction,
source, connection or service-scope cleanup and before activity publication or participant
Complete calls. A later cleanup/publication failure cannot become a retryable delete
failure. An exception during CommitAsync without acknowledgement is Unconfirmed; do not
compensate or submit a new operation. Exact replay reads the receipt and does not rerun
Prepare, delete, activity publication or external cleanup. A known PostgreSQL rollback
(serialization/deadlock) may be classified as this attempt's rejection only after a
fresh outside-transaction exact lookup; it cannot erase a prior ambiguous attempt.

Participant completion remains a separate operation. Return or show its current status
separately from the immutable accepted acknowledgement: Pending/Processing/Failed,
Completed, CompletedWithWarnings or Unavailable. Missing recovery evidence is Unavailable,
not Completed. Add a narrowly keyed participant observation by original project lifetime
and recovery ID if the caller needs current status; do not make every receipt lookup
scan `ListPendingRecoveriesAsync` and `ListCompletionNoticesAsync` across all projects.
The two existing participant implementations already own their durable records and are
the correct place for that bounded lookup. Existing durable claims/leases govern retries.

Receipt expiry never deletes unfinished participant obligations, and cleanup completion
does not rewrite the accepted operation. Detailed warning locators/messages stay behind
the existing current cleanup-disclosure policy. Do not embed them in the minimal receipt
or interpret a subsequent project at the same ID as authority over the old cleanup.

### Disclosure after the target has disappeared

For native local-workspace and HTTP callers, a minimal deleted acknowledgement requires
the exact original caller binding, active dataset namespace and the current Projects
read capability under the host's existing trust policy. HTTP selects authenticated
subject or open-API scope from current server configuration; the client cannot select
it. Native local workspace uses its current trusted boundary. No live-project existence
check applies to these identity-only bytes, and a newly created lifetime at that ID
neither grants nor steals them. A capability or caller change yields unavailable/context
changed evidence and never NotObserved in a newly selected partition.

Cleanup details/retry still require their current read/write authority and exact original
participant recovery, independent of receipt possession. A future governed deletion
adapter needs a reviewed post-deletion source-disclosure policy before it is admitted;
do not let a lease that depended on the now-deleted target manufacture that policy.

## P03c: hierarchy and governed caller cutover

Implement concrete ensure/remove/reconnect commands in one hierarchy writer using the
existing sorted endpoint keys and global hierarchy key. Keep the complete graph cycle
check and Serializable semantics for this checkpoint. The durable receipt is inserted
in the same transaction as the edge effect or accepted ensure-present no-op.

| Intent | New operation result | Exact replay |
| --- | --- | --- |
| Ensure link | Validate original endpoint lifetimes/current authority; return the exact existing edge or create one. Preserve existing successful no-op semantics. | Return the original resulting edge identity, even if the graph changed later; do not recreate it. |
| Remove observed link | Require original source edge ID and endpoint identities. Missing source remains a refusal; a replacement edge is a conflict and survives. | Return the accepted removal identity without interpreting current absence as evidence. |
| Reconnect observed link | Require the original source edge. Preserve an already-existing target edge identity; otherwise create a new target edge after cycle validation, then remove only the original source. | Return both original removed edge and accepted resulting edge identities; never redo the move. |

An endpoint deletion/recreation or namespace change rejects old intent. A project name
change alone does not. Jointly cycle-forming commands remain serialized through the
existing hierarchy guard. Do not replace it with endpoint-only locks or infer cycle
safety from definition CAS.

Cut over HTTP hierarchy routes, the Workbench hierarchy dialog and
`ProjectStructureAgentService.ChangeSubprojectAsync` together. The governed hierarchy
read/tool input must expose and preserve the observed edge witness. Current just-in-time
endpoint admission capture remains authority validation; it cannot replace the original
observation. Runtime effect identity, source binding and current source lease come from
the existing admitted execution boundary, never arbitrary tool arguments. If that durable
runtime boundary is not ready, the governed cutover remains closed rather than minting
a fresh owner operation on each continuation.

Human minimal hierarchy receipts use the same current Projects-read policy as minimal
deletion receipts. Governed receipt reads additionally require current source/endpoint
disclosure under the original source binding. Revoked/retired source access yields
DetailUnavailable, without effect execution. Do not acquire a mutation lease merely to
read a receipt. There is no claim that a historically accepted edge still exists now.

## Shared recovery and compatibility rules

Lifecycle lookup variants are Found with its exact typed acknowledgement, NotObserved
Open/Draining, ClosedAbsent, Expired, DatasetChanged, CallerContextChanged, InvalidContext
and DetailUnavailable. Keep the existing meanings: Open absence does not prove a prior
dispatch failed; ClosedAbsent is authoritative only from one committed closed-epoch
snapshot above the retirement watermark with full family retention. A past minimum
retention promise does not invalidate physically retained ClosedAbsent proof. Expired
and unsupported/corrupt payloads never authorize reissue. Lookup is read-only.

The frozen command, codec versions and family/address survive native restart. Use an
additive IndexedDB object store with its own family key and reuse the already-reviewed
strict-durability transaction semantics; preserve the version-one definition store and
its unresolved records. A blocked upgrade or quota failure visibly blocks dispatch.
Persist Prepared, then Dispatching before owner call; persist exact Accepted before
local reconciliation. Atomic revision checks prevent same-operation cross-tab downgrade
or stale clear. Storage events never submit. Unconfirmed remains sticky across later
attempt-scoped rejection or open-epoch absence. Browser storage remains bounded same-
origin plaintext; never store source lease credentials, tokens or the whole editor.

The lifecycle digest/result versions are independent of definition version one. Retained
versions must remain readable for their promised horizon. Unsupported retained versions
return DetailUnavailable; never silently renormalize an old frozen command with new
rules. Upgrading browser/HTTP metadata must preserve family, original versions and epoch
as the existing decimal-string wire token. Define limits and OpenAPI presence rules at
the boundary, before allocating large raw request copies.

## P03d: measured closure and retirement of unsafe entrypoints

Remove or make internal the old public mutation entrypoints after every real caller is
cut over. Test setup uses explicit original witnesses; it does not justify a production
tokenless overload. Creation compensation and participant cleanup are separate documented
paths. Search the real caller perimeter again before closure, including import/child
creation hierarchy writers, without changing their independently authorized semantics.

Hierarchy cost remains proportional to the complete edge graph under a global key.
Measure small and retained-size synthetic graphs, 8/32 disjoint versus contended writers,
query count, wait time and p95/p99 before choosing a graph-lock/query redesign. Deletion
cost includes participant preparation and child/search/routing work; measure graph and
owned-object counts separately, and keep postcommit cleanup latency separate from database
acceptance. Receipt replay must remain one bounded coherent observation plus current
authorization; it must not traverse the graph or scan all cleanup records. Extend the
existing opt-in harness with a separately versioned workload, not a replacement for the
protected-definition baseline. No throughput or bounded whole-project delete claim follows
from the small command/receipt size.

Each checkpoint has owner review, source-derived discovery expectations, canonical builds,
focused real-PostgreSQL barriers/faults, portability enforcement and a commit. Do not expose
an intent between its correctness change and its durable caller/recovery cutover. P03a may
land dormant; P03b and P03c are complete vertical cutovers, in that order.

## Existing tests and proposed proof

The following is a bounded source inventory, not a discovery/execution result or all
Projects coverage. Selected files have only Fact/InlineData rows; no MemberData/ClassData.

| Current class (assembly) | Source rows |
| --- | ---: |
| ProjectDeletionIntegrationTests, including Residuals partial (Integration) | 20 |
| ProjectsApiDeletionIntegrationTests (Integration) | 2 |
| ProjectDeletionSharedStorageConcurrencyIntegrationTests (Integration) | 1 |
| ProjectLifecycleMutationCoordinationIntegrationTests (Integration) | 4 |
| ProjectsServiceIntegrationTests (Integration) | 8 |
| ProjectStructureHierarchyLifetimeTests (Components) | 18 |
| ProjectStructureProjectHierarchySelectionPolicyTests (Unit) | 10 |
| ProjectDeletionStatusSurfaceTests (Projects.UI.Tests) | 7 |
| **Selected existing total** | **70** |

Proposed additions below are required assertions, not claimed new test/discovery counts.
Finalize actual row counts from the implemented source before execution.

- P03a: each typed codec round-trips; reused address/different kind or digest refuses;
  limits/version rejection occurs before effect; one coherent opening survives a concurrent
  edge/root change; both receipt families preserve watermark/closed absence, bounded shared
  purge and inactive cleanup; transfer refuses lifecycle receipt residue but permits empty
  epoch metadata; existing definition receipts and native pending records remain readable.
- P03b: stale revision rejects before any Prepare and preserves current owner/projection
  rows; fresh deletion commits receipt and obligations atomically; actual commit followed
  by lost acknowledgement or disposal/publication failure retains durable accepted evidence;
  known rollback leaves no receipt/obligation; replay never repeats Prepare/Complete; deleted
  target lookup succeeds under current read capability; caller/namespace/revocation and a
  new same-ID lifetime do not leak or remap evidence; cleanup observation distinguishes
  pending, warning-bearing completion and missing evidence; creation compensation remains
  unchanged. Fault tests use existing real PostgreSQL fixtures and independently read state.
- P03c: remove-and-recreate ABA survives stale remove/reconnect; exact source edge succeeds;
  an existing reconnect destination keeps its ID; ensure no-op is receipted; scalar edits
  do not reject relationship intent; endpoint recreation/source revocation does; joint-cycle
  commands cannot both commit; replay after later graph edits returns historical identities
  without effect; governed proposal and continuation retain original witness/address.
- Caller proof: HTTP omissions fail before dispatch; native selection change cannot retarget
  a captured operation; accepted deletion stays accepted when cleanup/read refresh fails;
  restart and two-tab recovery preserve original lifecycle records; stored definition-v1
  records survive the upgrade; Unconfirmed never becomes a fresh automatic operation.

Open release gates are runtime governed identity readiness, owner receipt-family migration/
maintenance parity, native additive storage compatibility, measured graph/deletion tails and
the actual validation above. They are implementation gates, not unresolved authority choices.
