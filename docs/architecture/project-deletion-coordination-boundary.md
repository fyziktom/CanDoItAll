# Project deletion coordination boundary

## Decision

`ProjectsService.DeleteAsync` is the only project-lifecycle deletion coordinator.
Projects owns the unit of work and the small `IProjectDeletionParticipant` boundary.
Workbench implements that boundary without introducing a Projects-to-Workbench
reference. Projects enters its own `ProjectsDbContext` transaction through
`CoordinatedDatabaseTransaction`; each participant saves through a fresh explicitly
enlisted owner context on that same connection and transaction. The public participant
contract carries project/recovery facts and no EF context. Projects then asks each
participant to finish recoverable external work after the authoritative commit.

The selected pattern is a unit-of-work participant plus the existing durable
cross-module mutation processor. Project, hierarchy, Workbench, search, and routing
rows commit together. CRM reconciliation and physical storage deletion are durable,
idempotent post-commit work. A post-commit failure is a typed partial commit; it is
never converted into apparent success or silently retried under a new identity.

## Dependency and responsibility map

| Owner | Responsibility |
|---|---|
| Projects | Acquire the sorted project, hierarchy, and participant scope union, load the project, stage every participant and Search/Storage owner command, save project/hierarchy state, commit, aggregate results, and expose typed recovery. |
| `IProjectDeletionParticipant` | Declare every serialization key required while staging, stage downstream database cleanup in the Projects-owned unit of work, and complete post-commit work by an exact durable recovery ID. |
| Agent access participant | Stage its existing revocation row in the Projects transaction; revoke workspace permissions after commit and retain pending/terminal history. Its existing AttemptCount is the claim generation; a live lease and matching generation fence status updates. |
| Workbench participant | Remove project-scoped Workbench rows, persist or amend the `DeleteProject` mutation, order dependency cleanup, detect residual state, and expose pending and terminal history. |
| Cross-module mutation processor | Claim one mutation across instances, heartbeat ownership, reconcile CRM, delete storage conservatively, checkpoint outcomes, and complete or fail the owned claim. |
| Managed-storage planner and deletion service | Validate provenance, deduplicate physical identities, recheck the final current identity and all surviving bindings under the binding gate, and return explicit deletion or retention outcomes. |
| CRM integration | Converge project assignments to the desired empty state under the exact project scope; a replay that finds zero rows is successful. |
| HTTP, Blazor, and agent adapters | Preserve exact recovery identity, surface lease availability and warnings, and map typed failures without leaking implementation exceptions. |

Projects must not reference Workbench. The participant contract is justified by the
transaction boundary and test seam; no broader deletion framework or one-method
wrapper interface is introduced.

## Authoritative deletion sequence

1. Projects collects `PreparationScopeKeys` from every ordered participant, adds
   `project:{projectId}` and `projects:hierarchy`, deduplicates and sorts the union,
   starts one serializable scope, and loads the project. Preparation and residual
   cleanup still run when the project row is already absent. Workbench declares `workbench:managed-storage-bindings`, so its deletion planning
   and binding-row removal cannot race another binding writer.
2. Projects enters its active transaction. Each participant creates an explicit
   enlisted context for its own mapped records, saves, and disposes without committing.
   Workbench removes its project objects, links, bindings, view state, projections,
   analytics, references, leases, and related project-scoped state, and records one
   durable `DeleteProject` mutation containing dependency and managed-storage evidence.
   Agent access stages its revocation. Completed/no-new-work paths still save any
   residual cleanup before returning null.
3. Search and Storage owner commands save matching search documents and routing-rule
   removal on that same transaction. Projects saves its owned phases/options/hierarchy
   and project deletion, exits the coordinated scope, and alone commits. Ordinary
   factories remain independent; only explicit coordinated methods enlist.
4. Immediately after commit, Projects disposes the preparation mutation scope before
   recording activity or invoking participant completion. Commit releases the
   transaction-scoped database advisory locks; disposal releases the matching
   in-process semaphores. A participant can then reacquire its project or
   managed-binding keys without waiting on its caller.
5. Only after the scope is released, each prepared participant completes its exact
   recovery. The Workbench processor first completes outstanding durable dependencies,
   then performs CRM and managed-storage cleanup.
6. Projects returns `ProjectDeletionResult`. Retention outcomes are warnings, not
   deletion claims. Any still-incomplete participant produces
   `ProjectDeletionPartialCommitException` with the exact participant and recovery ID.

Before commit, any validation, provenance, catalog, or database failure rolls the
whole unit of work back. After commit, the project remains deleted and only the exact
durable cleanup is retryable. Precommit filesystem containment and reparse inspection
remain side-effect-free safety validation. Physical deletion is postcommit, under a
separate Serializable managed-binding scope deliberately held across the driver call.
The complete AppDbContext remains the migration and unfinished transfer authority;
runtime owner contexts do not initialize schemas. InMemory tests do not establish
transactional atomicity.

## Public recovery contract

### Project lifecycle API

| Method and route | Contract |
|---|---|
| `DELETE /api/projects/{projectId}` | `200 ProjectDeletionResult`; `409 projects.delete-cleanup-pending` after a database commit with incomplete participant cleanup. |
| `GET /api/projects/deletion-cleanups` | Lists non-terminal cleanup identities, status, `CanRetryNow`, `RetryAvailableAtUtc`, and retry guidance. |
| `GET /api/projects/deletion-completion-notices` | Lists durable terminal project-deletion notices, including clean completion and retained-object warnings. |
| `POST /api/projects/{projectId}/deletion-cleanups/{participantId}/{recoveryId}/retry` | Retries only that participant and recovery. Returns `200 ProjectDeletionResult`, `404 projects.delete-cleanup-not-found`, `400 projects.delete-cleanup-participant-invalid`, or `409 projects.delete-cleanup-pending`. |

A caller must not repeat `DELETE` as recovery. It must retain the participant and
recovery ID from the 409 response or enumerate pending cleanups, then call the exact
retry route. A second exact retry after completion is an idempotent read of terminal
history and returns the same clean or warning result without repeating external work.

### Project Structure node API

| Method and route | Contract |
|---|---|
| `POST /api/project-structure/projects/{projectId}/nodes/{nodeId}/delete` | Starts a node deletion with an explicit `managedStorageDisposition` and the `expectedProjectAdmission` returned by Structure read. Missing admission requires a refresh (409); the owner rejects stale profile/project lifetimes. A body containing the exact `durableMutationId` retries that same deletion and must match its persisted disposition; it uses the original cleanup receipt rather than new deletion admission. |
| `POST /api/project-structure/projects/{projectId}/nodes/delete` | Deletes independent requested branches with one explicit `managedStorageDisposition` and the displayed `expectedProjectAdmission`, and reports every exact recovery when only part of the batch finishes. Missing or stale admission cannot delete nodes in a recreated project. |
| `GET /api/project-structure/projects/{projectId}/deletion-cleanups` | Lists non-terminal node cleanup recoveries with lease-aware retry availability. |
| `GET /api/project-structure/projects/{projectId}/deletion-completion-notices` | Lists retained-warning completion evidence for node cleanup. |

Node partial commit is the 409 agent envelope
`ProjectStructureDeletionPartialCommit`; batch partial commit is
`ProjectStructureDeletionBatchPartialCommit`. An unknown exact node recovery is the
404 envelope `ProjectStructureDeletionRecoveryNotFound`.

## Durable identity, dependencies, and terminal history

The Workbench mutation ID is the recovery identity. The requested identity is never
replaced merely because a retry occurs. `Pending`, `WorkbenchCommitted`, `Failed`, and
`Processing` records remain visible through the appropriate pending status.
`Completed` records are retained as terminal audit and replay evidence; they are
excluded from pending lists.

`OutstandingMutationIds` are processed before the parent project mutation. A missing
or incomplete dependency keeps the parent pending and produces the same typed partial
commit. Cleanup does not infer success from absent source rows.

If residual project rows are discovered during participant completion and the current
recovery can no longer be amended safely, the participant stages a durable follow-up
mutation. Completion returns that effective recovery ID, and both the original and
follow-up histories remain queryable. This is the only case where the effective
identity can differ from the requested identity; it is explicit in the completion
result rather than hidden behind a retry. Residual staging holds the sorted union
of project and managed-binding keys before planning or removing new bindings. Durable
workflow receipts/admissions are retained across ordinary project/node deletion;
explicit profile transfer and purge must include their replay evidence separately.

Project-deletion terminal notices include both clean and warning completion. Node
completion notices are retained when warnings require user action; clean node
completion is intentionally not emitted as notification noise. Retention warnings
include the participant, effective recovery, provider, storage and locator identity,
reason, message, and remediation.

## Cross-instance claim and lease protocol

The Workbench processor uses its opaque claim token. Agent access uses its existing
AttemptCount as a monotonic durable generation: claiming compares and increments the
observed value; heartbeat, completion and failure require the same generation,
Processing status and a live lease. Each Agent transition first locks its exact row
in a short owner transaction. Its conditional UPDATE samples PostgreSQL
clock_timestamp() after that lock wait, so delayed commands cannot renew or finalize
an already expired lease. No workspace work runs in this transaction. Heartbeat updates
UpdatedAtUtc while preserving LastAttemptAtUtc as the attempt start. Fresh Processing
is unavailable for retry; stale work can be reclaimed under the same recovery ID with
a new generation. A failed claim never reports active work as successful completion
or marks another owner's attempt Failed. Database failure persistence is bounded.

Agent workspace revocation remains an idempotent cross-process locked catalog removal.
Ownership loss cancels processing and fences database finalization, but does not promise
external exactly-once effects. Project admission and retired-ID lifetime rules remain
required. Mixed execution with old generation-unaware binaries cannot provide the new
claim guarantee; participating workers must be upgraded together.

The processor conditionally claims one relational mutation. A successful claim sets
`Processing`, increments the attempt count, records an opaque owner token, and records
the last-attempt time. Checkpoint, completion, failure, and heartbeat updates are
conditional on the same token. A worker that loses ownership cannot finalize another
worker's claim.

The heartbeat renews the last-attempt time more frequently than the configured lease
duration. `Pending` and `Failed` work can be claimed immediately. A fresh `Processing`
claim exposes `CanRetryNow = false` and its `RetryAvailableAtUtc`; a retry cannot steal
it. Once the lease is stale, another process may reclaim the exact mutation. Database
transactions and PostgreSQL advisory locks release on commit, rollback, or connection
loss; mutation ownership still requires the durable token and lease checks.

Concurrent callers of the same participant and recovery converge on one terminal
mutation. They may observe the active lease or the same completed notice, but must not
execute the external mutation twice.

## CRM idempotency boundary

Project assignment deletion is part of durable Workbench completion, not the Projects
database transaction. This operation is naturally idempotent: the CRM bridge acquires
the exact `project:{projectId}` scope, removes every assignment matching that project,
and commits. Finding zero rows is successful because the desired state is precisely
that no assignment remains for a deleted project.

If CRM commits and later storage cleanup fails, the Workbench mutation remains failed
under the same recovery ID. Exact retry runs the same project predicate, accepts the
zero-row result, and continues storage cleanup. No deletion receipt or result
fingerprint is needed because no caller consumes a deleted-row count. A receipt that
short-circuited future deletion would be harmful: it could preserve an invalid late
assignment instead of reconverging to the required empty state. Assignment moves are
different because their replay must preserve source identity; that separate operation
may require a receipt, but it does not justify one for deletion.

## Managed-storage provenance and final identity

New managed project assets carry versioned creation provenance: ownership kind
`project-asset`, asset ID, requested path, storage/provider/locator facts, and a
physical fingerprint. The deletion planner classifies each candidate by a typed basis:

- `CreationProvenanceV2` for valid current owned provenance;
- `AuthoritativeBootstrapNamespace` for the controlled bootstrap namespace;
- `ImmutableContentAddress` for providers such as IPFS that cannot delete content;
- `UnverifiedLegacyPayload` when ownership cannot be proved.

Malformed or mismatched mutable provenance fails before the project commit. There is
no fallback that silently broadens ownership.

Immediately before a mutable object is deleted, the deletion service acquires the
global managed-storage binding gate, reloads the current catalog and provenance,
recomputes the physical identity, and scans surviving bindings. File-system identity
uses the canonical final path plus file volume and index when available; FTP identity
uses canonical authority and path; immutable content uses its content address. Any
surviving reference preserves the object. Duplicate deleted bindings produce one
driver delete. Storage owns catalog reads and the retained driver record; Workbench
receives typed storage/bootstrap facts through a required bounded validation callback.
The callback sees no raw configuration or EF record. Planning facts preserve complete
catalog/bootstrap selection and parse FTP addressing only for references in the
reached phase. Unrelated malformed configuration remains unparsed. Under the final
postcommit binding gate, binding IDs are collected before the ordinary owner fact
query. Those independent reads do not form a new atomic catalog snapshot and the
binding gate does not serialize independent catalog edits.

The only terminal non-delete outcomes are explicit:

- `RetainedByProvider` for immutable providers;
- `RetainedWithoutOwnershipProof` when safe ownership cannot be established.

Both persist in the mutation payload and reappear as durable warnings. They are never
reported as deleted and are not retried forever.

## Serialization gates and availability trade-off

`SerializableMutationScope` sorts and deduplicates every scope key. PostgreSQL uses
transaction-scoped advisory locks based on the full key; the in-process provider uses
deterministically ordered lock stripes. Reverse-order multi-key callers therefore
acquire an identical order and do not deadlock.

Every managed-storage binding write and every final mutable-storage delete includes
`workbench:managed-storage-bindings`. This deliberately serializes binding changes
across projects so a last-reference decision cannot race a new reference. Operations
that use unrelated project keys and do not touch managed bindings remain independent.
The Workbench deletion participant declares this key to Projects, and Projects holds
it from before participant planning through the authoritative database commit. The
scope is released immediately after that commit. The post-commit deletion service then
reacquires the key before the final identity and surviving-binding check. Retaining the
outer in-process semaphore while invoking completion would self-deadlock even though
the transaction-scoped PostgreSQL advisory lock had already ended.

The gate trades availability for correctness: a long-running holder or unavailable
database delays all managed-binding mutations, even for different projects. Callers
must honor cancellation and bounded command timeouts, surface the conflict, and retry
the original operation. They must never bypass the gate or downgrade to an in-memory
check. Transaction completion or connection loss releases the PostgreSQL lock.

## Deleted-project mutation guard

Workbench mutation scopes parse every `project:{id}` key and verify the project exists
after acquiring the serialized scope. A post-deletion write fails with 404
`ProjectNotFound`. This prevents a stale agent, UI, or retry from resurrecting
project-structure rows after Projects has committed deletion, for callers using
that established guard. This is not a complete admission protocol for every owner:
all nonempty project-attributed writes need final enlisted liveness/admission checks,
and historical/global/orphan reads and residual cleanup must remain supported.
Reserved-ID creation currently checks live Projects rows only. Completed revocations
remain unique by ProjectId, so safe retired-ID reuse requires an explicit durable
identity/tombstone or incarnation and restore policy; this cutover does not invent one.

## Project-package import boundary

The twelve-owner target-transfer contract still exposes the complete maintenance
context. Its owner-mediated cutover is separate unfinished work; the runtime deletion
cutover must not weaken its locked residue scan, payload compatibility, or cleanup.

Project packages use format `candoitall.projects.v2`. Import is intentionally
empty-target-only: the target profile must be inactive and must contain no project or
project-attributed residue. Import never performs destructive replacement. Version 1
packages are rejected with an actionable unsupported-format error because they do not
carry the integrity and storage-identity evidence required by this boundary.

Target emptiness is a composed, typed contract. Exactly one
`IProjectTransferTargetStateParticipant` must be registered for each of Infrastructure,
AgentFramework, Collaboration, CrmHr, Processes, Projects, Prompts, Resources,
SchedulerPlanner, TestLab, Workbench, and Workspace. Missing or duplicate participants
fail composition. Each participant owns its typed predicates and declares the mapped
entity types that can create project residue. The guard resolves table and schema names
from EF metadata, takes one ordered PostgreSQL `ACCESS EXCLUSIVE` lock statement for
all declared tables, and repeats every residue query inside the final transaction.
This closes the check/write race without central foreign-property reflection or a
cross-module string table catalog.

Scheduler plans and runs are a deliberate conservative exception. Their input can
carry arbitrary project or node identity and a schedule can dispatch while import is
running, so any scheduler row makes the target operationally non-quiescent. This
trades target reuse availability for deterministic import safety. Repo-branch-only
Workbench leases and analytics and unattributed terminal process history do not block,
although their tables remain locked to prevent a concurrent transition into
project-attributed state.

Export holds the serializable global managed-storage binding scope through both the
database snapshot and every physical storage read. The v2 manifest records table and
storage payload lengths and SHA-256 hashes plus source storage identity. Import
validates the complete archive and project graph before writing the target. Storage
payloads are copied to package-isolated target-owned locations, bindings are restamped
with target storage identity, and immutable content-addressed objects are accepted only
after an exact read proves the expected bytes.

Physical storage is staged before the final database commit because storage drivers
cannot enlist in the database transaction. The import therefore uses copy-on-write
placement and bounded delete compensation for every newly created object when
validation, final emptiness recheck, or commit fails. If compensation cannot prove
cleanup, import returns a typed cleanup-incomplete failure rather than claiming a clean
rollback. Recovery identities expose storage id, provider kind, locator kind, and a
stable SHA-256 locator fingerprint; raw locators and credentials are never surfaced.
The global managed-storage binding gate remains held across staging, the final
transaction, and compensation.

## Failure and observability policy

- Validation and provenance failures before commit retain the original typed error and
  roll back all database changes.
- Post-commit failures retain the durable mutation, actionable error state, exact
  recovery identity, and safe retry guidance.
- Logs identify project, participant, mutation, attempt, status, and failure type while
  excluding secrets, claim tokens, and raw credentials.
- Search and storage-routing rows associated through `ProjectId` are part of the
  authoritative transaction, not best-effort cleanup.
- Activity recording remains best-effort telemetry and cannot alter the deletion
  result.

## Testability contract

Focused tests must prove:

- actual Projects deletion saves Agent access, Workbench, Search, and Storage through
  the same raw connection/Serializable transaction; a final Projects save failure
  rolls back all already-successful owner saves;
- completed/no-new-work cleanup persists residual views after the project is missing,
  and follow-up residual binding removal waits behind the managed-binding gate;
- Agent legacy mapping, pending/terminal fields and recovery IDs survive restart and
  remain isolated by canonical profile;
- clean deletion removes project, hierarchy, Workbench, search, routing, CRM, and
  physical storage state while retaining a clean terminal mutation and notice;
- a driver failure commits database deletion, then exact participant and recovery
  retry completes once and replays terminal success without another driver call;
- a storage failure after CRM commit leaves assignments absent, and exact retry
  accepts the CRM zero-row replay before finishing storage cleanup;
- retained immutable media persists the exact warning across retry and a fresh service
  scope;
- missing dependencies remain pending, and residual rows create an explicit effective
  follow-up recovery;
- fresh claims cannot be stolen, stale claims can be reclaimed, and concurrent exact
  callers converge on one completion;
- a deleted project rejects new Workbench mutations with `ProjectNotFound`;
- PostgreSQL two-context tests prove same-key and global-gate blocking, different-key
  independence, lock release, and reverse-order multi-key completion without deadlock;
- an actual `ProjectsService.DeleteAsync` PostgreSQL test observes the ungranted
  participant-declared binding-gate waiter, proves project/object/binding rows and
  physical bytes remain with zero driver calls before release, then proves one
  terminal external delete after release and proves post-commit completion does not
  self-deadlock while reacquiring the same in-process gate;
- HTTP tests prove project 409/404/400 contracts and node 409/404 envelopes;
- component tests expose pending status, lease availability, exact retry controls,
  retained warnings, and terminal notices.

Release builds plus focused unit, integration, component, and architecture tests are
required before this boundary is considered complete.
