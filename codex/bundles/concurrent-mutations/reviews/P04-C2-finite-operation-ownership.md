# C2 decision: shared finite epochs, owner-specific receipts

Status: parent accepts this design direction on 2026-10-16, including the two corrections from [independent persistence review](P04-C2-persistence-review.md): explicit pool installation and separate monotonic metadata cleanup. No implementation, migration, build, test or capacity proof accompanies it. This refines [Card C](../plan/P04-card-C-atomic-task-plan-publication.md) and future W1c in [Workspace defaults](../plan/P05-workspace-first-owner-refinement.md). C1 remains independent; extraction is a separately reviewed C2 prerequisite, not yet released for source implementation.

The [C2a concrete extraction card](../plan/P04-C2-neutral-epoch-extraction.md) now fixes identities, file/API ownership, migration/rollback refusal, no-FK/trigger lock constraints and validation inventory. Its first checkpoint installs only the existing Projects definition family; downstream receipts and pool installation follow separately. It supersedes provisional owner-local metadata and initialize-if-missing wording in Card C.

## Decision and ownership cost

Extract one finite-epoch persistence mechanism into Infrastructure, with **separate strongly identified pools** for Projects, Workbench task plans and future Workspace defaults. Keep receipts, semantic codecs, caller binding, access decisions, effect admission and transactions with each owner. Projects definition and planned P03 lifecycle operations share the Projects pool but have distinct receipt families/disclosure rules. Do not create a dataset-wide epoch or operation lock.

| Alternative | Concrete consequence | Decision |
| --- | --- | --- |
| Three owner-local implementations | Avoids moving existing metadata now, but repeats activation, deadlines, precheck/lock ordering, rotation, retirement, old-namespace cleanup, confirmed accounting and health rules. Recent retirement/health fixes would need three independent repairs. | Reject for C2. Workbench is a concrete second consumer; Workspace confirms the common requirements. |
| Shared implementation with supplied table names or mutation callbacks | Avoids metadata migration by introducing configurable SQL/mapping machinery or hiding owner authority/effects inside a common coordinator. | Reject. Neither is necessary for two small control tables. |
| One shared pool for every owner | Couples issued policy, expiry readiness, maintenance contention and recovery availability of unrelated owners. | Reject. Shared code does not require shared admission state. |
| Neutral metadata per pool plus owner receipt participants | One metadata migration and Projects adaptation now; subsequently one lifecycle implementation with separate owner payload/query code. All three applications already reference Infrastructure. | Select, subject to the migration and requalification gates below. |

Extract before enabling C2/C3, **not before proving C1**. If compatibility or performance gates fail, leave publication dormant; do not temporarily enable an unbounded receipt path or silently introduce a second epoch implementation. This has a larger immediate diff than copying the service, but avoids copying already subtle lifecycle corrections. No new assembly is justified.

## Source-grounded split

Inspected Projects `ProjectOperationEpochService`, `ProjectDefinitionOperationRecords`, `ProjectDefinitionOperationStore`, `ProjectDefinitionReceiptReader`, `ProjectDefinitionOperationProtocol`, `ProjectDefinitionOperationMaintenance`, `ProjectOperationMaintenanceResult`; Infrastructure `DatasetLifecycleStore`, `CoordinatedDatabaseTransaction`, `DatabaseTransferOperationRunner`; Composition `DatasetActivationCoordinator`; Workbench `ProjectTaskPlanBasisCapture` and transfer participants; Workspace `Models/WorkspaceModels.cs`, its defaults owner and W1 plan. Names below are within those existing projects.

| Current Projects responsibility | Destination |
| --- | --- |
| Activation initialization/closure; current epoch; SQL deadlines; expiry precheck; lifecycle/epoch admission; DB time; issued horizon; rotation; contiguous retirement | Infrastructure `Persistence/FiniteOperations/FiniteOperationEpochStore.cs`, neutral records/context and timing types. No Project caller, command, target, codec or result type enters it. |
| `GetWriteContextAsync` binding the caller and returning definition versions/limits; typed refusal mapping | Projects protocol or the existing small facade delegates metadata access and builds the unchanged Projects contract. |
| Receipt DELETE/EXISTS portions of `PurgeRetiredBatchAsync` and `ObserveEligibleWorkAsync` | New Projects `ProjectDefinitionReceiptRetention.cs`. Workbench/Workspace supply their own receipt queries. Infrastructure does not know receipt table names, caller columns or payload formats. |
| Empty eligible epoch/old namespace cleanup; acknowledged maintenance totals; bounded cycle/health bookkeeping | Shared maintenance, requiring all registered receipt families in the pool to report emptiness before metadata removal. Preserve confirmed-after-commit and disposal failure handling. |
| `ProjectDefinitionOperationStore.ObserveAsync` | Remains Projects-owned; joins neutral metadata filtered by Projects pool. Keep activation, pool protocol, watermark, epoch, receipt and target availability in **one SQL statement**. Never replace this with an epoch read followed by a receipt read. |
| Operation exclusion, digest equality, receipt insertion, exact acknowledgement, authorization, commit knowledge and fresh recovery | Remain with each owner. No `ExecuteMutationAsync`, commit callback, common serializer, automatic retry or source-authority framework. |

The roughly 400-line service is split by responsibility, not renamed wholesale. Receipt-shaped SQL stays in Projects; lifecycle SQL exists once. Owner replay still precedes effect authority, and fresh replay after a failed/duplicate transaction occurs only after releasing its context/source. Extraction must not reintroduce nested connections or pool starvation.

## Minimal contracts and storage

Use validated immutable Guid wrappers `FiniteOperationPoolId` and `FiniteReceiptFamilyId` in Infrastructure. Permanent constants belong to each module; Composition registers them explicitly. Never derive them from CLR names, caller text, profile aliases or HTTP input. Pool identity is internal; existing Projects operation addresses/epoch JSON need no new field.

- `Infrastructure_FiniteOperationState`: PK `(NamespaceId, PoolId)`, CurrentEpoch, RetiredThroughEpoch, positive RetentionProtocolVersion.
- `Infrastructure_FiniteOperationEpochs`: PK `(NamespaceId, PoolId, Epoch)`, OpenedAt, AdmissionClosesAt, issued MutationDeadlineMilliseconds/ReplayRetentionSeconds, Open/Closed and ClosedAt. Preserve the constraints and unique Open epoch per namespace/pool. No receipt/target cascade.

RetentionProtocolVersion identifies the compiled receipt-family/maintenance interpretation, not a command codec. Mismatch means unsupported readiness, never an empty pool. Every metadata mutation and family cleanup predicate requires the registered version; owner lookup validates it before claiming absence. Adding a family to an existing pool requires a migration/cutover, not opportunistic DI discovery.

Proposed signatures, not existing declarations:

```csharp
Task InitializeNewNamespaceEnlistedAsync(DatasetActivationBinding binding,
    FiniteOperationPoolRegistration pool, CancellationToken cancellationToken);
Task RequireReadyEnlistedAsync(DatasetActivationBinding binding,
    FiniteOperationPoolRegistration pool, CancellationToken cancellationToken);
Task CloseForReactivationEnlistedAsync(DatasetActivationBinding previous,
    DatasetActivationBinding current, FiniteOperationPoolRegistration pool,
    CancellationToken cancellationToken);
Task<FiniteOperationEpochSnapshot> ReadCurrentAsync(DatasetActivationBinding binding,
    FiniteOperationPoolRegistration pool, CancellationToken cancellationToken);
Task<FiniteOperationEpochSnapshot> EnterAsync(DbContext owner,
    DatasetActivationBinding binding, FiniteOperationPoolRegistration pool,
    long originalEpoch, CancellationToken cancellationToken);
Task RequireAdmissibleAsync(DbContext owner, FiniteOperationEpochSnapshot epoch,
    CancellationToken cancellationToken);
Task<FiniteOperationRotationResult> RotateExpiredAsync(DatasetActivationBinding binding,
    FiniteOperationPoolRegistration pool, CancellationToken cancellationToken);
Task<FiniteOperationRetirementResult> AdvanceRetirementAsync(DatasetActivationBinding binding,
    FiniteOperationPoolRegistration pool, CancellationToken cancellationToken);
```

Snapshot contains validated namespace/pool/epoch and all issued timing values. Registration contains fixed pool/protocol identities, validated timing/maintenance options and a closed list of receipt participants; no semantic delegate/command handler. Current payload limits remain owner-specific. Map existing configuration keys to neutral timing values without reinterpreting issued rows.

`EnterAsync` uses the supplied same-profile PostgreSQL transaction; it creates no transaction/connection and never commits. Activation uses existing `CoordinatedDatabaseTransaction` enlistment. Maintenance owns short neutral-context RC transactions. Independent ReadCurrent is valid for admission metadata, not a composed receipt observation.

Initialization is limited to an explicitly prepared new namespace Bootstrap/Reactivate (including exact-plan resume). Matching Active readiness and Attach use `RequireReadyEnlistedAsync`: required state/current epoch/protocol must already exist; missing or inconsistent evidence is an error, never permission to mint epoch 1. Readiness validates structure without refreshing an expired Open epoch: the maintenance worker rotates it, and admission metadata remains unavailable meanwhile. Adding a pool to an existing namespace requires the reviewed stopped-host installation/migration step, with explicit persisted initial timings and idempotence. The source card must select that step; startup cannot infer first installation from an absent state row.

One real interface boundary is justified for independently owned receipt tables:

```csharp
interface IFiniteOperationReceiptRetentionParticipant {
    FiniteOperationPoolId Pool { get; }
    FiniteReceiptFamilyId Family { get; }
    Task<FiniteReceiptPurgeObservation> PurgeRetiredBatchEnlistedAsync(
        FiniteReceiptPurgeRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<FiniteOperationEpochAddress>> FindNonemptyEpochsEnlistedAsync(
        FiniteEpochInspectionRequest request, CancellationToken cancellationToken);
}
```

Requests contain exact active binding/pool protocol and row/byte bounds, or at most 128 eligible epoch addresses. Participants create their owner context through existing coordinated enlistment. No independent connection, commit, payload decode or external effect. Purge observations contain staged counts/bytes and Unknown/NoneObserved/Present evidence; only the coordinator's acknowledged commit confirms counts. Post-delete observation remains a separate SQL statement. Empty-epoch inspection uses bounded EXISTS/key queries, not bodies or a full receipt count. Its returned addresses must be a subset of the requested set.

The coordinator uses **separate short transactions** for each single-family purge, each family's empty-epoch inspection, and final neutral metadata deletion. It commits/releases one family's transaction before entering another; no transaction retains locks on multiple families' receipt tables. All-family empty evidence is combined only for already Closed/retired eligible epochs. Supported effect writers cannot append receipts after closure, so emptiness is monotonic across these reads. The final DELETE repeats exact binding, protocol and eligible-state/horizon predicates and deletes only the intersection proved empty by every registered family. Failed/unknown/missing-family evidence retains metadata. This maintenance-specific monotonicity does not relax the one-SQL user lookup rule.

Validate duplicate/missing families against each owner's explicit required set, supported protocol versions and a batch byte cap that can delete at least one maximum supported receipt. Composition assembles the registrations; Infrastructure sees only neutral types/interface. Missing required participants fail readiness and maintenance; absence from DI is never evidence that a receipt family is empty. Model/registration tests catch omitted durable families. P03 cannot reuse the definition participant for lifecycle receipts.

## Lock and transaction invariants

| Path | Required order |
| --- | --- |
| New effect | Bound/replay before effect authority; non-SQL source lease; BEGIN current owner isolation; finite deadlines; expiry precheck before lifecycle SHARE; lifecycle SHARE; pool epoch SHARE; owner operation exclusion; established SQL source guards; complete sorted P set or Workspace settings table; final epoch/source/head check; effect plus owner receipt commit. No state-row lock on this path. |
| Rotation | Lock-free exact-binding/server-time precheck; lifecycle SHARE; pool state UPDATE SKIP LOCKED; current epoch UPDATE SKIP LOCKED; close/issue atomically. State before epoch, with post-wait recheck. |
| Retirement | RC exact-binding/protocol precheck; pool state UPDATE SKIP LOCKED; bounded ordered Closed prefix; monotonic conditional watermark. Preserve current standalone advancement: **no lifecycle SHARE or epoch UPDATE**. Both persisted horizons must have elapsed. |
| Purge / metadata cleanup | Only active-pool retired epochs or Closed old namespaces beyond both issued and actual-close horizons. One family's receipt SKIP LOCKED and row/byte bounds per transaction; repeat binding/protocol predicates. No lifecycle/live-epoch/project/settings/source lock. Commit purge, then inspect each family in a separate released transaction. Final neutral-only DELETE repeats eligibility and removes only all-family empty keys. Eligible Closed epochs cannot admit a new receipt between these reads. |
| Activation/import | Offline lifecycle UPDATE precedes pool closure/initialization in one transaction. Existing transfer takes lifecycle utility table EXCLUSIVE before its first Serializable snapshot, then table/owner exclusions. Never acquire lifecycle after state/settings/P. |

Projects keeps current isolation/source behavior. Workbench C3 still requires independently proved RC source adapters. Workspace W1c puts epoch/operation exclusion between lifecycle and W1b's singleton settings table lock. Bootstrap/preference transfer preserve W1b head/revision participation and do not fabricate human operation receipts. No effect/source callback enters Infrastructure.

Owner operation-exclusion keys remain owner/pool-specific. Equal operation GUIDs in different pools must not accidentally acquire a shared global operation lock. Real shared business targets still use their existing common owner guards, such as P for the same project.

The existing P03 family/address distinction also remains: definition and lifecycle share a pool, but use distinct typed receipt/digest/operation-lock domains. A UUID in one family is not evidence for another effect.

Do not place all-family receipt inspection after a single-family DELETE in the same transaction. An exclusive import can hold receipt A's ACCESS EXCLUSIVE lock while waiting for receipt B; B's purger would then wait for A's read lock. Lifecycle-first import does not exclude purge, which intentionally has no lifecycle SHARE. Separately released single-family inspection avoids this new inversion without a new complete receipt-table lock-plan abstraction. Include all these short phases in the aggregate cycle deadline.

Retirement commits before later purge: a crash retains excess bytes without reopening admission. Watermark alone means Expired; elapsed minimum retention does not invalidate a still-retained ClosedAbsent proof. Busy/failed/unknown evidence cannot clear pressure. Confirmed counts survive later cleanup failure. These are shared rules, not owner-specific variants.

## Concrete maintenance fairness

Use one narrow Composition-owned finite-operation maintenance hosted service with the shared bounded cycle, replacing the Projects scheduling loop at cutover. Scheduling state is fixed-size per registered pool/family, not per user/operation. Initially preserve each pool's own two-second/64-batch budget and Projects' 60-second default, 128 rows and 16 MiB per batch. These numbers do not qualify task-plan or Workspace capacity.

Select due pools round-robin and advance the cursor **before awaiting work**. Each due pool gets at most one cycle before a due peer gets a second; a fault does not abort peer service. Within a pool, advance the family cursor before each single purge transaction; visit every family once before returning to one. Retain the cursor across spent budgets/failure/cancellation, so a slow first family cannot own every cycle. Zero deleted with Present marks that family contended for the rest of the cycle; continue peers, then stop rather than spin. NoneObserved needs no second batch that cycle. Pool health clears only after a successful Empty cycle in which every family observes relief, retirement is not Busy and no maintenance phase failed; unknown peer evidence prevents recovery.

Each participant retains the bounded eligible-epoch query and receipt SKIP LOCKED behavior. Locked rows are not absence. Metadata removal uses the bounded all-family inspection, never the last family's local emptiness. A blocked family cannot deny another family/pool its next turn. Rotation/retirement remain bounded and their outcomes separately observed. This does not promise fairness within a family's first selected eligible epoch: current ordered epoch selection can encounter a fully locked earlier epoch. Measure that limit in mixed-pressure proof and preserve explicit contention/health evidence.

Measure aggregate scheduled demand and per-pool lateness. Three two-second cycles can consume six seconds before cleanup overhead; three five-second cadences are not automatically sustainable in one serial worker. Keep current defaults and finite per-pool metric labels, and make no cadence/capacity claim without mixed-pool evidence. Fair turns are neither a hard execution deadline nor a guaranteed purge rate. Do not add a global receipt counter/mutex to foreground admission as a substitute for capacity planning.

## Exact cutover edits and compatibility

1. Add Infrastructure neutral records/configuration/context/store and maintenance. Remove the two control mappings from `ProjectsDbContext`; retain `ProjectDefinitionReceiptRecord`. Canonical `AppDbContextModelRegistry` already collects Infrastructure mappings. Require owner/canonical model parity and an ordinary migration in `src/Foundation/CanDoItAll.Migrations.PostgreSql`.
2. Migrate **all Projects namespaces**, including historical ones. Rename/move control tables and add the permanent Projects pool/protocol identity; rebuild PK/Open indexes. Preserve current epoch, retired watermark, timestamps and issued timings exactly. Never reset epoch 1, clamp retention, mint namespace or rewrite accepted payload/digest/caller bytes. Check row/key correspondence and fail on inconsistent retained metadata.
3. Adapt Projects `ProjectDefinitionOperationProtocol`, `ProjectMutationAuthority`, `ProjectDefinitionOperationStore`, `ProjectDefinitionReceiptReader` and retained epoch facade/tests to neutral snapshots/mechanics. Existing public wire/caller/codec contracts stay unchanged. Move cleanup SQL to `ProjectDefinitionReceiptRetention`; remove duplicated lifecycle SQL from the facade. Replace `ProjectsModuleServiceCollectionExtensions`' worker registration with explicit Composition registration.
4. Update `DatasetActivationCoordinator.CreateStores`, `EnsureReadyAsync`, `ApplyAsync` to build the profile-bound neutral store and explicit installed pools. New namespace bootstrap/reactivation initializes/closes them in its existing coordinated transaction; matching Active readiness and attach validate existing metadata only. Exact resume preserves the prepared plan. Workbench joins at C2 through explicit offline pool installation; Workspace at W1c. Define and persist the new pool's initial timings in that reviewed installation, rather than creating it on any startup that finds no state. No generic activation callback engine or Infrastructure-to-module edge.
5. The later Workbench C2 checkpoint adds **only `Workbench_TaskPlanReceipts`** plus its codec/protocol/retention participant and pool registration; C2a extraction adds none of them. The card's provisional Workbench state/epoch tables are superseded. W1c similarly owns settings receipts/caller/read policy and its registration. W1a/W1b CAS and client work remain prerequisites. P03 adds a distinct Projects lifecycle family with minimal deleted-target disclosure; definition's live-target check must not leak into it.
6. Add owner receipt types and explicit retained-history residue to existing Projects/Workbench/Workspace transfer participants. Retained task receipts block overwrite after target deletion. Receipts are not executable portable-package data. Neutral control tables are activation metadata, not project content or automatic nonempty-target residue; ordinary transfer neither exports nor resets them. Preserve lifecycle-first exclusion and receipt-table locks. A narrow Workspace preference transfer preserving history does not need every unrelated receipt table. Restore/reactivation closes every installed pool.
7. Explicitly exclude Workbench receipt bookkeeping from canonical task-effect observation while retaining real task/binding observation. Neutral-context metadata does not pass through Workbench's tracker. Update the card's provisional three Workbench control/history entity inventory to one owner receipt plus neutral mappings.

**Mixed-version hosts are unsupported during this cutover.** Stop and verify all writers/maintenance processes, including secondary workers, before metadata DDL; drain in-flight work normally. Apply one migration and deploy matching registration/protocol versions before readiness. Old Projects binaries lose their table names and must fail readiness/admission. No compatibility view, dual write, fallback read or automatic activation reset. A later pool-family change advances retention protocol version and uses the same explicit stopped-host procedure. Version predicates alone do not fence already running older transactions. If older hosts cannot be stopped, implementation is blocked pending a separately reviewed rolling-upgrade protocol. Rollback cannot drop a newly used family or lower its protocol while issued evidence remains promised.

## Required proof before enabling C2/C3

- Lossless retained-data migration across active/old namespaces, inconsistent evidence refusal, stricter current policy, exact old replay and ClosedAbsent/Expired. Prove old-binary readiness failure and upgrade/rollback handling; unchanged epoch JSON is not server compatibility proof.
- Same-address exclusion within a pool; disjoint pools do not wait on each other's state/epoch rows. One-SQL lookup races rotation/retirement/purge/reactivation without false absence. No nested connection under an owner/source lease.
- Held live epoch SHARE permits older-prefix retirement; expiry precheck remains before lifecycle SHARE; rotation/import/activation both orders avoid inversion. Requalify Projects fairness/small-pool tests, then Workbench source guards and Workspace singleton ordering separately.
- Two pools and two families: permanently contended family, faulting family, nonempty peers, cursor continuation after budget exhaustion, simultaneous workers, metadata retained until the last family empties. Disposal preserves confirmed totals; unknown evidence cannot claim recovery. Missing/duplicate/unsupported registrations fail closed.
- Matching Active readiness refuses missing previously required pool state/current epoch; explicit new namespace activation and new-pool installation initialize once and retain issued timings across restart. Metadata migration validates every old namespace/protocol and receipt correspondence.
- Actual exclusive-import table A-then-B race against family B purge; separately released inspections cannot form the cross-family cycle. Concurrent metadata deletion, activation/protocol change between inspections/final DELETE, failed/unknown/missing family, and attempted post-close insert all retain the required evidence. Confirmed single-family purge totals survive later metadata-cleanup failure.
- Mixed foreground/maintenance measurements: unchanged Projects comparison, actual task-plan receipt sizes, cadence/lateness, table/index/TOAST/WAL, sustained arrivals/backlog. Existing Projects-only numbers do not qualify the shared worker or justify shorter promises.

These are planned arrangements, not discovered/passing counts. The [concrete source card](../plan/P04-C2-neutral-epoch-extraction.md) supplies the permanent IDs, migration shape/installation semantics, API/failure/predicate/transaction boundaries and current source versus prospective test inventory. Its migration timestamp will be generated only with source release; no migration is claimed here. Parent accepts the extraction cost because Workbench is a concrete second owner; B1/C1 proof, card review and explicit source release still gate implementation.
