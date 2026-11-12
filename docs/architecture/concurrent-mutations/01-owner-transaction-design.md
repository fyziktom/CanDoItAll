# Owner transactions and the Projects definition pilot

Status: design reviewed against the working tree on 2026-10-16; implementation and
runtime evidence are separate gates. This document does not claim the proposed
protocol already ships. The architect's 2026-09-28 archive is reference material;
current repository ownership and source determine the implementation.

## Observed behavior and the first safety boundary

The first aggregate is the editable project definition: `Project` scalar fields,
ordered `ProjectPhase` rows, and `ProjectOptionSelection` rows. Project hierarchy,
Workbench graph, task execution, CRM assignments, conversations, and usage are
different facts with different owners. They must not all invalidate one project
definition revision.

The live implementation still permits lost definition edits. In
`src/Modules/CanDoItAll.Modules.Projects/ProjectModels.cs`, `Project` has a lifetime
but no content concurrency token. `GetAsync` reads the root, phases, and options
in three independent statements without a coherent transaction snapshot.
`SaveWithReceiptCoreAsync` loads current rows and replaces the submitted lists;
its optional `ExpectedLifetimeId` check detects recreation, not intervening edits.
`SaveEditorAsync` additionally requires the original profile/lifetime admission,
but the API and agent adapters call `SaveAsync` directly.

There is also an independent intent defect. `ProjectStructureAgentService` builds
a `ProjectEditorModel` from `ProjectStructureProjectSaveRequest` using only scalar
fields. Empty default phase/option lists then reach the replacement owner method,
which deletes stored children absent from those lists. An agent scalar update
can therefore erase phases and options even without an intervening writer.
Adding a token alone is insufficient. The scalar route requires its own narrow
owner command, and it must never manufacture a complete replacement snapshot.

Current Workbench structure preconditions are already more advanced: node
`ExpectedState`, exact batch participants, and `ExpectedGraph` protect structure
edits. Preserve them. The Projects definition work complements those contracts;
it must not replace them with a coarser generic project revision. See
[`process-tool-storage-and-concurrency.md`](../process-tool-storage-and-concurrency.md).

## Responsibilities and dependencies

| Responsibility | Owner after the change | Explicit exclusions |
| --- | --- | --- |
| Definition edit expectation, commands, accepted result | `CanDoItAll.Modules.Projects.Contracts` | No EF, UI, Process runtime, MAF SDK, or provider references |
| Definition normalization, child identity validation, conditional application | Small cohesive types in `CanDoItAll.Modules.Projects` | No new partial appended to the large service as the long-term owner |
| Definition data, operation receipt mappings, coherent edit read | `ProjectsDbContext` and Projects services | No independent schema initializer |
| Canonical schema migration | `AppDbContext` model composition and `CanDoItAll.Migrations.PostgreSql` | No owner `EnsureCreated` or separate migration lineage |
| Physical transaction and cleanup mechanics | Existing Infrastructure persistence helpers | No project-specific facts or agent decisions moved into Infrastructure |
| Original agent/process authority and publication policy | Existing owning runtime and Workbench adapters | Projects does not reference Process implementation or MAF adapters |
| Search row application | Existing `ISearchIndexService.UpsertForMutationAsync` | Projects never accesses Search tables directly |
| Editor draft and conflict presentation | Existing Projects UI seam and host | Renderer does not resolve DbContexts or mutation services |
| Profile activation and destructive restore fence | Control-plane authority | No feature-owned ad hoc global epoch |

Keep `IProjectMutationSourceAuthority` as the Projects-owned port and Workbench's
existing agent/process adapters as its implementations. Keep
`CoordinatedDatabaseTransaction` for actual same-database enlistment. A new
universal mutation engine, event bus, or universal receipt repository is not
needed for the first pilot. Extract shared immutable vocabulary only when a
second concrete owner requires it; do not put mutable project receipts in
SharedKernel.

The service currently concentrates 1,500+ lines of reads, writes, hierarchy,
deletion, and projections. Limit extraction to the definition write/read slice:
an owner command handler and pure definition normalization/identity rules are
justified because they have independently testable behavior. Preserve the public
service as a thin delegation point during migration. Do not extract hierarchy,
deletion, storage, or unrelated renderers merely to complete this change.

## Definition expectation and coherent reads

Introduce one opaque equality stamp for the definition aggregate. The expectation
contains the original database namespace, project ID, lifetime ID, and definition
stamp. Existing `ProjectWriteAdmission` remains the lifecycle tuple; do not alter
its meaning so every lifecycle consumer unexpectedly becomes a content consumer.
Use typed records and enum outcomes at public C# boundaries. A serialized GUID
stamp is an equality token, never a sortable version or an authority credential.

Every accepted definition write, including child-only replacement and scalar-only
commands, advances the aggregate stamp in the same transaction as the root and
children. Updates require a nonempty original expectation. The owner must set
EF's original concurrency value from that expectation, or use an equivalent
explicit SQL predicate; comparing against a freshly loaded value launders stale
intent. Root conditions failing must roll back all child changes and receipts.

The existing application-managed token helper stamps modified root entities,
not arbitrary child aggregates. Explicitly mark/advance the root for child-only
definition changes. Preserve that invariant in any future bulk child writer.
Avoid adding a standalone index on the equality token: the target is already
located by primary key; require query evidence before adding indexes.

Use a short Repeatable Read read-only snapshot for root/phases/options in
PostgreSQL, or one equivalent bounded coherent query. This transaction ends
before returning to the caller. Do not hold it while a human edits or an agent
thinks. InMemory may prove mapping and local rules but cannot certify coherent
database snapshots or rollback. An explicit existing-ID read must distinguish
missing from a blank create template in the protected API; retain any old
compatibility behavior only outside the new protected read contract.

Root definition stamps do not cover hierarchy, logs, runs, progress, or parties.
Hierarchy mutation uses its own edge identity/graph precondition when required.
A derived dashboard is not a strong ETag representation of just the definition.
Prefer explicit edit-expectation DTO fields for current APIs. If ETag/If-Match is
later exposed, bind it to a dedicated definition representation and reject
contradictory header/body conditions.

## Intent shapes and collection identity

Use two concrete update shapes:

1. Full definition replacement for the Projects editor, containing scalar fields
   and the complete ordered phase/option lists under the original definition
   expectation.
2. Scalar definition edit for the current Project Structure/API/tool contract,
   containing only the fields that contract owns. It preserves child collections
   and still requires the original definition expectation.

Create is distinct from update and has no nonexistent target stamp. Existing
creation reservations and source authorization still apply. A maintenance import
is also a distinct already-locked operation, never a public `Force` flag.

Normalize and validate the submitted payload once. Reject duplicate persisted
child IDs, IDs owned by another project, and unknown nonempty persisted IDs.
New child rows use an explicit new-row identity or null persisted ID and receive
authoritative IDs. Use dictionaries and sets for retained-row matching and
deletion detection, reducing the current repeated `All`/`FirstOrDefault` scans
from quadratic to linear collection work. Preserve submitted index-to-accepted-ID
mapping for the existing UI acknowledgement/reconciliation contract.

Current blank option behavior deserves an explicit correction test: an existing
option whose submitted name and notes become blank is currently kept in the
deletion set and then skipped, leaving its old stored value. Define blank optional
rows as omitted from the accepted set, so a user can clear them without hidden
retained data. Keep default empty categories as read/editor conveniences, not
persisted rows. Keep CurrentPhase free text and existing date normalization unless
a separate domain decision changes them.

For the first pilot preserve accepted-save UpdatedAt/stamp advancement semantics,
including normalized no-change saves. A no-op optimization requires a later
documented decision because ordering, activity, approvals, and receipt hashes
depend on it. Never accept a stale request as a no-op simply because selected
current values happen to equal the submitted values.

## Transaction and lock protocol

The measured P02a definition path uses the narrow protocol below; other existing
mutation paths retain their current guarded Serializable mode. Technical retries,
if added, recreate the context and whole transaction while preserving the exact
original command, expectation, operation ID, and authority. Business content
conflicts are not technical retries.

The pre-change baseline rejected 50-65 of 100 disjoint writes with PostgreSQL 40001.
A bounded eight-connection reproduction then accepted only one writer in each of
12 samples, including root-only updates with index scans. Held `SIReadLock` entries
included shared primary-index pages; disabling sequential scans did not help.
See the [diagnostic record](../../../codex/bundles/concurrent-mutations/validation/projects-ssi-diagnostic.md).
This justifies reopening the initial isolation decision rather than adding retries
that amplify unrelated contention.

`ProjectSourceMutationScope.BeginDefinitionAsync` is an explicit Projects-only
entrypoint. For PostgreSQL definitions without source authorization it opens
Read Committed and acquires the complete existing sorted project/hierarchy/external
identity advisory-key set before owner reads. These database keys coordinate with
existing deletion, hierarchy and reservation paths. It does not acquire the generic
helper's process stripes; PostgreSQL supplies the shared guard. InMemory retains
the existing striped test path. The generic helper and ordinary scope entrypoint
are unchanged.

This branch is safe only with mandatory original profile/project/lifetime/revision
validation, a root original-value concurrency predicate, explicit root advancement,
all child changes enclosed in that transaction, and existing uniqueness constraints.
Never use it for an unguarded predicate-based owner mutation. The source-authorized
definition branch retains Serializable and existing source-first ordering/rechecks
until that source protocol is independently audited. Its known 40001 aborts reject
the submitted attempt without an automatic retry. Unknown commit outcomes remain
unconfirmed. The owner classifies known conflicts from a fresh context after rollback,
and preserves confirmed commit knowledge through cleanup failures.

Existing order is significant:

1. Validate bounded input and obtain trusted actor/operation context.
2. Resolve a completed matching operation before treating its obsolete original
   stamp as a new edit; disclosure still requires current permission.
3. Acquire the existing agent/process source lease before opening the Projects
   transaction. Freeze the complete expected lifecycle/source set.
4. Open the owner transaction. The authorized PostgreSQL branch performs its
   existing source `RequireForMutationAsync` before project advisory locks.
   The ordinary branch currently acquires process stripes, opens Serializable,
   and acquires sorted project/hierarchy advisory keys.
5. Validate lifecycle, original definition stamp, domain rules, and any declared
   current-input publication conditions under the required owner protection.
6. Apply root/children, accepted result receipt, and the selected search policy
   using the actual owner transaction. Enlisted participants are awaited
   sequentially on the shared connection.
7. Recheck source authority/lifecycle where the existing protocol requires it;
   commit and record confirmed commit knowledge immediately.
8. Retire participation and release resources. Optional observers run after
   release and cannot reclassify a confirmed commit as rejection.

Within each lock class acquire the complete stable sorted key set. Do not impose
one alphabetical order across source/claim and target/project lock classes, and
do not acquire new project keys after a late read. A newly discovered required
key means rollback/replan acquisition or explicit bounded-scope rejection.
Nested entry into the same process semaphore can self-deadlock. Use explicit
enlistment instead of beginning a second top-level owner scope.

`SerializableMutationScope` uses 64 static process stripes for PostgreSQL as well
as InMemory. Disjoint projects and different database profiles may collide in
the same host. The authorized source branch deliberately bypasses these stripes.
Instrument and benchmark the existing paths separately before changing that
strategy. Removal, replacement by per-key locks, or a larger stripe count is not
part of the correctness fix without multi-host proof.

### Snapshot timing after advisory waits

The first advisory-lock statement can establish a Serializable snapshot before
the lock is granted. A later plain read may still see the pre-wait version. The
current `ProjectAdmissionSnapshotRaceTests` already tests deletion/recreation
across that wait and relies on the final `FOR KEY SHARE` lifecycle read to force
a serialization failure. This is useful existing proof, not a new unverified
assumption.

Do not extend that lifecycle proof to ordinary content freshness. `FOR KEY SHARE`
protects key/lifetime existence but does not exclude normal non-key field updates.
For target edits, the original token predicate plus PostgreSQL serialization
failure handles overlapping updates. For strict source-current publication, use
the source owner's compatible writer guard and a locking read appropriate to
content (`FOR UPDATE`/equivalent), with deterministic wait-snapshot tests. Every
conflicting source writer must participate. A plain read in a coherent snapshot
provides historical consistency, not wall-clock publication freshness.

### Cleanup and confirmed commit

Two concrete infrastructure paths currently skip stripe release when transaction
disposal throws: the `BeginAsync` catch and `DisposeAsync` in
`SerializableMutationScope`. Correct both with finally-based release; maintain
idempotent disposal and test fault injection, not only cancellation.

`ProjectSourceMutationScope.CommitAsync` marks its private `committed` flag after
SQL commit and then disposes the source lease. If that disposal throws, callers
can observe failure after the database committed. `SaveWithReceiptCoreAsync` also
explicitly disposes before returning its result. Preserve confirmed commit
knowledge in an owner outcome accessible across these cleanup paths. Cleanup
errors are actionable secondary failures; precommit errors remain rejection or
unconfirmed according to what is known. Do not simply swallow all cleanup errors.

`CoordinatedDatabaseTransaction` already binds physical connection/transaction,
checks profile compatibility, and retires leaked participants. Keep it. Do not
use `Task.WhenAll` on enlisted commands or let detached callbacks inherit an
apparently valid ambient frame.

## Durable operation truth and replay

Existing `ProjectCreationReceipt` is an in-memory compensation witness containing
the original lifecycle, reservation, fingerprint, and source authorization.
`ProjectCreationReservationRecord` records reservation lifecycle. Neither is a
durable ordinary create/update result with semantic-body identity. Runtime tool
journals describe invocation progress and cannot independently prove an SQL
business commit. Preserve those records and link them to the Projects result;
do not substitute their distinct meanings.

The pilot requires a small Projects-owned receipt row written in the business
transaction. Its logical fields are:

- Durable operation ID and trusted initiating principal/client scope.
- Canonical dataset namespace, command kind, target ID/lifetime, and hash-domain
  version.
- Deterministic semantic request digest covering original expectation, normalized
  intended effect, original basis/approval binding when applicable, and creation
  reservation identity. Transport timestamps/correlation headers are excluded.
- Accepted definition stamp, commit timestamp, and exact accepted acknowledgement
  or immutable accepted-result reference sufficient for draft reconciliation.
- Result-detail retention and compact replay-denial metadata.

The authenticated principal/scope must come from a trusted adapter/context, not
from a model-supplied actor string. The current service signatures lack this
ordinary-operation context; introduce a minimal typed boundary and wire the
HTTP/UI/tool adapters explicitly before promising cross-client result lookup.

Serialize concurrent equal operation IDs using a unique receipt constraint or a
proven operation guard. The receipt and business effect commit together. On a
duplicate conflict, dispose/rollback the failed context and query the canonical
database afresh: the losing Serializable snapshot may not see the winning row.
Do not query a read replica for authoritative reconciliation. An absent receipt
while another attempt is in flight means pending/unknown, not proof of noncommit.

Same ID with a different semantic digest is rejected. Same ID with the same
committed command returns the exact prior acknowledgement even when the current
target stamp has advanced. Permission to disclose remains current. A newly
reconciled user/agent intent receives a new operation ID; a network retry or
outcome lookup retains the original ID. Create duplicates return the same target
and child IDs. A postcommit readback is labelled current state, never relabelled
as the original accepted result.

No precommitted InProgress row is needed for a short all-SQL operation. Adding
one would create lease expiry, fencing, crash-recovery, and claim/effect ambiguity
without an established requirement. Existing long-running owner journals retain
their own claim protocols.

Full acknowledgement storage must be bounded. Exact values are useful for
normalization and reconciliation, but duplicating unlimited text/collections on
every write is unacceptable. Ratify request/result byte and row limits against
retained data before implementation; reject oversized commands explicitly.
Do not silently truncate a receipt or a decision dependency set.

## Search projection choice

Today save commits and releases the owner scope, then invokes a second guarded
transaction with an old search payload. The guard checks lifetime only. Older
save A can therefore write its projection after newer save B, and a crash can
omit projection work entirely. `RunPostCommitActionAsync` logs failure but has no
durable repair obligation.

For this pilot choose the existing same-database transactional participant,
`ISearchIndexService.UpsertForMutationAsync`, provisionally subject to the measured
bounded index-cost gate. Build its input from accepted normalized values, enlist
it in the Projects transaction, and commit definition + receipt + search row
together. Existing deletion already removes project search through an enlisted
owner port. No embeddings, filesystem reads, HTTP calls, or portfolio enrichment
are permitted inside this boundary.

This deliberately changes current save semantics: search failure rejects the
whole SQL mutation. Update the existing
`CreateAsync_returns_the_committed_project_when_search_projection_fails` test to
the chosen behavior, and add full rollback evidence. Preserve optional activity
feed as best effort after commit. Mandatory audit/receipt evidence is durable.

If measured index work makes that boundary unacceptable, replace this choice
before closure with a source-owned durable rebuild intention and independent
worker. It must coalesce safely, read canonical current content under the owner
guard, preserve lifetime/deletion tombstones, expose backlog age, and retry
boundedly. An outbox alone does not solve ordering; GUID stamps cannot order
events. Do not ship the old callback while claiming search consistency.

## Lifecycle, migration, transfer, and restore

Add mappings to both the Projects owner context and complete canonical model;
generate one migration from the then-current migration head. Initialize a distinct
valid stamp for every existing aggregate. Do not independently migrate Projects
or reset retained data. Run retained-data upgrade and model-parity tests.

`ProjectsProfileTransferStore` is a bounded maintenance writer using an explicitly
locked Serializable target session. It already allocates fresh target lifetimes
and treats reservations/retirements as imported history. Allocate fresh local
definition stamps too. Source receipts may be preserved as inert provenance but
must not authorize replay in the target. Transfer preflight/inspection must
include new receipt history if replacement would destroy deduplication evidence.

Delete does not cascade away compact operation evidence required by retained
clients/checkpoints. Full result detail can expire earlier, subject to privacy
rules; an expired operation cannot execute as a new request just because its
receipt detail was removed. Keep a bounded tombstone or enforce a durable minimum
admissible operation generation. Define and test the retry horizon before adding
receipt cleanup.

Current `CanonicalRuntimeDatabase.Generation` is constant zero; the runtime
switching generation is process-local state. These are not established durable
restore epochs. Replacing a database with an older backup can restore old IDs,
lifetimes, stamps, and missing future receipts. Safe destructive restore therefore
requires a control-plane activation namespace changed by the restore protocol
and verified by all protected entry points. It must live outside the replaced
history or be explicitly advanced at activation; a row restored unchanged from
the same backup is insufficient. Do not rotate it on normal host restart.
Until that activation path is implemented/proven, report restore replay safety
as incomplete and keep restored protected writes closed pending re-admission.

Cutover requires stopping/draining old enabled writers and incompatible open
sessions. Missing expectations fail explicitly; no nullable legacy bypass. Once
enforced, rollback uses a compatible enforcement build or forward repair with
protected writes paused. Reinstalling an older last-write-wins binary is unsafe
even if the SQL migration is additive.

## Bounded costs and acceptance evidence

The ordinary edit read touches only the requested root and indexed children.
The write performs one bounded load of existing children, linear normalization/
matching, root CAS, receipt, and one small enlisted projection. Source checks use
existing complete `GetManyForMutationAsync`/admission batches per owner rather
than one call per row or a full hierarchy dashboard.

Measure SQL statement counts, rows, elapsed transaction time, local/advisory lock
wait and hold, connection waits, bytes, allocations, and receipt growth. Compare
small and large definitions; same-project and disjoint-project concurrency;
deliberate stripe collisions; source-authorized and ordinary branches; one and
two host processes. Separate deterministic race tests from noisy load tests.
No test may rely only on the same-process stripe to establish correctness.

The archive's 15% uncontended p95 and 10% disjoint-throughput regression figures
are investigation thresholds, not published SLOs or permission to weaken safety.
Ratify limits from a measured baseline. No long LLM/user wait or external I/O
may enter a SQL critical section. Receipt capacity includes index/WAL cost and
both detailed-result and compact-dedupe retention; it is not just row payload.

## Required tests and closure

| Proof | Concrete acceptance |
| --- | --- |
| Original-state safety | Two clients read A; one adds a phase, the other's old full save conflicts without losing it |
| Scalar intent | Real Project Structure API/tool scalar save preserves phases/options; stale scalar update conflicts |
| Aggregate coherence | Barrier between root and child reads cannot return old root stamp with new children |
| Child ownership | Duplicate/foreign/unknown child IDs reject atomically; blank persisted option is removed; accepted IDs remain stable |
| Cross-process SQL | Two independent hosts/connections race; no local semaphore is the sole coordination proof |
| Advisory wait | Ordinary content update while waiter holds an old snapshot fails/retries technically without rebinding original intent |
| Source policy | Strict source change/revoke rejects publication; deliberate snapshot computation retains exact provenance and still obeys target CAS |
| Receipt | Same-ID duplicate before/after commit/restart returns one effect; mismatched digest rejects; lost response reconciles original IDs |
| Commit knowledge | Inject source/transaction disposal, observer, and readback failures after confirmed commit; never report known rejection |
| Search | Atomic projection rollback on failure; delayed old callbacks cannot regress the projection; deletion cannot revive content |
| Migration/lifecycle | Existing data gets valid stamps; owner/canonical mapping parity; fresh import stamps; old lifetime and restored namespace reject |
| UI | Existing submitted/live acknowledgement tests plus conflict recovery, typing during save, navigation, and two browser sessions |
| Boundaries | Contracts stay data-only; Projects gets no new Process/MAF implementation dependency; sandbox remains independent |

Relevant existing suites are `ProjectsServiceIntegrationTests`,
`ProjectsOwnerPersistenceTests`, `ProjectAdmissionSnapshotRaceTests`,
`ProjectCreationReservationIntegrationTests`, `CoordinatedDatabaseTransactionTests`,
`AgentNativeProjectAdmissionIntegrationTests`,
`ProjectStructureAgentRuntimeToolRoundTripIntegrationTests`,
`ProjectsEditorMutationTests`, and the Projects UI/browser suites. These are
selection anchors, not a claim that their current tests cover the new protocol.

Build each affected production project; discover the exact focused test filter
and confirm its count before executing. Use isolated PostgreSQL 18 for transactional
proof. Run the mandatory portability-static review/refresh/enforcement procedure
after protected source changes, and record final enforcement without
`--write-baseline`. Do not declare end-to-end completion with missing SQL,
API/tool, UI, retained-data, performance, or cleanup evidence.
