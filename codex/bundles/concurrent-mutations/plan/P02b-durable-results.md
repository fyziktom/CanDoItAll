# P01b / P02b — Projects durable results and atomic projection

Status: implementation card prepared on 2026-10-16 against the P02a candidate.
This is design evidence, not implemented or validated behavior. Parent execution
still owns build, test, migration and commit ordering. The contract is
[06](../../../../docs/architecture/concurrent-mutations/06-operation-lifecycle-contract.md)
and [07](../../../../docs/architecture/concurrent-mutations/07-projects-contract-cutover.md).

## Outcome and scope

A protected create/full/scalar definition command commits its definition, relational
search document and exact accepted operation result in one PostgreSQL transaction.
The same complete address and semantic payload returns that result even after later
edits, a lost response or restart. An uncertain outcome never becomes an automatic
new operation. Finite closed epochs and a retained watermark prevent replay after
purge; offline dataset activation prevents old addresses executing after restore.

Do not migrate Process authoring, workflow/execution journals, deletion, hierarchy,
starter objects or arbitrary owner writes into a new shared framework. Preserve their
existing owners. Definition creation may still participate in existing reservation
and compensation protocols; neither protocol substitutes for the operation receipt.
Activity remains an explicitly optional postcommit action. Its failure cannot change
the accepted result and its delivery is not guaranteed by this card.

## Source evidence and the exact reuse boundary

| Existing source / member | Reuse or required change |
| --- | --- |
| `src/Modules/CanDoItAll.Modules.Projects/ProjectDefinitionWriter.cs`, `ExecuteCoreAsync`, `ReplaceChildrenAsync`, `BuildProjection` | Keep the only definition write path, normalized intent, original root CAS, linear identity maps and in-transaction acknowledgement capture. Add operation admission/replay around this path rather than introducing another writer. |
| `.../ProjectMutationAuthority.cs`, `ProjectSourceMutationScope.BeginDefinitionAsync`, `CommitAsync` | Keep source-first lease acquisition, source recheck, complete project/identity/hierarchy locks and final source/lifetime checks. Add an explicit definition-protocol stage for dataset/epoch guards and receipt-before-target admission. Ordinary BeginAsync consumers do not change. |
| `src/Foundation/CanDoItAll.Infrastructure/Persistence/CoordinatedDatabaseTransaction.cs`, `Enter`, `CreateEnlistedAsync` | Existing same-connection/same-transaction enforcement; no generic unit-of-work or new enlistment abstraction. |
| `.../Search/SearchIndexing.cs`, `ISearchIndexService.UpsertForMutationAsync` | Already creates an enlisted SearchDbContext. Call it before the definition transaction commits; remove the writer's delayed `ProjectAsync` callback. Search retains its entity/mapping ownership. |
| `.../Persistence/ApplicationManagedConcurrencyTokens.cs`, `Stamp` | Modified roots receive the final token during SaveChanges. Receipt serialization must use that final value, not a provisional token or subsequent read. |
| `src/Processes/CanDoItAll.Processes.Persistence/EfProcessAuthoringStore.cs`, `CommitAsync`, `FindReceipt` | Useful local precedent for a locked operation key, digest comparison and receipt in the owner transaction. Its profile/caller/id schema and unbounded lifetime are not the Projects schema and are not silently changed. |
| `.../ControlPlane/CanonicalRuntimeDatabase.cs`, `Generation` | Process value zero does not identify restored data. Do not reuse it as the namespace. |
| `.../ControlPlane/ControlPlanePaths.cs`, `ControlPlaneFileCoordination.cs`; `.../FileSystem/DurableFileWriter.cs` | Reuse contained private paths, existing durable file replacement and bounded cross-process file coordination. Add a dedicated dataset-activation coordination kind/path; no ad hoc File.WriteAllText or lockfile protocol. |
| `src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`, `AppDatabaseBootstrapper.EnsureProfileReadyAsync` | Existing explicit-profile migration/readiness sequence. Call dataset validation/provisioning and Projects readiness from composition after migrations; Infrastructure must not reference Projects. |
| `src/App/CanDoItAll.Web/Program.cs` | Readiness completes before app.Run. A narrow offline maintenance command mode can exit before hosted writers start; no new HTTP restore endpoint or new executable project is required. |
| `.../Projects/Persistence/ProjectsDbContext.cs` and canonical AppDbContext | Add the same Projects-owned mappings to both models through existing configuration discovery and the canonical migration lineage. |
| `.../Projects/DatabaseTransfer/ProjectsProfileTransferStore.cs` | Explicit transfer DTOs already exclude live definition revisions and allocate fresh target roots. Keep operation state/receipts absent from DTOs. Do not delete target operation evidence when clearing business rows. |
| `.../Projects/ProjectTransferTargetStateParticipant.cs` | Existing target inspection and table exclusion seam. Include receipt evidence in target residue decisions, without treating an empty ready epoch as business residue. |
| `.../Workbench/DatabaseTransfer/ProjectTransferTargetStateGuard.cs` | Reuse existing locked import inspection/table locks; do not create a second import coordinator. |

## Small concrete responsibilities

Only the validated `DatasetNamespaceId` value belongs in SharedKernel. No Projects
caller, epoch, receipt or result type moves there.

Infrastructure owns a small `DatasetLifecycleDbContext` and SQL configuration, a
`DatasetLifecycleStore` for activation/current-state checks, and a
`DatasetActivationService` for durable activation-file plans. Composition orchestrates
the file/SQL maintenance state machine. It reuses
profile factories/options and coordinated enlistment. This separate context avoids
using the canonical all-module context for every ordinary save. A captured immutable
validated activation binding is an Infrastructure value; it is not cached permission
to ignore the current SQL namespace.

The concrete store exposes `StageActivationAsync(plan)` using the caller's existing
coordinated transaction, and shared/exclusive guard overloads accepting the actual
owner DbContext. No method commits or opens a file lease inside the ordinary owner
transaction. `DatasetActivationService.Acquire(profileId)` provides exact Prepared/
Active plan persistence through the existing private-file writer. Attach preserves
SQL namespace and maintenance identity; Reactivate requires an explicit previous SQL
binding. The convenience `ReadActiveBindingAsync` uses a bounded 64-profile immutable
binding cache in the singleton service, populated only after supported Active reads;
Prepare/MarkActive invalidate that profile. Hot requests avoid filesystem coordination
and still verify the exact SQL binding. External offline activation requires restart.

Projects owns these cohesive files/responsibilities:

- Contracts: operation address/epoch, expected caller binding, request interpretation
  version, metadata, closed command-kind enum, caller scope,
  typed accepted/rejected/unconfirmed results and explicit lookup states. Full and
  scalar accepted payloads remain distinct. Keep existing P02a expectation unchanged.
- `ProjectDefinitionOperationCodec`: explicit versioned canonical UTF-8 writing,
  digest, accepted bytes and size accounting. No EF, clock, logging or authorization.
- `ProjectDefinitionOperationStore`: receipt queries/inserts using an existing
  ProjectsDbContext, digest equality and codec validation. It does not start owner
  effects or acquire execution authority.
- `ProjectOperationEpochService`: metadata/readiness, guarded epoch admission and
  bounded rotation/retirement/purge. The owner-specific options and entity mappings
  stay beside it; no universal epoch service.
- Existing `ProjectDefinitionWriter`: orchestration and effect classification. Its
  public protected overloads take the request and explicit trusted invocation.
- A small `ProjectDefinitionReceiptAccess` interface is justified at the real Web /
  native / execution boundary. It checks current permission for a historical target;
  it contains no persistence or serializer mechanics. Do not create interfaces for
  each single-implementation codec/store.

Proposed invocation is application-only: trusted caller scope, the current receipt
read checker, and the existing optional ProjectMutationAuthorization plus typed
effect-relevant source/basis bindings. No such invocation can be populated from JSON.
Execution adapters must supply real admitted bindings; an arbitrary extra string
dictionary or hashing a source-authority CLR object is forbidden.

The protected owner API is concrete: `CreateAsync(ProjectDefinitionRequest<CreateProjectDefinitionCommand>,
ProjectDefinitionInvocation, CancellationToken)`, equivalent ReplaceAsync and
UpdateScalarsAsync overloads, `GetWriteContextAsync` and `LookupAsync(operation,
invocation, cancellationToken)`. Create/full results carry ProjectEditorAcknowledgement;
scalar results carry ProjectDefinitionScalarsAcknowledgement. The Projects-owned
result family can be generic over those two acknowledgement types; it is not a
SharedKernel mutation framework. Lookup uses closed full/scalar found variants plus
NotObserved(Open/Draining), ClosedAbsent, Expired and Unavailable, rather than nullable
child collections. Reserved
creation remains an internal overload with the existing creation context.

## Frozen caller and interpretation preconditions

Operation includes required `ProjectCallerBinding`: `pc1:` plus a 64-character lower
hex SHA-256 over the versioned/domain-separated encoding of namespace, independently
derived caller kind and stable caller key. The server publishes it in write-context
metadata. It is not a principal or permission. Compare the current namespace first,
then the retained binding before **any** receipt lookup or new effect. A caller change
returns caller-context-changed; do not look in the new partition and execute on absence.
Credential rotation for the same issuer/subject preserves binding, while auth-mode,
subject, surface and dataset changes do not. Lookup requires the same binding in its
bounded `callerBinding` query parameter. A forged binding cannot select authority.
Binding interpretation is versioned too: retain `pc1` derivation while any issued
operation needs it; publishing a later binding version cannot invalidate a matching
old scope during its horizon. Derive the submitted supported version from the current
trusted scope and compare; never treat a submitted hash as the scope itself.

The request envelope also requires ProjectDefinitionRequestVersion(schema version,
normalization/digest version), initially `(1, 1)`. Persist it in pending records and
receipts and select that normalizer before deriving the digest. Keep supported request
interpretations and result decoders through all issued horizons; unsupported versions
fail without effects. Do not silently use new normalization on an old pending command.
The digest covers versions and caller binding alongside the existing semantic address.
Equivalent normalized whitespace/date values may share a digest; frozen original
typed values remain separate for merge. Ordered blank slots are still significant.

Bounds apply before command-copy allocation. Web reads at most the configured body
budget, scans JSON tokens/array counts/field byte limits before constructing owner
command arrays, then uses a bounded command factory. Native/direct adapters call that
same bounds-first factory on their existing lists before any ToArray/normalization
copy. The already-copied immutable P02a command is not evidence of this protection.
Use a bounded UTF-8 reader/pass, not an unbounded JsonDocument/object graph followed by
counting. Unknown versions/depth and oversize input fail before owner transaction.

## Minimal durable schema

| Owner / table | Columns and invariants |
| --- | --- |
| Infrastructure dataset lifecycle singleton | Fixed singleton key; nonempty namespace; schema version; activation maintenance ID and state. SQL activation is explicit. Normal mutations read-lock the row and never update access time or counters. |
| Projects operation state | Namespace primary key, positive CurrentEpoch, nonnegative RetiredThroughEpoch strictly below CurrentEpoch. Retain for the namespace lifetime. A reactivation creates fresh namespace state rather than resetting old epoch numbers in place. |
| Projects operation epochs | `(Namespace, Epoch)` key; Open/Closed; admission deadline, closed timestamp; persisted mutation deadline and replay-retention parameters. Check valid states/times and positive finite durations. One open current epoch. |
| Projects definition receipts | Unique `(Namespace, Epoch, CallerKind, CallerKey, OperationId)`; command/codec versions; 32-byte semantic digest; target ID/lifetime; accepted revision/time; immutable accepted UTF-8 `bytea`. No FK/cascade from project deletion or the shared epoch row. Existence/admission is enforced under the epoch guard; cleanup deletes epoch metadata only after receipts are empty. |

Use a closed caller enum and fixed 32-byte scope key: domain-separated hash for the
validated issuer/subject pair, fixed defined keys for OpenApi and LocalWorkspace.
Use strongly typed values above persistence; avoid a concatenated user-provided key.
The receipt primary-key prefix supports namespace/epoch bounded cleanup; add indexes
only when an actual query plan requires them. There is no target-history browsing
endpoint and therefore no speculative content/history index.
An FK from every receipt to one shared epoch row would reacquire key-share tuple locks
and recreate MultiXact pressure even in advisory strategy B. Omit that redundant hot
reference; the guarded owner insertion/cleanup protocol and its invariant tests own
epoch membership. This follows the existing owner-local Process authoring receipt
precedent rather than creating a new database-global relationship. Never omit the
receipt uniqueness or value/length checks.

The pilot retains accepted receipts only. A well-formed address is permanently bound
to its digest when its accepted receipt commits. A rejected pre-admission request or
rolled-back transaction has no durable receipt binding; clients must still use a new
identity for changed intent. Same-address/different-body refusal is guaranteed when
committed evidence exists. Claiming permanent binding for every malformed, denied or
failed attempt would require a separate durable rejection/admission journal and is
outside this minimal protocol. Exact retry may execute an operation that previously
had no effect while its original epoch and preconditions remain admissible.

## Execution, replay and lock order

1. The adapter resolves the configured caller mode, current write permission and
   runtime activation binding outside the owner transaction. Compare namespace and
   expected caller binding, select supported request interpretation, enforce bounds
   before allocation, then freeze/normalize input and calculate its semantic digest.
   Create IDs allocated by the server are absent from the request digest; supplied
   reserved target/parent identities are included. New child IDs remain null in it.
2. Attempt an independent fresh receipt lookup with namespace/epoch validation.
   Matching evidence plus current historical read permission returns the old result
   before stale target or expired effect authority can reject it. A missing receipt
   is only NotObserved. This read does not hold an execution or project mutation lease.
   Current-profile comparison in the existing Prepare method is also an effect
   precondition: validate its structure before lookup, but defer selector equality
   until after replay. Otherwise an unchanged dataset reached through a new profile
   alias cannot recover the original receipt even though the namespace still matches.
3. For a possible new effect, acquire the existing source authority lease first.
   Open the definition transaction with the P02a isolation decision unchanged:
   source-null ReadCommitted; source-authorized Serializable pending its separate
   audit. Immediately install transaction-local bounded database timeouts.
   Before any shared lifecycle guard, read the requested epoch's immutable deadline
   and PostgreSQL clock; reject expired/unavailable new-effect admission without
   acquiring a shared guard. This is rejection-only, never authority or absence proof.
4. Under baseline guard A, take the dataset lifecycle SQL row `FOR SHARE`, compare captured/submitted/current
   namespace and active state, then read the requested Projects epoch row `FOR SHARE`.
   Acquire the complete sorted
   existing project/parent/hierarchy/external-identity **and operation** advisory key
   set. Source project keys remain included exactly as today. No late owner key.
5. Read the receipt again under the operation lock. Compare digest and return its
   historical payload if currently readable. Then obtain PostgreSQL `clock_timestamp()`
   after waits and reject a missing retired, closed, future or deadline-expired
   operation. Only now perform current target lifetime/revision/child checks and
   original root CAS. Defer the definition branch's current automatic target
   admission check until after this receipt check; other BeginAsync callers retain
   their existing order. Final source and target checks before commit remain required.
6. Stage normalized root/children and exact accepted mappings, SaveChanges, construct
   the accepted payload using the stamped root revision, and enforce its byte bound.
   Under `using (transactions.Enter(context))`, call search.UpsertForMutationAsync.
   Insert the receipt into that same Projects context and SaveChanges again. The
   root is Unchanged after its first successful SaveChanges, so the receipt-only
   second flush must not advance its revision. Commit once through the existing scope.
7. On confirmed commit, return the stored accepted result. Source/context/lease
   disposal, activity or response serialization failures cannot relabel it rejected.
   Replays do not repeat optional activity or any creation postprocessing.

Dataset and epoch maintenance never acquires execution/source/project keys. Under A it takes
dataset guard before epoch guard; epoch rotation takes the expired current epoch
`FOR UPDATE SKIP LOCKED`. A held shared epoch makes this attempt skip and retry on a
later bounded maintenance tick; closure is never published before holders finish. Routine writers
therefore overlap instead of updating a shared hot counter. Do not use FOR KEY SHARE:
closure changes non-key fields. Do not merely read state after an advisory wait:
the retained Serializable source branch could see its old snapshot. Locking reads
must fail with serialization failure on changed rows, followed by fresh reconciliation,
never automatic business-command rerun. PostgreSQL documents these locking-snapshot
rules in [transaction isolation](https://www.postgresql.org/docs/18/transaction-iso.html).
Guard A is selected by the [completed comparison](../validation/projects-guard-diagnostic.md).
Both safe variants passed; A was faster in the measured sequential-statement shape,
while its additional MultiXact/WAL work remains an explicit production-load gate.
No strategy toggle is shipped.

Set the finite whole-transaction deadline through transaction-local PostgreSQL 18
`transaction_timeout`, with bounded statement/lock waits and a linked caller deadline.
Do not rely on cancellation or per-command timeout alone to drain a paused connection.
The setting terminates a timed-out session; pool/recovery tests must cover it. Do not
change global server settings. See [client connection defaults](https://www.postgresql.org/docs/18/runtime-config-client.html).

The existing source Acquire/Require may reject before the in-transaction receipt
check, including while another duplicate commits. After disposal perform one bounded
fresh lookup before reporting that refusal. Receipt read authority remains required,
but expired effect authority is not a requirement for recovering a past result.
An accepted operation whose historical detail is no longer authorized returns
unavailable/denied, never Rejected-as-rollback and never newly executes.

Initially receipt detail requires the same current workspace/Projects read policy
and the exact target lifetime still readable. A deleted/replaced target returns
detail-unavailable; do not invent historical/tombstone access from ordinary read
permission. Such a boundary authorization refusal is not an owner rejection asserting
that the prior operation rolled back. Current authorization uses the verified dataset
and target lifetime, not an old profile selector inside the historical acknowledgement.

## Commit uncertainty and exact result storage

Track whether CommitAsync was invoked and whether the scope confirmed it. Known
validation/CAS/40001 failures before a successful commit prove this attempt made no
effect; another duplicate may still have committed, so reconcile the complete address
fresh first. Commit transport/cancellation exceptions are Unconfirmed unless fresh
lookup proves Accepted. Use a separate bounded recovery token, not the already-canceled
HTTP token; failure to recover does not extend the mutation deadline or loop forever.
Never swallow programmer/provider errors as validation. Keep primary and cleanup
failures actionable while avoiding owner payloads in logs.

Unconfirmed is sticky at the operation/client level: a later known-rejected attempt
and fresh NotObserved in an open/draining epoch cannot clear prior uncertainty. The
lookup contract distinguishes Found, NotObserved(Open/Draining), ClosedAbsent,
OperationExpired, DatasetChanged, CallerContextChanged and DetailUnavailable.
ClosedAbsent is issued only from one coherent MVCC lookup statement observing committed
closure after admitted writers drained and while every receipt is retained; include closure time,
observation time and retained-until bound. Only exact Found or this authoritative
absence resolves prior ambiguity. Retired or inaccessible history remains unknown;
neither absence proof nor rejection automatically starts a new intent. A first known
rejection with no previous ambiguous dispatch remains a normal attempt rejection.

Canonical bytes include original operation reference, fixed codec version, accepted
timestamp, final exact full/scalar acknowledgement and immutable result metadata.
Replayed is an outer presentation flag. Write property order, enum representation,
GUID/date formats, null representation and escaping explicitly; use golden bytes.
Bounded typed decoding validates version and required identities and fails on corrupt
retained bytes. HTTP can embed the original accepted JSON without reserializing it;
native clients use the typed immutable decode of the same payload.

The accepted timestamp is sampled inside the transaction, not claimed to be the exact
server commit instant. Publish a conservative replay guarantee based on the epoch's
admission deadline plus maximum transaction duration plus retention. Cleanup eligibility
is the later of that published bound and ClosedAtUtc + issued retention, so every last
admitted commit receives its full horizon without an updated per-save expiry counter.

Build a bounded provisional result before DML to detect obvious expansion. The final
exact byte check occurs after SaveChanges stamps the real revision and before commit;
an oversized final result rolls the whole transaction back. This avoids redesigning
ApplicationManagedConcurrencyTokens solely to serialize a provisional revision.

Creation compensation remains separate: store only primitive compensation witness
metadata if exact creation recovery needs it (reservation tuple and fingerprint), not
the ProjectMutationAuthorization object. Reconstruct effect authorization through the
current trusted adapter before compensation; possession of a receipt is not authority.
Do not serialize the opaque source lease into a public result.

## Epoch lifetime and bounded maintenance

Use the existing 07 candidates for controlled tests: 24-hour admission, seven-day
replay and 30-second mutation deadlines. They remain deployment candidates until
capacity/client recovery evidence passes. Persist issued durations on each epoch.
Full and compact retention are equal; no separate compact state or permanent
per-operation tombstone is needed in this slice.

Run one Projects-owned maintenance operation during owner readiness and from a small
hosted service every minute. Multiple hosts may call it safely under database guards.
Rotate an expired current epoch once: close after shared holders drain and publish one
successor using database time. After downtime do not create an empty epoch for every
missed day. An ordinary save never opens a new epoch. Metadata returns unavailable if
readiness cannot establish valid current state; it never silently replaces a dispatched
operation's epoch.

Check database time without a shared guard before requesting the exclusive owner
state lock; skip rotation while the current epoch is open. Retire a contiguous fully
eligible closed prefix only inside expired-current-epoch rotation, before publishing
the successor, and commit the watermark **before** deletion. An open-epoch tick only
purges already-retired evidence, without global lifecycle guards. This prevents valid
new writers starving an unrelated exclusive retirement pass. Allow up to one extra
configured admission-epoch cadence in retained-capacity/deletion-lateness estimates,
plus scheduling and bounded drain/work time; never shorten issued retention.
Purge afterwards in bounded resumable batches:
at most 128 receipts and at most 16 MiB of retained payload per batch, using ordered
indexed key candidates and octet_length metadata rather than loading payloads into
memory. Bound each purge pass to two seconds of work and resume next tick; rotation
separately permits the finite owner-transaction drain budget. Server command limits
enforce these bounds. These initial batching values limit individual
work, not WAL amplification or total backlog; measure both. Delete an eligible closed
epoch row only after its receipt batch is empty. Keep active namespace state/watermark.
The same bounded receipt pass selects one eligible `(namespace, epoch)` and then
uses its receipt-key range, rather than sorting the entire receipt table across
namespaces. Inactive epochs become eligible only after both persisted horizons pass;
they do not rely on future rotations of the inactive state. Delete empty inactive
namespace state only after all its epochs and receipts are gone. Every delete repeats
the exact active dataset binding predicate; no shared lifecycle guard is acquired.

Read/lookup uses one coherent MVCC SQL statement joining current dataset lifecycle,
owner state/watermark, requested epoch and exact caller receipt, without a shared
lifecycle guard. Copy the bounded result before current disclosure authorization;
no execution/source lock is taken. The statement snapshot retains receipt bytes
across concurrent purge. Found/Open can be an older safe observation; ClosedAbsent
requires committed Closed, unretired and fully retained state in the same snapshot.
Its retained-until timestamp is a minimum promise and can precede observation while
the contiguous watermark still preserves complete evidence; that remains valid
ClosedAbsent, rather than Expired inferred from wall-clock time alone.
Separate plain queries are insufficient. A stream of ordinary lookups must not
starve exclusive closure/retirement by taking shared row guards.

## Guard-cost audit and comparative gate before production wiring

Shared compatibility does not make the two global row guards free. Concurrent tuple
lock holders use MultiXact bookkeeping; its member/offset storage and aging can become
work even without logical row updates. This is a specific risk of concentrating every
definition save on two rows, not a measured defect in this candidate. PostgreSQL
describes that mechanism in [multixacts and vacuuming](https://www.postgresql.org/docs/18/routine-vacuuming.html#VACUUM-FOR-MULTIXACT-WRAPAROUND).
Tuple locking can dirty pages; advisory locks avoid tuple membership changes but use
the shared lock-manager pool. Thus replacing row locks can move cost into a different
shared resource, not establish unlimited scalability. See [explicit locking](https://www.postgresql.org/docs/18/explicit-locking.html).

Compare two fixed test strategies, not a production-selectable safety toggle:

| Strategy | Ordinary writer | Maintenance and stale-snapshot safety |
| --- | --- | --- |
| A: row guards | Dataset FOR SHARE, then Projects state FOR SHARE, followed by complete sorted operation/owner keys and database-time admission | Dataset/epoch exclusive row locks drain readers. Serializable locking read after a state change must abort; no old snapshot admission. |
| B: shared advisory with Serializable row validation | Two transaction-level `pg_advisory_xact_lock_shared` guards in dataset then Projects-state order; ReadCommitted reads state after acquisition without row locks. Serializable additionally performs A's FOR SHARE state validations after these guards | Every activation/closure/retirement path first takes the corresponding exclusive advisory guard, then updates/locks state rows. Serializable cannot replace its row validation with a plain read: even the SELECT acquiring an advisory lock may establish a snapshot before waiting. Retained row validation raises 40001 after a concurrent state change. |

Use fixed domain-separated keys: dataset lifecycle guard is constant per physical
database, not derived from a submitted namespace; Projects state guard is derived from
the verified namespace plus the owner discriminator. A stale writer and activation
must contend on the same lifecycle key. PostgreSQL's advisory locks are database-local.
There are no session-level locks, no pool-leaking lock/unlock pair and no key per
operation added to a global lock set. Existing operation/project keys remain separate.
For B, all code that updates lifecycle/epoch state must obey its advisory protocol;
SQL constraints alone do not enforce that convention. Mixed binaries/strategies are
unsupported until all maintenance paths are compatible; stamp the selected protocol
version in activation state and refuse unknown versions rather than configure each
host differently. Source authority and generic SerializableMutationScope do not change.

The direct-SQL prototype is allowed before production implementation and must mirror
these exact guards, row shapes and ordering. Reuse the single owned PostgreSQL18
fixture and a new lease; no extra container or global statistics reset. Record server
version, CPU/memory, max_connections, max_locks_per_transaction, pool limits and source
identity. The 128-writer cell requires 128 simultaneous physical writer connections
plus maintenance/observer headroom. If the existing fixture cannot admit that, parent
may explicitly reconfigure/restart only the owned fixture and repeat all comparative
cells identically; never label 128 queued tasks over a smaller pool as 128 writers.

Run A/B at 8, 32 and 128 writers, each with three interleaved samples, 256 total
measured operations per sample, one 64-operation total warmup per cell and a bounded
ten-minute overall deadline. Report fewer operations/timeouts rather than replacing
failed samples with easier ones. Each writer uses an independent connection; use a
common start barrier, disjoint synthetic roots and the same short owner-like work
(conditional root update plus fixed bounded receipt insert). A separate guard-only
cell isolates acquisition overhead. No model/network wait or artificial sleep belongs
inside the timed transaction. Alternate A/B order to reduce cache/order bias; retain
all sample results, not just the best. This small sample identifies regressions; it
does not establish production p99 or a supported sustained request rate.

Measure accepted/rejected/timed-out attempts separately; guard acquisition latency,
whole transaction p50/p95/p99, accepted throughput, query count and client pool wait.
Record before/after `pg_stat_slru` MultiXactMember/Offset counters, `pg_stat_wal` bytes,
table/index size and database transaction/deadlock counters without resetting shared
state. Sample observed `pg_stat_activity` wait types/events (LockManager, MultiXact*,
tuple/transactionid/advisory waits as present) and `pg_locks` from one bounded observer.
Stats may be delayed/cluster-wide; label them observed deltas and control unrelated
fixture activity. Observer cost must be identical between variants. Definitions come
from [PostgreSQL monitoring statistics](https://www.postgresql.org/docs/18/monitoring-stats.html).

For each strategy and concurrency, a distinct deterministic closure drill holds all
writer guards using barriers, queues maintenance, proves it has not closed while any
admitted writer remains, releases a subset, then all. Report maintenance request-to-close
and last-holder-release-to-close separately so intentional barrier time is not mistaken
for lock overhead. Once the immutable database admission deadline expires, keep a
bounded continuous stream of expired attempts arriving while closure waits. Prove
every newcomer rejects at the precheck without acquiring any shared lifecycle guard;
after the last old holder releases, closure completes despite that stream. Do not
assume a queued exclusive row lock prevents new FOR SHARE holders: the diagnostic
observed bypass. The transaction timeout installed before precheck bounds all
predeadline transactions, including those paused before taking a guard. Repeat with one paused
writer terminated by its transaction deadline and with rollback/cancellation. Drain
all tasks before lease cleanup. Neither drill relies on sleeps alone.

For Serializable, establish an old snapshot before a concurrent maintenance change,
wait for the guard, and prove both A and B reject stale state through locking validation.
For ReadCommitted B, prove a waiter observes closed/new state after the advisory wait.
Repeat the equivalent dataset rotation fence. A control implementation that omits B's
Serializable row check must fail the negative test; do not ship it. Closure/retirement
cannot claim authoritative ClosedAbsent until every admitted holder has drained.

Selection is deterministic: correctness failures disqualify a variant. Retain A unless
B shows a repeatable improvement beyond normal sample variation at 32/128 writers
(use the existing 10% accepted-throughput/15% p95 investigation bands) or A exhibits
material MultiXact/WAL growth, while B meets closure/deadline correctness and shows no
material regression at 8. If evidence is noisy, repeat only the ambiguous cells before
selection; do not assume an architectural winner. Record the choice and raw evidence
before wiring production guards, then rerun the real Projects harness with retained
receipts and cleanup after implementation. Guard-only results never replace that gate.

## Dataset bootstrap, attach and managed restore

Use a private activation document under the existing control-plane database-profiles
root, with profile-routing entries bound to the SQL namespace; profile IDs and connection
fingerprints are selectors, not dataset identity. New aliases of the same database
must explicitly attach to its existing namespace. Do not hash credentials into the
namespace. Separate control-plane roots require explicit attachment and offline
maintenance coordination; the local file lock is not a distributed writer inventory.

The first upgrade/bootstrap may provision only an absent SQL marker and absent binding
through an explicit idempotent bootstrap operation after migrations. Existing active
state must match, missing host binding requires attach, and any mismatch/maintenance
state fails closed. Ordinary startup does not repair or rotate identities.

Separate control-plane roots can race first bootstrap. SQL chooses one namespace;
the loser retains its exact Prepared plan and cannot use ordinary startup, Resume or
Attach to change it. The explicit superseded-bootstrap attachment command requires
that PlanId and the expected current SQL namespace. It accepts only a Prepared
Bootstrap with absent prior SQL binding and a different supported Active SQL namespace,
then durably replaces the local plan with Prepared Attach. It preserves SQL namespace
and maintenance ID and reuses the ordinary Apply/Resume sequence. Incorrect expectations
and Prepared Reactivate remain refused. A crash during replacement leaves either the
old failed bootstrap or the exact new attachment plan, never an absent marker.

Add a narrowly parsed offline Web-binary command mode for bootstrap/attach/reactivate/
resume, selected before interactive services and hosted writers start. Composition
creates explicit-profile contexts via IProfileAppDbContextFactory; Infrastructure
performs its lifecycle SQL and Projects initializes its owner state using existing
CoordinatedDatabaseTransaction.ForProfile/CreateEnlistedAsync. No dependency from
Infrastructure to Projects is introduced. No database credentials are printed.

Reactivation requires an operator-recorded maintenance ID and explicit expected old
SQL binding after restored files are in place. Composition mints the new namespace
once and persists that exact target in the Prepared plan; callers cannot choose or
reuse a previously retired namespace. Write durable Prepared activation;
in one SQL transaction rotate namespace, call Projects
`RetireForReactivationAsync(previousBinding, newBinding)` to close its previous open
epoch at database time, and initialize new Projects epoch state;
commit SQL; durably write Active activation. Recovery resumes the exact maintenance
ID and checks matching SQL, never allocates another namespace because acknowledgement
was lost. There are no file writes inside the SQL transaction. Update both backup/restore
runbooks to invoke this step before reopening writers and preserve the known-good copy.
Stopping all hosts is an explicit supported-operation prerequisite, not something a
database/profile-local lock can prove. Restoring a matching SQL/file pair while bypassing
reactivation remains operationally unsupported and cannot be detected from that pair.

All target transfers capture the Active binding before BEGIN and use the existing
Serializable isolation. The first statements are utility `SET LOCAL lock_timeout =
'5s'` and `LOCK TABLE Infrastructure_DatasetLifecycle IN EXCLUSIVE MODE`, followed by
the complete sorted owner-table lock set; only then may the exact lifecycle `FOR UPDATE`
query establish a snapshot. Waiting on an unchanged
lifecycle row alone does not refresh a Serializable snapshot. PostgreSQL documents the
[required pre-query explicit-lock order](https://www.postgresql.org/docs/18/sql-lock.html).
`DatabaseTransferLifecycleGuard` supplies the first utility exclusion stage for generic
transfer and exclusive import before participant admission. The latter preserves its
managed-storage session gate before BEGIN, then the lifecycle barrier, ACCESS EXCLUSIVE
NOWAIT tables, binding check and transaction keys. Ordinary definition writes never take the import
session gate. Five-second target lock waits refuse contention; no live-drain guarantee
or runtime strategy toggle is introduced. Infrastructure never references Projects.

The 77-case transfer run exposed a second snapshot boundary: an unprotected shared
provider writer can commit while transfer waits for its later owner table lock. A
lifecycle row query before that wait caused the retained-secret guard to miss it and
fail with a foreign-key violation. Generic transfer now requires a frozen typed
`DatabaseTransferTargetPlan` before BEGIN, resolves it from metadata, takes its complete
SHARE ROW EXCLUSIVE set before the lifecycle read, and admits only owner contexts whose
complete model is in that set. Subsequent lock calls validate subsets without issuing
new locks; no lazy first-query or first-lock admission exists. Workspace, Providers,
History and SimpleChats are the four production entry callers. Cross-owner Security,
Agent locator and Projects reference participants declare their entity types through
their existing owner boundaries. Existing manual internal session proofs and explicit
InMemory tests retain their old primitive; relational generic transfer cannot omit a plan.

History and SimpleChats declare the existing initialization key as a session gate before
BEGIN. Sort/deduplicate gates, retain the physical connection, release attempted keys
in finally even after cancellation or BEGIN failure, and evict on unconfirmed cleanup.
After table/binding admission, acquire matching transaction keys and release session
keys before owner effects, retaining finally cleanup for incomplete handoff. An
acknowledged generic target commit carries the exact result and profile IDs through
later source/connection cleanup failure. Service and Workspace presentation retain its
accepted count with a typed cleanup warning and operator-review state. Logs retain
profile/group identities, counts and failure type without raw exception/driver payloads. A target CommitAsync exception
does not establish known commit. Five additional `DatabaseTransferCommitOutcomeTests`
cases cover source commit failure, precommit rejection, server commit with lost ack,
final connection close failure, and preflight disposal before handler dispatch,
including service warning mapping.
Keep ordinary History initialization's transaction gate and ReadCommitted re-read.
`DatabaseTransferPlanTests` adds seven discovery cases: no plan, undeclared owner, later
plan extension, BEGIN failure cleanup, acquired-prefix cancellation cleanup, and both
initializer/transfer winner orders. The provider regression now observes an actual
`pg_locks` owner-table waiter before committing its concurrent writer. Lifecycle/binding
tests continue to prove project+receipt visibility and exact captured-binding refusal.
These cases are authored requirements until the parent records build-backed discovery
and execution; no new tests were run by the implementation agent.

Exclusive import now retains narrow target commit knowledge outside scope/context
cleanup. `DatabaseImportCommitException` carries the target profile and Acknowledged or
Unconfirmed state without a generic result payload. Projects row transfer preserves its
own exact result through the existing typed committed-transfer adapter. Package import
preserves staged bytes after either acknowledged or uncertain commit; only known
precommit failure permits compensation. Its accepted counts and cleanup warning survive
storage/extraction cleanup, and uncertainty holds further native import. See the
[migration, maintenance and outcome review](../reviews/P02b-migration-maintenance-transfer-review.md)
for the concrete deleted-file defect, six authored real-PG/file cases and residual
process-crash/operator-recovery limitation. Do not expand this into an untyped payload
or generic receipt framework.
`DatabaseTransferService` scopes/disposes its CanConnect-only preflight contexts
before dispatch so their cleanup cannot erase later committed group results.

Normal empty-database/schema provisioning already calls Composition dataset readiness
after migration. Standalone transfer fixtures explicitly activate their target. The
new race proof holds an ordinary lifecycle SHARE, stages project plus receipt, observes
the target transfer blocked on lifecycle EXCLUSIVE, commits the writer, and requires
the transfer's Serializable owner to observe both rows. The full project handler must
refuse its final target inspection; retained receipt-only targets also refuse clearing,
while initialized empty epoch metadata remains usable.

Logical project import preserves the target namespace and excludes source live receipts
and epochs. Retained target receipts survive project deletion/import. Do not copy them
into the source export DTO and do not let target table clearing remove them. Existing
target-state participants must reject destructive replacement when retained operation
evidence conflicts with the target import policy; empty lifecycle readiness rows alone
must not make every otherwise empty target unusable.

## Exact caller adapters and cutover

| Caller | Owned adapter change |
| --- | --- |
| Web Projects routes | New focused Web adapter next to ProjectsApi. Disabled authorization explicitly selects OpenApi. Enabled mode uses existing WebCurrentPrincipalResolver/current section policies, hashes verified issuer + subject, never jti/Stamp or request actor. Recheck current ReadProjects before historical disclosure; retry POST also requires WriteProjects. Invalid/revoked credentials never downgrade to open mode. |
| Native Projects host | Explicit LocalWorkspace adapter registered in native/module composition. ProjectsPage gets current metadata before first dispatch, freezes address with ProjectEditorSubmission and preserves pending submission on unknown result. Do not infer native identity from null HttpContext or mutate renderer ownership. |
| Workbench HTTP update/create | Preserve lease/source admission and replace scalar/full invocation with protected envelope. HTTP identity comes from its verified Web boundary; X-Agent-Id/session/name headers are attribution, not durable authenticated scope. Return accepted scalar result rather than treating ResolveSavedProjectAsync as the acknowledgement. |
| Governed runtime/tool | Workbench adapter maps the already admitted agent/process authority, not model text. ProjectProcessMutationAdmission exposes Dispatch.Evidence.ExecutionRunId; ProjectAgentMutationAdmission carries verified AgentId/Governance. Persist the full operation address with the existing prepared invocation/checkpoint and include material basis/approval bindings in digest. Final binding must be reviewed with the execution owner before enabling this path. |
| Compatibility service methods | Keep signatures only where needed for source compatibility, but return protected-write-context-required after final cutover. Never mint a fresh operation inside SaveAsync/SaveEditorAsync. Controlled imports remain distinct maintenance operations. |
| SPA | Follow 07's explicit read-only/cutover decision. No advertisement of protected editing while its serializer drops new fields or it cannot retain an uncertain submission. |

The current in-memory ProjectEditorDraft alone does not survive host/circuit/browser
restart. Native P02b release requires restart recovery of the frozen pending submission
and exact accepted result. Add a small Projects UI pending-submission store boundary
with a narrow Web IndexedDB implementation, as specified by
[P02c](P02c-native-pending-recovery.md). Existing browserState.js localStorage
load/save/remove does not provide atomic same-operation transitions across tabs.
Do not widen BrowserWorkspaceStateStore/WorkbenchSessionSnapshot, whose schema and
profile-fingerprint binding belong to Workbench navigation. Persist the bounded
pending address **and frozen typed command** before dispatch, keyed per dataset,
expected caller binding and operation; tabs must not overwrite or clear each
other. Compare expected record revision and transition/clear within one readwrite
transaction. Persist Prepared, then Dispatching, before the owner call; Accepted is
monotonic even when an older attempt reports later. Require supported strict
durability and wait for transaction completion, not individual request success.
No localStorage/memory fallback. Storage events never auto-submit. Quota/write failure visibly prevents first
dispatch; no silent eviction of unresolved records or TTL deletion pretending they
were never sent. The client partition key is correlation, never submitted authority.

Retain the exact accepted acknowledgement before local reconciliation when storage
succeeds. If postcommit acknowledgement storage fails, keep the known accepted state
in memory and preserve the existing pending recovery reference; report the storage
failure separately, never repeat the effect. After restart resolve through authorized
receipt lookup. Pending/accepted text may contain private descriptions and notes, so
document bounded same-origin plaintext retention and explicit inspect/discard controls;
never store credentials, opaque authorization objects, whole history or live leases.
The pilot explicitly selects plaintext same-origin IndexedDB storage;
it does not provide isolation between people sharing a browser profile. Revalidate
current caller binding before displaying stored content after account change. A future
per-person browser confidentiality feature requires a separate protected-storage design.
The versioned durable record contains request schema/normalization versions, command
kind, frozen typed command, operation/caller binding, state, record revision and optional
exact accepted bytes. Never serialize FieldIdentifier/EditContext/live row references.
Clear only the exact operation and expected record revision. Raw unsubmitted invalid
date input and typing after dispatch are not restart-durable and cannot be reconstructed
from the pending command; this slice makes no broader draft-history promise.

Namespace/caller changes retain a recognizable refused pending state; they never
rekey, refresh the epoch or reissue it. Starter-object seeding is a separate effect
protocol and must not automatically rerun because the definition receipt was replayed.

## Implementation sequence and proof gates

| Card | Minimal production slice | Required decisive proof |
| --- | --- | --- |
| P01b.1 | Namespace value, lifecycle SQL/file owner, explicit bootstrap/attach/reactivate/resume and readiness composition | Same DB/profile alias/restart preserves namespace; bootstrap race generates one; every file/SQL crash boundary fails closed/resumes same ID; wrong marker cannot auto-heal; actual restored backup pair must reactivate before old-address denial is claimed. |
| P02b.1 | Contracts, canonical codec and bounds; Projects epoch/receipt mappings and canonical migration | Golden digest/bytes full/scalar/create; caller/mode/surface switch refuses before new partition lookup; credential rotation retains binding; old supported request version replays, unsupported refuses; order/blank slots/expectation/source binding differ; generated result IDs do not alter digest; pre-copy bounds; mapping parity and migration. |
| P02b.2 | Selected guard after A/B comparison, maintenance and receipt lookup | 8/32/128-writer comparison recorded; closure/drain and old-Serializable-snapshot proofs; rejection plus NotObserved leaves a previous attempt unresolved while it later commits; authoritative ClosedAbsent only after fully retained closure; watermark-before-purge and issued-horizon preservation. |
| P02b.3 | Protected writer and enlisted mandatory search | Same-address duplicate create/update across independent hosts has one effect and exact receipt; stale original retry returns old ack; changed digest rejects; search exception rolls back root/children/receipt; search and owner commands share exact connection/transaction; scalar preserves children; no-op stamps once despite receipt flush. |
| P02b.4 | HTTP/native adapters and pending-state recovery; compatibility rejection | Open/secure/native are server-selected and distinct; credential rotation keeps same subject scope; read revocation blocks historical disclosure; omitted operation rejects legacy route; response lost after real commit recovers same result after provider/host restart; pending draft edits remain; no fresh-ID retry. |
| P02b.5 | Governed adapter, maintenance/import closure and measured capacity | Expired effect source can recover authorized accepted result but cannot make new effect; wrong source/approval binding rejects; parent lease cleanup cannot hide accepted result; paused checkpoint past horizon never reexecutes; import cannot revive source receipt/claim; loaded receipt table + cleanup benchmark and restore drill pass. |

Use existing isolated PostgreSQL18 leases and standard outputs. Source anchors for
test seams: ProjectDefinitionConcurrencyTests command interception; P01a EF transaction
factory fault injection; ProjectDefinitionMigrationTests raw old-schema rows;
ProjectExternalIdentityPersistenceTests real owner transfer; DurableFileWriter's
existing internal stage observer for file crash injection. Do not add reflection,
production test-only hooks, a container per test, or broad WebApplicationFactory
fixtures where an owner context and supported interceptor prove the behavior.

The commit-loss test must commit the real transaction then throw at its acknowledgement
boundary and destroy/recreate the service provider; a fake method returning Accepted
does not prove durability. The duplicate test must control an interleaving and use
independent physical connections; Task.WhenAll alone can pass sequentially. The stale
Serializable-epoch test must establish the old snapshot before waiting on maintenance.
The purge failure test must fail after watermark commit and prove old missing address
refusal after restart. Candidate test names/discovery counts become evidence only after
actual build-backed discovery; none are claimed here.

## Rollout blockers, measured costs and rejected excess

The existing baseline includes 1,000 options, above 07's candidate MaxOptionCount 256.
That synthetic case is not retained production evidence, but proves that enabling the
candidate blindly changes supported inputs and the performance comparison. Preflight
actual retained count/text/serialized-size distributions without recording private
content; align approved limits and repeat the baseline with disclosed scenarios.

No general Projects ingress rate limit was found; ApiTransportMiddleware's 16 KiB
limit applies only to /api/access. New Projects body limits must run before model
binding, not in an endpoint filter after allocation. Finite horizon plus bounded
receipt bytes does not strictly cap total disk use without an admitted-rate/capacity
budget. Measure receipts/day, bytes/indexes/WAL, epoch closure time, purge backlog and
available headroom. Do not invent a global counter updated by every save. If measured
capacity needs rate admission, review that explicit policy separately before rollout.

Count the new guard/receipt statements and time each phase; initial reads can be
combined only after correctness tests exist. Keep disjoint accepted throughput
separate from attempts/rejections, and benchmark with retained rows and maintenance
running. Source-authorized Serializable false conflicts remain visible known
non-effects, not hidden retries. Connection pool and closure wait are distinct metrics.

The infrastructure namespace value is necessary shared data; a generic mutation
coordinator, owner history store, outbox/event bus, anonymous identity service, automatic
retry policy, permanent operation tombstones and migration of Process journals are
not justified here. Outstanding release decisions are retained-data limits/capacity,
supported client recovery horizon and durable pending storage, and the exact governed
caller/basis mapping with the execution owner. Native pending restart storage and its
limited promise are now selected above, not an option to omit from P02b. Guard mechanism
selection requires the specified prototype evidence before production wiring. These do not prevent implementing and
testing the foundation, but they prevent declaring every Projects caller protected.
