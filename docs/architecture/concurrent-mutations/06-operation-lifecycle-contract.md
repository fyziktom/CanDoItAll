# Operation caller, dataset and retention contract

Status: design recommendation for P01b/P02, inspected against current source on
2026-10-16. This is not implemented or tested evidence. It refines the ordinary
Projects definition-save protocol in [01](01-owner-transaction-design.md) and the
finite receipt decision in the [owner review](../../../codex/bundles/concurrent-mutations/reviews/owner-design-review.md).
Existing Process authoring receipts, execution journals and external effects do not
automatically acquire these guarantees.

The concrete source-anchored implementation sequence and proof obligations are in
[P01b/P02b durable results](../../../codex/bundles/concurrent-mutations/plan/P02b-durable-results.md).
That card remains design evidence until its listed implementation/validation gates pass.

## Decisions

Use three independent identities: the server-derived caller scope, the durable
dataset namespace, and an owner-local operation admission epoch. The immutable
operation address is `(dataset namespace, Projects epoch, caller scope, operation ID)`.
Neither a project revision, database profile ID, JWT ID nor host generation replaces
any of these identities. The target lifetime and original definition revision remain
part of the semantic request digest and target precondition.

Preserve the open Projects HTTP API and native Projects UI access model. Do not add an
anonymous identity service, require login merely to deduplicate, or accept actor text
from the request. Open API and native workspace access use explicit server-assigned
trust scopes. Authenticated HTTP access uses the already validated issuer and subject.
The caller scope partitions receipts; it grants no permission.

Destructive restore is a managed, offline operation. Every restored or cloned writable
dataset receives a fresh namespace after restoration and before writers start. Ordinary
restart and return to an unchanged database preserve its namespace. SQL and control-plane
activation records must agree before protected writes open. Restoring both records from
a backup cannot itself prove freshness; the mandatory post-restore activation step is
part of the supported contract.

Use finite admission epochs with durable closure and a monotonic retired watermark.
After closure, a missing receipt can never authorize a new effect. After retirement,
the watermark rejects old addresses even when every per-operation row has been deleted.
Do not delete receipt identities before this protocol and its recovery tests ship.

## Existing seams and limitations

| Evidence | Current behavior and implication |
| --- | --- |
| `src/App/CanDoItAll.Web/Api/ApiSectionAuthorization.cs`, `WithApiSection` | Adds authorization requirements only when `ApiAccessOptions.Authorization.Enabled` is true. Open API is intentional behavior. |
| `src/App/CanDoItAll.Web/Api/ProjectsApi.cs`, `MapProjectsApi`, `ListProjectsAsync`, `ListProjectAccessItemsAsync`, `GetProjectEditorAsync`, `SaveProjectAsync` | Project read routes expose the project collection/editor under the configured section scope; save delegates directly to `ProjectsService`. No per-caller Projects visibility filter is present in these inspected routes. |
| `src/App/CanDoItAll.Web/Api/ApiServiceCollectionExtensions.cs`, authentication registration; `ApiManagedTokenValidation.cs` | Enabled mode verifies JWT signature, issuer, audience and lifetime. Managed credentials additionally resolve current registry/session state. Disabled mode does not register this authentication pipeline. |
| `src/App/CanDoItAll.Web/Api/WebCurrentPrincipalResolver.cs`, `ResolveAsync`, `RequireScopeAsync` | Rechecks credential validity and exposes subject/issuer. Its `Stamp` includes expiry, session, scopes and revocation state: it is an authority observation, not a stable receipt key. It rejects anonymous API access, so calling it unconditionally would break open Projects mode. |
| `src/App/CanDoItAll.Web/Infrastructure/LocalOperatorAuthenticationStateProvider.cs` | Synthetic local identity is restricted to a trusted interactive circuit and has an explicit, limited capability list. Its circuit JWT ID changes. It does not establish universal Projects permissions. Null `HttpContext`, loopback text or a request field cannot manufacture this identity. |
| `src/Modules/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor`, `SaveCoreAsync` | Native host calls `ProjectsService.SaveEditorAsync` directly. Its existing access model is not an authenticated HTTP request. |
| `src/Modules/CanDoItAll.Modules.Processes/Services/ProcessAuthoringAdmissionPolicy.cs` | Existing native authoring already uses an explicit local caller scope, plus current profile/project-lifetime checks. This is a precedent for honest local scope, not an identity subsystem to copy. |
| `src/Processes/CanDoItAll.Processes.Persistence/EfProcessAuthoringStore.cs`, `CommitAsync`, `FindReceipt`; `ProcessAuthoringEntities.cs` | Receipts key profile/caller/operation, acquire an operation lock, compare request fingerprints and commit with owner changes. These inspected types do not provide the proposed finite epoch/restore lifecycle. Keep their migration separate. |
| `src/Foundation/CanDoItAll.Infrastructure/ControlPlane/CanonicalRuntimeDatabase.cs` | `Generation` is initialized to `0`; the canonical profile is selected once. It is not a durable destructive-restore fence. |
| `.../ControlPlane/DatabaseProfileModels.cs`, `DatabaseProfileRuntimeMetadata`, `DatabaseActiveProfileState` | Profile fingerprint and switch generation describe configuration/selection. Profile aliases, credential edits and switching away/back are different from replacing dataset history. |
| `.../ControlPlane/DatabaseProfileControlPlaneService.cs`, `SaveCoreAsync`; `ControlPlaneFileCoordination.cs`; `ControlPlanePaths.cs` | Existing durable file writes and filesystem coordination are reusable control-plane mechanics. The coordination protects callers using the same root; it is not a distributed lock over arbitrary hosts. |
| `.../ControlPlane/DatabaseTransferOperationRunner.cs`, `RunExclusiveImportAsync`, `RunTransferAsync` | Logical owner transfer uses explicit source/target contexts and owner transactions. It is not a full database restore entrypoint. |
| `docs/operations/backup-and-restore.md`; `docs/operations/installed-web-app.md` | Backup/restore includes SQL and app-data, including control-plane state. Writers must stop and restoration uses an isolated target before cutover. No application-owned full destructive restore entrypoint was found in the inspected control-plane code. |

## Minimal typed caller contract

Keep the Projects-specific caller and operation contracts in Projects.Contracts.
Proposed `ProjectMutationCallerScope` has a closed enum kind and a bounded typed key;
its validating constructors prevent unsupported kind/key combinations. Use a GUID for
operation IDs, a positive 64-bit `ProjectOperationEpoch`, and a nonempty GUID dataset namespace.
Do not add an arbitrary dictionary of claims or a generic string `actorId` to commands.

Add a required `ProjectCallerBinding` precondition to operation metadata and the frozen
operation envelope. Version 1 is `pc1:` plus 64 lowercase hexadecimal characters: the
SHA-256 of an unambiguous, domain-separated encoding of dataset namespace, server-derived
caller kind and its stable scope key. It contains no credential, issuer/subject text or
permission. The adapter derives current caller authority independently and compares the
expected binding **before any lookup or new effect**. A mismatch returns
`projects.caller-context-changed`; it must not query a new caller partition and treat
absence there as permission to execute. The binding never selects caller authority.
Credential rotation for the same verified issuer/subject preserves it; subject, mode,
surface or dataset changes do not. Lookup requires the retained binding too.
Retain supported binding-version derivation for every issued horizon; a future current
version must not invalidate an old matching caller merely because software upgraded.

| Scope kind | Server derivation | Stable key and access meaning |
| --- | --- | --- |
| `AuthenticatedSubject` | Enabled HTTP mode, after the existing verified JWT/session path; use issuer and nonempty `sub`/mapped name-identifier from that verified identity | Versioned, domain-separated SHA-256 of an unambiguous length-prefixed issuer/subject pair. Do not lowercase either value. Bound each input to 512 characters. JWT ID, token expiry and current scope stamp are excluded. The same verified subject can reconcile after credential rotation; current authority is checked again. |
| `OpenApi` | HTTP adapter selected from the configured disabled-authorization mode established by endpoint composition | One fixed typed scope for this dataset and surface. No caller key from headers, IP, user agent, display name or body. This honestly represents the existing open workspace, with no per-person isolation claim. |
| `LocalWorkspace` | Native server-side Projects adapter selected by composition, preserving the native feature's current access requirements | One fixed typed scope for this dataset/native surface. It means native workspace execution, not an authenticated person or a newly granted local-operator capability. It must not be selectable through HTTP JSON. |
| `GovernedExecution` | Dedicated trusted runtime/tool adapter from the existing durable execution identity and verified source-authority admission | Add only with the agent integration slice. A stable admitted execution/source identity partitions operations; current claim, approval and source authority stay separate required checks. Never derive it from model-supplied agent/run text. |

For the initial pilot the first three kinds suffice. The neutral runtime must not
depend on Projects to produce its identity; the owning module adapter translates its
existing typed execution identity when that slice ships. Native authenticated-person
attribution can be added where the host already supplies a verified principal, but it
is not required to replace the existing native workspace mode for this pilot. Audit
attribution and receipt partitioning must not be mislabeled as personal authentication.

The public HTTP command contains only the operation address fields and expected caller
binding the client must retain, the original edit stamp and the owner payload. Caller
scope itself is server-only; the public binding is a checked context precondition.
Prefer separate transport DTO and internal command/context types so JSON binding cannot
populate the trusted scope. A fake authenticated principal, unsupported issuer, missing
subject, expired/revoked credential or registry failure is an explicit denial in enabled
mode. Never catch that failure and select `OpenApi` or `LocalWorkspace`. In disabled
mode, select `OpenApi` explicitly; do not interpret an unverified bearer string as an
authenticated caller. Changing API authorization mode does not remap old receipts.

The two shared workspace scopes intentionally do not permit cross-surface replay. A
native operation must be reconciled through its native adapter; an open API operation
through its HTTP adapter. Enabling authentication after an open-mode operation may make
its old receipt unavailable through ordinary authenticated lookup. That is explicit
scope change, not grounds to retry with a fresh identity automatically. A future
administrative recovery operation would need its own reviewed authority contract.

### Receipt disclosure

Authenticate/resolve mode and apply current read permission before revealing receipt
content. Authenticated lookup requires current `ReadProjects`; a retrying POST still
also obeys its existing `WriteProjects` route requirement. Do not treat an old credential
stamp in a receipt as proof of today's rights. A write-only credential may perform the
existing write, but cannot recover historical result content without current read
permission. Return a typed authorization/unavailable result, never fabricated rollback.

Retain the bounded **exact owner acknowledgement**: accepted project ID and
lifetime/revision, exact normalized accepted scalar values, and exact accepted child
values/IDs paired with their frozen submitted slots, plus command kind/digest and commit
metadata. Extend the existing `ProjectEditorAcknowledgement` contract. IDs and a revision
alone cannot safely reconcile a draft: a later current read may include another writer's
changes and is never the original accepted result. An immutable accepted-result reference
is an alternative only if it resolves the exact same historical acknowledgement for its
full advertised horizon, with equivalent atomic commit and access guarantees.
The pilot stores the exact accepted payload directly as selected in 07; the reference
option is not a requirement to introduce a separate definition-history store.

Keep the result owner-specific; do not retain LLM context, unrelated decision-basis
snapshots, provider credentials or infrastructure secrets. A current read remains a
separate observation for refresh/conflict review. Apply current read permissions to the
**historical accepted payload** before disclosure. If permitted fields no longer cover
the exact acknowledgement required by the draft, return a typed unavailable/denied
acknowledgement and preserve the draft; do not silently redact it into an apparent exact
result or replace it with current content. Deletion does not implicitly grant historical
visibility: a missing target uses the owner's explicit workspace/tombstone disclosure
rule or returns unavailable. Possession of an operation ID grants no new permission.

Scalar length, child cardinality, serialized acknowledgement/reference size and total
retained-data bounds remain explicit pre-rollout gates. Measure representative retained
owner data before choosing them; do not claim a seven-day horizon alone bounds capacity
or invent request limits that silently make existing saved projects uneditable. A
rejected oversize request must fail before any business effect. Reference-backed storage
must count its immutable payload and indexes in the same retention/capacity accounting.

Open API users currently can read the project data, so a shared `OpenApi` namespace adds
no asserted private per-user compartment. Historical acknowledgement disclosure still
uses the current owner read policy; open current reads do not independently authorize
every historical field. Random GUID operation IDs are collision avoidance, not secrets.
Guessed IDs must not expose digests or content beyond that policy. If Projects gains
per-user data isolation, this shared scope must be revisited before that isolation ships; do not paper over it with an
anonymous cookie that has no actual authorization meaning.

## Durable dataset namespace and restore activation

Introduce `DatasetNamespaceId` as a small neutral value in SharedKernel, already
referenced by Projects.Contracts. Keep PostgreSQL storage, activation state and file
coordination in Infrastructure. Do not put owner epochs or Projects payloads there.
Do not make Contracts reference Infrastructure or add a product dependency to it.

Use one canonical SQL dataset-lifecycle row containing a namespace, schema version and
activation state, with an external control-plane activation record containing the same
namespace and a maintenance operation ID/state. The SQL row identifies the actual target
database. Profiles remain connection selectors; two profiles targeting the same database
must observe the same namespace, never independently generate one. An activation binding
may reference profile IDs for routing, but profile IDs/fingerprints are not the identity.
This small lifecycle responsibility belongs in its own service beside the control-plane
services; do not grow `DatabaseProfileControlPlaneService` with owner journal mechanics.

Normal startup reads and validates an existing active binding; it must not generate a
new namespace on every restart, silently adopt a conflicting SQL marker, or repair an
unknown mismatch by overwriting either side. First installation/upgrade provisions one
namespace through an explicit, idempotent bootstrap step after migrations and before
protected writers open. A new host joining the same database provisions an explicit
matching activation binding. Separate control-plane roots are not mutually coordinated
by the existing filesystem lock. Multi-host maintenance requires stopping all writers
and provisioning consistent activation state for each host; do not claim arbitrary
online distributed restore support.

Supported destructive restore/reactivation sequence:

1. Resolve and record the exact target database and filesystem roots. Stop/drain every
   application writer to it, including agent/process/workflow hosts. Preserve the known
   good source and restore into the isolated target prescribed by operator guidance.
2. Create a durable maintenance record with a new namespace and maintenance operation ID
   **after** restored control-plane files are in place. Restoration of an old namespace
   is expected; that value is never accepted as the new activation. Keep the target
   marked unavailable to protected writers throughout maintenance.
3. Apply required migrations and validate target data. In a short target database
   transaction set the new dataset namespace and initialize Projects admission state for
   that namespace. Imported old receipts/epochs may remain historical but cannot be live
   in the new namespace. Existing claim/history import restrictions still apply; do not
   turn restored runtime attempts into newly authorized execution.
4. Durably record successful SQL activation, then mark the external activation active.
   A crash between these writes leaves maintenance/mismatch and fails closed. Recovery
   resumes the same maintenance ID and verifies exact SQL state; it never generates
   another identity merely because the final acknowledgement was lost.
5. Verify namespace equality, owner readiness, runtime recovery constraints and current
   credentials before reopening writers. New reads carry the new namespace. Requests
   with the old one return dataset-changed without executing or migrating their identity.

SQL transactions cannot atomically commit a filesystem record. This explicit recoverable
state machine is required; neither `try/finally` nor a best-effort file update supplies
that atomicity. No file operation runs inside an owner mutation transaction.

The control-plane record is outside the database but is still in existing app-data
backups. It is an activation/maintenance consistency check, **not** evidence that a
matching pair of restored SQL and files is current. A raw `pg_restore`, full volume
rollback, or simultaneous restore of both records that bypasses the mandatory activation
step cannot be detected reliably from the restored state alone. State this operational
boundary plainly. Automatic detection would need an independent, non-restored authority
with a separately designed availability/coordination contract; do not invent it here.

Ordinary logical import/transfer retains the target dataset namespace and uses the
existing owner import protocol to allocate target lifetimes/revisions. It must not copy
source live receipts, epochs or claims as executable records. A whole writable clone is
a new activation, even if it has the same profile name, database name or data IDs.

For each protected mutation, compare the captured runtime namespace, submitted namespace
and active SQL namespace in the same real owner transaction. Use a shared/read guard
that maintenance alone takes exclusively; do not update this row on every save. Capture
the validated control-plane binding outside the transaction. Offline restore is the
supported maintenance path; the guard prevents stale host contexts from writing after
activation, but does not make an uncoordinated live physical restore safe.

## Projects operation epoch and finite retirement

The Projects owner owns its epoch state and receipt schema. Use one state row per active
dataset with `CurrentEpoch` and `RetiredThroughEpoch`, plus bounded epoch rows with
`Open`/`Closed` state, `AdmissionClosesAtUtc`, `ClosedAtUtc` and persisted retention
parameters. Epoch values strictly increase within the dataset; they never reopen or
wrap. Exhaustion is an explicit maintenance error, not reuse. The namespace changes
only for dataset activation, not routine receipt cleanup.

Admission metadata is published by the Projects owner. Editor reads can include it;
creation also needs a small metadata query that uses the appropriate current read/write
capability. No portfolio scan is involved. A native host/API client can obtain a current
epoch before the **first** dispatch without changing the original target edit stamp.
Once a command may have been dispatched, persist its full operation address and digest
inputs. It cannot refresh its epoch, caller, target revision or operation ID as a retry.
An undispatched local draft is different from an unconfirmed submitted command.

Native restart recovery uses a narrow, versioned IndexedDB submission store keyed by
dataset, expected caller binding, epoch and operation. Each create, dispatch claim,
result transition and conditional clear is one transaction comparing the record
revision. Persist Prepared, then Dispatching, before calling the owner; Accepted
cannot be downgraded by another tab or a late attempt. Require supported strict
durability and transaction completion; storage failure blocks first dispatch without
a localStorage or memory fallback. This retains bounded same-origin plaintext only,
not credentials or people-sharing-browser isolation. No unresolved TTL eviction or
automatic submission on storage events. The exact client protocol is in
[P02c](../../../codex/bundles/concurrent-mutations/plan/P02c-native-pending-recovery.md).

| State observed under the owner protocol | Matching committed receipt | Missing receipt |
| --- | --- | --- |
| Open and before admission deadline | Compare semantic digest, then return the authorized exact accepted acknowledgement; original target revision may now be stale | May execute with original target precondition and current authority, bounded by the mutation deadline |
| Admission deadline reached, closure still draining | Existing visible receipt may be replayed | No new effect; return expired/unconfirmed as appropriate because an earlier admitted transaction may still commit |
| Closed, within retained horizon | Replay after digest/access checks | No new effect. A fresh protocol read can resolve absence only after closure has drained admitted transactions; never turn absence into resubmission permission |
| Detail expired, compact evidence retained | Return accepted-but-detail-unavailable plus only currently authorized compact acknowledgement | No new effect |
| At/below retired watermark or wrong dataset | Return expired/dataset-changed without executing; ordinary live lookup does not search imported histories | No new effect; previous historical success may be unavailable, so do not claim rollback |
| Unknown future epoch | Reject invalid context | Reject invalid context |

Lookup has distinct typed cases: `Found`, `NotObserved` (Open or Draining),
`ClosedAbsent` (closed, fully retained epoch after admitted transactions drained),
`OperationExpired`, `DatasetChanged`, `CallerContextChanged`, and `DetailUnavailable`.
ClosedAbsent includes the operation, authoritative closure/observation timestamps and
retained-until bound. Only Found or valid ClosedAbsent can resolve prior uncertainty.
A later rejected attempt plus NotObserved cannot clear an earlier unconfirmed dispatch.
Expired/retired evidence, unavailable detail, denied access and a changed namespace
leave historical outcome unknown. Even ClosedAbsent never automatically submits a
replacement intent. Rejected is an attempt result, not permanent operation absence.

Receipt lookup uses one coherent PostgreSQL MVCC statement joining the current
dataset lifecycle state, owner state/watermark, requested epoch and exact caller's
receipt. It takes no shared lifecycle guard. Copy its bounded result before current
historical-disclosure authorization; it acquires no execution/source lease. A statement
snapshot may safely return an older Found or Open observation. ClosedAbsent requires
committed Closed state, an unretired epoch and full retained evidence in that same
snapshot. Concurrent purge cannot remove receipt bytes from that snapshot. Separate
unguarded queries do not establish this guarantee, and continuously arriving lookups
must not hold a shared row lock that can starve closure or retirement.

`ClosedAbsent.RetainedUntilUtc` is the minimum promised retention bound, not an
independent expiry verdict. It can precede the observation time when the contiguous
watermark has not yet retired that epoch. Closed plus unretired state still proves
complete receipt retention in that statement snapshot. Consumers validate closure
before observation and a positive promised horizon; they must not discard this proof
solely because observation occurred after the minimum bound.

The receipt key includes caller scope and operation ID; the versioned digest covers the
full operation address, command kind, target/lifetime, original revision and normalized
owner payload. Reusing the address with a different semantic request rejects explicitly.
There is no digest-only deduplication across independently intended operations.

The pilot persists accepted receipts. Durable digest binding therefore begins when
that receipt commits; malformed/denied requests and rolled-back attempts have no
committed identity row. Clients still allocate a new ID for changed intent. The
same-address/different-payload refusal is guaranteed against existing committed
evidence, not a promise to remember every rejected network attempt forever. This
qualification avoids introducing a separate precommitted admission/rejection journal.

Freeze request schema and normalization/digest versions alongside the original values.
The issued version selects interpretation before canonicalization; unsupported versions
fail explicitly without effects. Preserve supported request interpretation and result
decoders through the issued replay horizon. An upgrade cannot silently normalize an
old request using newer rules. Canonically equivalent whitespace/date representations
may compare equal, but original submitted values remain separately available for UI
reconciliation; order, blank slots and original expectations remain significant.

HTTP represents an operation epoch as a canonical decimal **string** from `"1"`
through `"9223372036854775807"`. JSON numbers, signs, whitespace, leading zeros,
fractions, exponents and values outside that range reject. The lookup path uses
the same spelling. Browser clients retain this string unchanged; conversion to a
JavaScript number would lose identity above `2^53 - 1`. This is a Web transport
contract only. SQL values, internal .NET contracts and version-one canonical
command, receipt and recovery encodings keep their original numeric representation.

### Atomic admission, closure and cleanup

Use a short, shared/read epoch guard for ordinary writes and an exclusive guard only for
rotation/closure/retirement. Share this guard across processes through PostgreSQL, not
only a process semaphore. Holding it is bounded by the owner mutation transaction; no
human/model/remote-provider wait occurs under it. All normal writers can hold compatible
guards concurrently. Do not turn a last-access timestamp or global epoch counter into
an updated row on every operation.

The selected initial guards are locking reads of the SQL dataset lifecycle
and requested Projects epoch rows using `FOR SHARE`; closure/activation use the
corresponding exclusive row locks. `FOR KEY SHARE` does not exclude non-key state
updates. The retained Serializable source branch must use locking reads that fail
on a row changed after its snapshot, not trust a plain stale read after a lock wait.
Concurrent shared tuple locks create measured MultiXact and WAL work. The
[8/32/128-writer diagnostic](../../../codex/bundles/concurrent-mutations/validation/projects-guard-diagnostic.md)
selected row guards after both safe variants passed their drain/snapshot checks:
the unbatched shared-advisory alternative was slower, including at eight writers. The
row strategy's additional WAL at 128 writers was about 1.1–1.2 KiB per synthetic owner effect.
This is a measured initial tradeoff, not a production throughput or sustained-disk
claim; the real retained-journal workload remains a closure gate.

The guarded state/deadline and receipt check must observe current database state. For
PostgreSQL use the authoritative database clock for admission, not inconsistent host
clocks. Snapshot/serialization failures after a lock wait require a fresh transaction;
an old Serializable snapshot cannot establish current receipt absence. Current source
authority is acquired before owner mutation keys as specified in 01. Place dataset/epoch
guards at one documented position before the complete owner/operation key set; maintenance
never takes execution/source-authority keys. Add the operation key to the same ordered
bounded lock set, and retain a unique receipt constraint as final duplicate protection.

Install finite server transaction_timeout before admission work. Before any shared
lifecycle guard, read server clock and the requested epoch's immutable deadline and
reject expired or unavailable new-effect admission. Repeat the authoritative namespace,
epoch and clock checks under the guards after waits. This first read only rejects;
it never grants effect authority or proves receipt absence. PostgreSQL FOR SHARE
newcomers can bypass a queued exclusive locker: rejection only after taking a shared
guard would permit an overlapping stream of expired readers. The precheck prevents
that stream acquiring guards; predeadline transactions that pause before acquiring
them remain bounded by the already-installed transaction timeout. Replay/lookup uses
the coherent read above and cannot extend the drain through shared read guards.

Closure cannot proceed until already admitted transactions finish. The initial owner
implementation takes the expired current epoch `FOR UPDATE SKIP LOCKED`; a busy epoch
causes a bounded no-op and a later maintenance attempt. Once acquired, closure durably
marks the old epoch closed and publishes its successor atomically. A stalled process has a finite database
command/transaction deadline. An expired admission deadline independently prevents new
work even when the cleanup worker has not run. Commit/rollback after cancellation remains
subject to the ordinary unconfirmed-result protocol; do not claim cancellation rolled
back merely because the host stopped waiting.

Run rotation/cleanup through one Projects-owned maintenance operation, invoked at owner
readiness and periodically by the host. Multiple hosts may invoke it: the database guard
and expected current epoch make it idempotent. If it cannot publish a valid epoch, new
admissions report unavailable/expired; an ordinary save never manufactures a successor
or silently replaces its submitted epoch. Scheduling and batch-size values belong in
the L3 implementation card, with a bounded catch-up path after downtime.

Before requesting its exclusive state guard, maintenance checks database time without
a shared guard and skips rotation while the current epoch is still open. Advance the
eligible retired watermark only within expired-current-epoch rotation, before
publishing the successor. Open-epoch ticks purge already-retired evidence without a
global lifecycle guard. This avoids an exclusive retirement waiter competing with
continuous valid new writers. Capacity and deletion-lateness bounds include up to
one extra configured epoch cadence, plus bounded scheduling/drain/work time; retention
remains a minimum guarantee and is never shortened by this scheduling choice.

The concrete Infrastructure seam is `DatasetLifecycleStore.StageActivationAsync`:
it enlists in the caller's existing transaction and never commits. Composition owns
the SQL lifecycle update plus Projects epoch initialization, between the durable
file Prepared and Active writes. Attach preserves the existing namespace **and**
maintenance ID; its local plan ID is separate. Reactivate requires an explicit prior
SQL binding and newly minted namespace and maintenance IDs. Missing prior SQL state
is bootstrap only when the local activation file is also absent; there is no implicit
legacy-reactivation path.

Two hosts with separate control-plane roots can prepare different bootstrap plans
before either commits SQL. SQL admits one; the losing local Prepared plan remains
closed to normal startup, Resume and ordinary Attach. Recovery is an explicit
`AttachAfterSupersededBootstrapAsync` operation naming the exact losing PlanId and
expected current SQL namespace. Only Prepared Bootstrap with no previous SQL binding
may be replaced; SQL must be supported Active with a different namespace. Replace
that exact file with a Prepared Attach plan through the durable writer, preserving
both current SQL identities, then complete/resume normally. A failed file replacement
retains the original plan. This does not replace Prepared Reactivate or auto-heal a
restore mismatch, and makes no assertion about historical business effects.

`DatasetActivationService.ReadActiveBindingAsync` caches only a successfully read
Active immutable binding, keyed by profile in a bounded 64-entry process cache. The
service is singleton; hot mutation/lookup calls do not take a filesystem lock. Explicit
Prepare/MarkActive invalidates the affected entry. Profile switches capture by the new
profile ID; SQL namespace/maintenance checks still run on every protected operation.
An external offline activation requires runtime restart. The cache grants no authority
and never adopts a mismatched SQL marker. Cold file reads reuse existing bounded
control-plane coordination, private paths and durable-file infrastructure.

Every target transfer/import captures its Active file binding before beginning the
target transaction. Immediately after Serializable BEGIN, it issues utility-only
`SET LOCAL lock_timeout = '5s'`, then `LOCK TABLE Infrastructure_DatasetLifecycle IN
EXCLUSIVE MODE`, then acquires the complete sorted target owner table set, and only
then validates the exact captured lifecycle row `FOR UPDATE`. No SELECT, DML,
`SELECT set_config` or participant enrollment query may precede completion of both
utility lock stages. Ordinary definition writers hold ROW SHARE on this
table through their lifecycle `FOR SHARE`; the transfer therefore waits before fixing
its target snapshot. Merely waiting on the unchanged lifecycle row would leave a
Serializable snapshot unable to see the writer's newly committed project and receipt.
PostgreSQL requires this ordering for a [stable snapshot after explicit locking](https://www.postgresql.org/docs/18/sql-lock.html).

This entry protocol prevents sorted transfer table locks from acquiring Search or
receipts while an ordinary writer holds Projects and needs those tables. It retains
the existing target Serializable/source RepeatableRead isolation. Exclusive import
preserves its managed-storage **session** advisory gate before BEGIN, then the utility
lifecycle barrier, sorted ACCESS EXCLUSIVE NOWAIT owner tables, exact binding check
and existing transaction advisory keys. The definition path never takes the import-only session
key. The five-second lock timeout applies to subsequent target lock waits too;
busy transfers fail explicitly, with no fairness or eventual-progress promise.
Plain coherent receipt reads remain possible under the lifecycle EXCLUSIVE table lock.

Generic transfer requires an immutable `DatabaseTransferTargetPlan` before BEGIN:
complete owner entity types plus any existing entry advisory keys. Resolve names from
the canonical model without database reads. The one sorted SHARE ROW EXCLUSIVE table
lock completes before the binding read; otherwise an unprotected owner writer that
commits during a later table wait is invisible to the frozen snapshot. This defect
was reproduced by the SharedProviderSource/Secret transfer regression. Target owner
contexts must have their entire mapped model in the admitted set; command and save
guards also enforce it. Later lock requests only verify subsets and cannot add tables.
The internal null-plan session primitive remains limited to existing explicit session
proofs and the InMemory test path, not a production generic-transfer entrypoint.

History and SimpleChats retain the existing partition-initialization key as a sorted,
deduplicated session gate before BEGIN. The same connection releases every attempted
gate in finally, including cancellation and BEGIN failure; uncertain cleanup evicts
that connection from its pool. After all target tables and the exact binding are
admitted, hand those keys to the same transaction and release the session locks before
owner effects. No new session-unlock command runs after commit. History initialization keeps its original ReadCommitted
re-read after the transaction advisory gate. No ordinary current History, SimpleChats,
SharedProvider or Workflow staging path holds lifecycle SHARE then waits this key;
Projects definition writes do not initialize History. History transfer copies directly
and SimpleChats transfer stages an outbox; neither takes dynamic history attempt keys.
Other unprotected multi-owner writers can still cause bounded maintenance refusal or
database-detected rollback; this protocol does not promise live transfer drain.

Generic transfer records its exact accepted `DatabaseTransferItemResult` only after
target CommitAsync returns. Later source read-transaction/disposal/connection failures
carry this knowledge in `DatabaseTransferCommittedException`; the service preserves
accepted count/success with a typed cleanup warning and sanitized message. The existing
Workspace adapter displays committed data plus operator review, not ordinary confirmed
completion. A throwing target commit remains unconfirmed even if the server may have
committed. This is in-process commit knowledge, not a durable transfer replay protocol.
Exclusive import similarly preserves target commit knowledge across scope/context
cleanup in `DatabaseImportCommitException`, carrying only profile and Acknowledged or
Unconfirmed state. Workbench retains its own typed result. Package compensation may
delete staged assets only before commit invocation; both acknowledged and uncertain
commits retain bytes. Accepted counts survive storage orchestration/extraction cleanup
with a typed warning, while uncertainty explicitly holds further native import. This
prevents cleanup from deleting bytes referenced by committed rows; it is not a durable
package journal or permission to garbage-collect staged assets after a process crash.

`DatabaseTransferLifecycleGuard` owns the shared target entry sequence; Infrastructure
does not depend on Projects. Normal CreateEmpty/ApplySchema provisioning calls the
Composition bootstrapper's explicit dataset readiness after migration. A migrated
standalone database without an Active binding is not a writable target. Target Projects
inspection and clearing refuse retained definition receipts, including deleted-project
receipts. Empty owner state/epoch metadata does not constitute project residue. Logical
source DTOs exclude receipts/epochs, and source export is not blocked by these receipts.

Offline reactivation closes the previous namespace's one open Projects epoch in the
same transaction as the new SQL binding and new owner epoch. It performs no receipt
scan or deletion. Later bounded cleanup may remove inactive-namespace evidence only
after both the persisted admission/deadline/replay promise and actual closure plus
retention have elapsed. Each delete checks the exact current binding and excludes its
active namespace in the same statement. Delete inactive state only after all its
epochs and receipts are gone. Old operations still return DatasetChanged; ordinary
startup cannot re-admit that namespace. Composition must mint the new namespace once
when preparing reactivation and resume the same plan; caller-chosen/reused namespace
activation is unsupported. The active namespace keeps its watermark for its lifetime.

Retention is measured from each accepted commit, not the epoch's opening time. Closed
epoch cleanup must allow the latest admitted commit its full advertised replay horizon.
A safe bounded eligibility time is `ClosedAtUtc + ReplayRetention` after closure has
drained all writers; alternatively persist an equally conservative committed upper
bound. Never shorten an already advertised receipt horizon when options change. Apply
new options to future epochs and persist their values so restart/configuration changes
cannot silently reinterpret previous contracts.

If an acknowledgement advertises a fixed replay-until time, derive its conservative
bound from the epoch admission deadline plus maximum transaction duration plus issued
retention. Purge eligibility must be no earlier than both that advertised bound and
`ClosedAtUtc + ReplayRetention`. The accepted timestamp sampled before commit is not
the exact server commit instant and must not shorten this guarantee.

Retire only a contiguous prefix of fully eligible closed epochs. In a short maintenance
transaction take the exclusive guard, verify eligibility and advance
`RetiredThroughEpoch`. Then purge those epochs' detailed and compact receipt rows in
bounded resumable batches. If deletion crashes after watermark advancement, extra rows
are harmless and are removed later. Never delete the last evidence first and advance
the watermark later. Keep the watermark/state row for the dataset lifetime. Ordinary
lookup rejects retired epochs even if a stale cache or unfinished batch retains rows.

Initial ordinary-save configuration recommendation for review is 24-hour admission
epochs, a seven-day full acknowledgement/deduplication horizon after commit, and a
30-second owner mutation deadline. These are proposed pilot values, not a measured
product promise or a claim about supported disconnected clients. Set explicit validated
options and publish effective deadlines in the metadata/acknowledgement. The release
gate must select values against supported client recovery and measured owner duration;
if 30 seconds cannot contain the bounded owner operation, fix/review the operation or
deadline before rollout. No duration can be infinite, zero or silently coerced. Initially
keep full and compact retention equal to avoid a second expiry mechanism; the table's
compact-only state is available only if a later approved retention policy needs it.

Paused execution checkpoints retain their original operation address. They do not keep
ordinary epochs alive indefinitely. After the horizon, reconcile through available
owner history or require review/new admission with current preconditions and any new
approval; never silently substitute an epoch. Existing long-lived Process/Workflow
journals are separate contracts until their owners explicitly adopt compatible horizons.
Do not apply the pilot's seven-day choice to all engine history by a shared cleanup job.

## Smallest integration sequence and ownership

| Gate | Concrete integration | Closure evidence |
| --- | --- | --- |
| L0 contracts | Add only `DatasetNamespaceId` to SharedKernel. Projects.Contracts owns typed operation ID/epoch/caller scope, address, context metadata, bounded exact owner acknowledgement and explicit expired/unconfirmed outcomes. Keep transport DTO separate from trusted context. | Validation/digest tests, serializer contract, no new dependency direction. |
| L1 host scope | Add a focused Web Projects caller adapter beside `ProjectsApi`, wired in Web API composition. Enabled branch reuses verified principal/current authorization; disabled branch explicitly selects OpenApi. Add a native Projects-host context adapter in the existing module/native composition using LocalWorkspace. | Open/secure/native mode tests, denial tests, no claimed per-human identity, no general changes to JWT or local-operator policy. |
| L2 dataset activation | Add the independent Infrastructure control-plane dataset lifecycle service and canonical SQL mapping; register through Infrastructure/composition and canonical migrations. Add an explicit bootstrap/attach/reactivate maintenance entrypoint with durable maintenance states. Extend existing restore/runbook commands to invoke it before application startup. | Restart, alias, multi-process consistency and crash-point activation tests; real restore round trip; no auto-heal mismatch. |
| L3 owner epochs | Add Projects-owned epoch entities/configuration, one directly testable epoch protocol and bounded maintenance operation. Map them in ProjectsDbContext and the canonical migration model. Introduce validated retention/deadline options and owner metadata query. | PostgreSQL admission/closure/retirement races, watermark crash recovery, no global exclusive write queue, retained-size estimate. |
| L4 definition writer | Pass trusted context to the extracted Projects definition writer; check dataset/epoch/receipt before target precondition, atomically commit owner data/projection/receipt, preserve current source authority. The facade delegates and does not grow another protocol partial. | Lost-ack same-address replay, different digest rejection, concurrent duplicate creates/updates, current permission on replay. |
| L5 clients | Native Projects host and HTTP/SPA retain the whole submission address, semantic payload and original target stamp from dispatch through reconciliation. Add exact operation lookup under current read access. Render expired and dataset-changed as explicit resolution states preserving draft. | Browser/native/API serializer and restart recovery, write-only historical-read denial, no automatic fresh-ID/epoch retry. |
| L6 execution and maintenance closure | Dedicated runtime module adapter maps governed identity and persists address in existing tool/checkpoint state. Logical import excludes live receipts; managed full restore rotates namespace; offline activation validates all writers are stopped. | Claim/approval/basis tests from 02, retained checkpoint expiry, restored history cannot execute, operator runbook smoke. |

L0/L1 can be implemented independently of the protected write cutover, but do not claim
durable replay before L2-L5 are implemented and verified. Keep an explicit feature gate
or incomplete rollout state until all enabled Projects save callers participate. L6 is
required before advertising these semantics for agent/process/workflow writes. No new
generic mutation coordinator, identity service, event bus, universal epoch subsystem or
product-module dependency in Infrastructure is justified by this pilot.

## Deterministic validation plan

The following are proposed test names, not existing discovery or passed execution.
Use independent barrier-controlled PostgreSQL connections/processes for races, a controlled
clock for pure policy tests, and real database time/deadlines for protocol tests. Do not
use sleeps as the only interleaving control or InMemory to prove SQL atomicity.

- `ProjectMutationCallerTests.OpenApiScope_IsServerSelected_AndIgnoresClientActorFields`
- `ProjectMutationCallerTests.EnabledApi_InvalidOrRevokedIdentity_NeverDowngradesToOpenScope`
- `ProjectMutationCallerTests.VerifiedSubject_CredentialRotation_PreservesReceiptScope`
- `ProjectMutationCallerTests.NativeWorkspace_RequiresNoNewAuthentication_AndIsNotHttpSelectable`
- `ProjectReceiptAccessTests.ScopeChangeOrReadRevocation_DeniesHistoricalDisclosure`
- `ProjectReceiptAccessTests.AcceptedAcknowledgement_ReplaysExactNormalizedScalarsAndChildren_AfterLaterEdit`
- `ProjectReceiptAccessTests.AcceptedAcknowledgement_ExcludesUnrelatedContextAndSecrets`
- `ProjectReceiptAccessTests.FieldPermissionReduction_DoesNotReturnRedactionAsExactAcknowledgement`
- `ProjectReceiptStorageTests.ImmutableReference_PreservesExactAcknowledgementForFullHorizon`
- `DatasetLifecycleTests.RestartAndProfileAlias_PreserveDatasetNamespace`
- `DatasetLifecycleTests.NewWritableClone_ReceivesNewNamespace`
- `DatasetLifecycleTests.CrashAtEachActivationWrite_RemainsClosed_AndResumesSameMaintenanceId`
- `DatasetLifecycleTests.SqlAndControlPlaneMismatch_NeverSilentlyAdoptsEitherSide`
- `PostgreSqlDatasetRestoreTests.ManagedMatchingBackupPair_ReactivatesBeforeStartup_AndRejectsOldCommand`
- `ProjectOperationEpochTests.ClosureWaitsForAdmittedCommit_AndRejectsMissingOldOperation`
- `ProjectOperationEpochTests.DeadlinePassedBeforeClosure_MissingReceiptRemainsUnconfirmed`
- `ProjectOperationEpochTests.SameOperationConcurrentAcrossHosts_CommitsOneEffectAndReceipt`
- `ProjectOperationEpochTests.StaleSnapshotAfterGuardWait_CannotProveReceiptAbsence`
- `ProjectOperationEpochTests.LastCommitReceivesFullHorizon_DespiteEpochOpeningTime`
- `ProjectOperationEpochTests.RetentionOptionChange_DoesNotShortenIssuedHorizon`
- `ProjectOperationEpochTests.WatermarkCommitsBeforePurge_CrashCannotResurrectOldOperation`
- `ProjectOperationEpochTests.FutureEpochAndRetiredEpoch_NeverExecute`
- `ProjectSubmissionRecoveryTests.RefreshContextBeforeFirstDispatch_PreservesOriginalTargetStamp`
- `ProjectSubmissionRecoveryTests.UnconfirmedSubmission_CannotRefreshEpochOrOperationIdentity`
- `ProjectSubmissionRecoveryTests.PausedCheckpointPastHorizon_RequiresReviewWithoutAutomaticResubmit`

Measure indexed receipt lookup, dataset/epoch guard acquisition, duplicate contention,
closure wait, bounded purge batches and table/index growth separately from owner save
latency. Show unrelated project writes overlap while guards are held. Use a workload
with accepted receipt retention and cleanup running; an empty journal benchmark cannot
establish steady-state cost. Capacity is bounded by the finite horizon and admitted
rate, not by horizon alone; retain payload limits and normal HTTP/owner admission limits.
Report queue/connection-pool wait and timeout counts as well as latency percentiles.

Before source work, discover affected tests, follow `docs/testing.md` and current CI,
serialize standard Debug/Release builds, use the isolated fixture and finish mandatory
portability-static enforcement. This document reports source inspection and design only;
none of the proposed tests, restore drills or performance measurements has been run here.
