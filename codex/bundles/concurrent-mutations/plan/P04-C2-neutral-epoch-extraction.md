# C2a: extract finite metadata without changing owner receipts

Status: C2a source is authored in the managed checkout, with the [source inventory and unexecuted proof](../validation/P04-C2-neutral-source.md) recorded separately. Parent released preparation after B1's initial 22 passing rows and independent review of C1's authored source. The [accepted ownership decision](../reviews/P04-C2-finite-operation-ownership.md) and [independent persistence review](../reviews/P04-C2-persistence-review.md) govern this card. C1 runtime proof and the final static gate remain prerequisites for C2 integration; migration generation/review, retained-data proof and activation remain separately gated. No C2 migration or validation has run. C2a extracts the existing Projects implementation only. Workbench task-plan, P03 lifecycle and Workspace receipt families are subsequent cards; none is created or enabled here.

## Permanent identity and installation

These are permanent protocol identities, chosen once for this design. Represent them with nonempty immutable `FiniteOperationPoolId` and `FiniteReceiptFamilyId` Guid wrappers, not enums/strings supplied by a caller. Persist the Guid values in metadata; no JSON converter or new caller-controlled pool field is needed.

| Owner / purpose | Pool ID | Receipt family ID | C2a registration |
| --- | --- | --- | --- |
| Projects definition | `e6eaf1c1-5dbc-46bf-8641-1ae9fe3ae376` | `574607ce-cb66-4783-9675-8a59d4383ab4` | Pool version 1, exactly this family |
| Projects lifecycle / P03 | Same Projects pool | `d7c41f34-8330-45b5-923a-251aa6b54808` | Reserved in this document only |
| Workbench task plans | `c91aaef3-cfc1-4c29-88aa-10693fecd4e3` | `d8835be0-6fd5-4d9d-8162-1733367fc22e` | Reserved in this document only |
| Workspace defaults | `83cb614b-a665-474a-a96f-9759786eb7aa` | `8118e409-512c-428b-9d7a-aa8909b50c25` | Reserved in this document only |

P03 uses its reserved separate family ID in its own receipt migration and advances the Projects retention protocol. Do not add an unused P03 family to the C2a required set: a missing participant is an error, not an empty implementation. The fixed Projects definition constants and its registration factory belong in `Modules.Projects/ProjectFiniteOperationPool.cs`. Future family/owner constants remain documentation-only until their implementation card; Infrastructure defines no catalogue of module identities.

`RetentionProtocolVersion = 1` identifies the Projects definition-only family set and cleanup interpretation. It is separate from request schema/normalization/result versions, which stay unchanged. A family-set change requires stopped-host migration of every retained state row for that pool, including inactive namespaces. The migration must prove preservation of each old family before changing the version. Ordinary startup never changes a version.

Adding a future pool uses that owner's ordinary, stopped-host EF migration, not a runtime missing-row repair. Its migration inserts state/epoch 1 **only for the active SQL namespace**, with one database timestamp and explicit initial issued values: admission one day, mutation deadline 30,000 ms, replay retention 604,800 s. These reviewed initial defaults are persisted once, independent of later configuration. The owner's normal configuration governs subsequent rotations. If there is no lifecycle row in a fresh database, no pool row is inserted; explicit namespace Bootstrap initializes all compiled installed pools. A non-Active lifecycle row refuses pool installation. Migration history makes rerun idempotent; a pre-existing unexpected row before first installation refuses rather than `ON CONFLICT` silently adopting it. Historical namespaces predating that pool have no state and cannot have that family's receipts. This installs no new pool in C2a.

## Exact production edit perimeter

All paths below are under `src/`. No new project reference or assembly is needed.

| File(s) | Change |
| --- | --- |
| Foundation/Infrastructure `Persistence/FiniteOperations/FiniteOperationContracts.cs` | The two IDs, immutable pool registration/timing policy, epoch address/snapshot, typed admission/protocol failures and bounded participant request/results. Keep related small records together. |
| Same directory `FiniteOperationRecords.cs`, `FiniteOperationDbContext.cs` | Two metadata entities/configurations only; no owner receipts, navigation properties, FKs or triggers. |
| Same directory `FiniteOperationEpochStore.cs` | Move initialization/readiness/closure, metadata read, admission/deadlines, rotation and standalone retirement. Use existing coordinated enlistment; no owner callbacks or commit framework. |
| Same directory `FiniteOperationReceiptMaintenance.cs` | Own short single-family purge/inspection transactions and final neutral metadata cleanup. Validate participant responses and publish acknowledged batch totals. |
| Same directory `FiniteOperationMaintenanceState.cs` | Move the existing bounded cycle/result/health rules; add fixed pool/family cursors. This is state bookkeeping, not another store or registry. |
| Foundation/Infrastructure `DependencyInjection/InfrastructureServiceCollectionExtensions.cs` | Register the neutral context options/factory, scoped epoch store and singleton receipt-maintenance service using current canonical profile configuration. No module types. |
| Modules.Projects `ProjectDefinitionOperationRecords.cs`, `Persistence/ProjectsDbContext.cs` | Remove the old state/epoch CLR types/configurations and their two owner mappings. Keep the exact receipt type/table and all its constraints. Projects owner model becomes the existing seven remaining types. |
| Modules.Projects `ProjectFiniteOperationPool.cs`, `ProjectDefinitionReceiptRetention.cs` | Fixed Projects registration factory; single definition-family participant containing existing receipt-specific DELETE/EXISTS SQL. |
| Modules.Projects `ProjectDefinitionOperationProtocol.cs`, `ProjectMutationAuthority.cs` | Consume the neutral store/snapshot; build the unchanged write-context DTO here. Preserve namespace/caller validation, original operation lock domain, source/P ordering and final deadline recheck. |
| Modules.Projects `ProjectOperationEpochService.cs` | Delete after moving all callers; do not retain a forwarding lifecycle service or an unsafe `InitializeForActivationAsync` compatibility entrypoint. Owner-specific write-context construction moves into the existing protocol. |
| Modules.Projects `ProjectDefinitionOperationStore.cs`, `ProjectDefinitionReceiptReader.cs`, `ProjectDefinitionWriter.cs` | One-statement lookup joins new metadata with fixed Projects pool/version; map neutral admission failures without changing receipt/outcome knowledge semantics. |
| Modules.Projects `ProjectsModuleServiceCollectionExtensions.cs` | Register its concrete retention participant. Remove old epoch service/hosted-worker registration. Preserve existing Projects options and payload-limit validation. |
| Modules.Projects `ProjectDefinitionOperationMaintenance.cs`, `ProjectOperationMaintenanceResult.cs`, `ProjectOperationMaintenanceMetrics.cs` | Superseded by neutral state/maintenance and one Composition worker; delete after all source/test/harness callers move. Do not retain two running loops. |
| App/Composition `FiniteOperationComposition.cs` (new), `RuntimeHostServiceCollectionExtensions.cs` | Explicit fixed registrations and one `FiniteOperationMaintenance` hosted service. No auto-discovery of durable families. |
| App/Composition `DatasetActivationCoordinator.cs` | Profile-bound neutral options/store; explicit initialization versus validation over the compiled pool list. See activation rules below. |
| App/Web `Infrastructure/DatasetActivationCommand.cs` | Register the same installed-pool descriptors for offline coordinator construction, without starting the worker or restoring unsupported offline services. Existing stopped-writers acknowledgement remains. |
| Modules.Projects `ProjectTransferReferenceQuery.cs` | Remove the two neutral entity types from its owner table plan; preserve receipt and the other seven owner types. No replacement neutral-table locks inside the business target plan. |
| Foundation/Migrations.PostgreSql new migration + designer + canonical snapshot | The lossless metadata rename/key migration below. Historical migrations stay immutable. |

Exact existing anchors: `ProjectOperationEpochService.EnterAsync` currently queries `ProjectsDbContext.Set<ProjectOperationEpochRecord>`; the neutral owner-entry path must use scalar SQL on the **supplied owner context/connection**, not require neutral entities in every owner model. `ProjectDefinitionWriter` enters coordinated Search only around its enlisted projection write; do not assume a coordinated frame already exists during early epoch admission. Activation and maintenance can create neutral contexts through `CoordinatedDatabaseTransaction.CreateEnlistedAsync`; owner admission can execute its bounded scalar queries directly on the already-owned connection.

The canonical model already collects Infrastructure configurations through `AppDbContext`; do not add a second migration authority. No change to `Projects.Contracts/ProjectOperationEpoch`, operation JSON strings, operation IDs, caller hashes, codec limits, accepted payload bytes or API/SPA generated types follows from table extraction.

## Typed APIs and failure meaning

`FiniteOperationPoolRegistration` is immutable: Pool, positive ProtocolVersion, validated Timing, MaintenanceInterval/WarningAfter and an immutable nonempty array of `(Family, MaximumReceiptPayloadBytes)`. C2a's factory maps existing `ProjectDefinitionOperationOptions` fields exactly. Preserve current validation/ranges, existing issuance conversion to integer milliseconds/seconds and default cadence 60 seconds; do not clamp or reinterpret persisted epochs. Shared fixed bounds remain two seconds aggregate pool work, 64 receipt batches, 128 rows/epoch candidates and 16 MiB per receipt batch. Every supported family's maximum payload must fit one batch.

`FiniteOperationEpochSnapshot` contains exact activation binding, Pool, ProtocolVersion, positive Epoch, Open/Closed, OpenedAtUtc, AdmissionClosesAtUtc, ClosedAtUtc, issued deadline/retention and database ObservedAtUtc. It is data, not authority to skip entry guards. Require constructor validation; no mutable EF entity escapes the store. `FiniteOperationEpochAddress` is `(Namespace, Pool, Epoch)`.

`FiniteOperationEpochStore` has the following concrete methods (cancellation token required):

```text
InitializeNewNamespaceEnlistedAsync(binding, pool, ct) -> Task
RequireReadyEnlistedAsync(binding, pool, ct) -> Task
CloseForReactivationEnlistedAsync(previous, current, pool, ct) -> Task
ReadCurrentAsync(binding, pool, ct) -> Task<FiniteOperationEpochSnapshot>
EnterAsync(DbContext owner, binding, pool, originalEpoch, ct) -> Task<FiniteOperationEpochSnapshot>
RequireAdmissibleAsync(DbContext owner, snapshot, ct) -> Task
ReadTimeAsync(DbContext owner, ct) -> Task<DateTimeOffset>
RotateExpiredAsync(binding, pool, ct) -> Task<FiniteOperationRotationResult>
AdvanceRetirementAsync(binding, pool, ct) -> Task<FiniteOperationRetirementResult>
```

The first three require the existing same-profile coordinated transaction; they neither commit nor create an independent connection. New namespace initialization is idempotent only for the exact supported existing state/current epoch after a saved plan's SQL commit; it never resets counters/timings. `EnterAsync`/`RequireAdmissibleAsync` require a real same-profile PostgreSQL owner transaction, preserve the caller's RC/Serializable isolation, and never commit it. Other methods own short RC metadata transactions or independent read-only metadata reads. ReadCurrent is not a receipt lookup. All temporal decisions use database time.

Admission failures are `UnknownFutureEpoch`, `RetiredEpoch`, `ClosedEpoch`, `AdmissionDeadlineElapsed`. A missing row at/below a known retained current epoch is **not** Unknown: it is protocol corruption. Preserve `DatasetActivationMismatchException` for binding mismatch. New `FiniteOperationProtocolException` carries a closed enum (`MissingState`, `MissingRetainedEpoch`, `UnsupportedProtocol`, `InconsistentMetadata`, `InvalidRegistration`, `InvalidParticipantResponse`) and fixed pool/version identifiers, not payloads. Programmer misuse/unsupported provider remains explicit Argument/InvalidOperation/NotSupported exceptions. Rotation is `Unchanged`, `Rotated`, `Busy`; retirement preserves `Unchanged`, `Advanced`, `Busy` plus watermark. No lock miss becomes Empty.

Projects maps future/retired/closed/deadline admission refusals to its existing Unknown/Expired/Closed codes after the existing fresh outcome-recovery attempt. Protocol corruption/unavailability is never authorization denial, ClosedAbsent or UnknownFuture. Read metadata/lookup fails explicitly; a known precommit write keeps its existing transaction-failure rejection, while any commit-attempted unresolved result remains Unconfirmed. No new HTTP response schema is introduced in C2a. Known accepted/replayed evidence continues to win; a new attempt rejection does not erase a caller's earlier uncertainty.

The only polymorphic persistence port is `IFiniteOperationReceiptRetentionParticipant`, with Pool/Family and two methods:

```text
PurgeRetiredBatchEnlistedAsync(FiniteReceiptPurgeRequest, ct)
    -> Task<FiniteReceiptPurgeObservation>
FindNonemptyEpochsEnlistedAsync(FiniteEpochInspectionRequest, ct)
    -> Task<IReadOnlyList<FiniteOperationEpochAddress>>
```

Purge request: exact current activation binding, pool registration, this family, row/byte caps. Observation: staged deleted rows/bytes, Unknown/NoneObserved/Present, database ObservedAtUtc and optional observed eligibility timestamp. Inspection request: the same binding/pool/family and a copied unique array of at most 128 candidate addresses. It returns only nonempty members of that array; duplicate/foreign/unknown responses fail closed. Exceptions are unknown evidence. No receipt payload, serializer, effect callback, authorization policy or arbitrary table identifier crosses this interface.

Composition registers one immutable Projects pool descriptor and one singleton Projects participant through explicit calls. The singleton maintenance service pairs actual participants with each registration's closed family list, rejects duplicates/unknown/missing families, and does not infer empty families from DI absence. `AppDatabaseBootstrapper` explicitly depends on that service, so its bounded constructor checks complete before bootstrap SQL or serving writes. The actual registration test resolves the bootstrapper with `ValidateOnBuild=true` and `ValidateScopes=true` for valid, missing and duplicate participants. Offline stopped-host activation uses the same compiled descriptors without resolving owner mutation services.

The singleton lifetime is a reviewed constraint: both services hold immutable pooled context options/registration arrays and the existing singleton `CoordinatedDatabaseTransaction`; the canonical database profile is fixed for this host process. Each operation creates and disposes its own context, and enlistment is held in the coordinator's existing `AsyncLocal` frame. Neither service stores a live context, connection, transaction or mutable per-operation state. Constructors perform no queries or provider activation. A real PostgreSQL arrangement calls the same Projects participant concurrently through the same maintenance instance and verifies distinct enlisted transactions and exact combined committed deletion counts. Future participants must prove these same lifetime/thread-safety requirements before registration. The epoch store remains scoped. The current extraction has exactly one pool/one family; multi-pool tests use test-only metadata and small test-owned receipt tables, not early Workbench or Workspace production receipts.

## Predicates and transaction boundaries

Define `Active(binding)` in every relevant SQL statement as lifecycle Id=1, exact NamespaceId/MaintenanceId/SchemaVersion/GuardProtocol, State=Active. Define `Pool(state)` as exact PoolId and exact expected RetentionProtocolVersion. Numeric epochs/pool IDs are parameters, not SQL fragments.

- Owner entry installs finite transaction/statement/lock deadlines **before** its immutable-epoch/database-clock precheck. Reject expired arrivals before acquiring lifecycle SHARE. Then lifecycle SHARE, original pool epoch SHARE, operation guard, source guards, sorted P, final deadline check. No state-row lock in foreground admission. An old Serializable snapshot must reject safely (including 40001); never assume advisory/table waits refresh it.
- Entry queries the state/watermark/version and requested epoch coherently before interpreting missing metadata. The locked epoch query repeats the exact pool/version and active binding predicates. State/current/watermark results are not substituted across separate snapshots to assert absence. Future/retired failures and metadata corruption remain distinguishable.
- Rotation retains read-only exact-binding/deadline precheck, lifecycle SHARE, state UPDATE SKIP LOCKED, then expired current epoch UPDATE SKIP LOCKED. Recheck after waits; close and increment/create atomically, state before epoch. Every update includes fixed pool/version.
- Standalone retirement keeps state UPDATE SKIP LOCKED, bounded contiguous Closed prefix and conditional monotonic watermark; no lifecycle SHARE or epoch UPDATE. For every candidate, both `AdmissionClosesAtUtc + issued mutation deadline + issued retention` and `ClosedAtUtc + issued retention` must have elapsed.
- Family purge owns one short RC transaction, `transactions.Enter(neutralOwner)`, then participant enlistment on that exact connection. Preserve the current bounded candidate CTE, row SKIP LOCKED and cumulative `octet_length(AcceptedPayload)` byte budget; add fixed pool/version joins. Repeat Active in the DELETE. Eligibility observation is a **later SQL statement** in the same transaction, never the DELETE CTE snapshot. Commit before publishing counts. A later transaction/context disposal failure carries the exact confirmed batch; a commit exception confirms no invented count.

Eligibility for purge/metadata candidates is explicit Closed state plus either active namespace epoch <= committed watermark, or inactive namespace with both persisted horizons elapsed. All state joins include the exact pool/version. Receipt family SQL knows its table; Infrastructure never accepts owner table names. An unsupported protocol must be observed as an error before cleanup, not filtered out and reported empty.

Metadata cleanup runs only after releasing family purge transactions. Capture at most 128 eligible epoch keys. Inspect each registered family in its own short RC enlisted transaction and release it before the next family. Combine the intersection proven empty. In a **separate neutral-only RC transaction**, delete only those exact candidate keys with repeated Active, pool/version, Closed state and eligibility predicates. Bound and delete inactive state rows only after their epochs are gone; active state/watermark always remains. A concurrent worker's earlier removal is idempotent zero work, not protocol corruption for these already captured inactive cleanup candidates. Changed binding/protocol, missing family or failed/unknown inspection aborts metadata cleanup and retains known receipt counts.

Cross-statement emptiness is monotonic only because supported effect/receipt writers retain epoch SHARE until their common commit; closure waits; no receipt insert is allowed into Closed epochs. Imports never insert receipts and restore requires stopped writers/new namespace. No generic `SaveChanges` hook is introduced. Each future family must prove this insertion perimeter before registration. User receipt lookup stays one MVCC SQL statement joining lifecycle, state/pool/version, epoch, exact caller receipt and live-target disclosure facts. Its returned protocol version is validated before Expired/future/absence interpretation; do not hide mismatch in a LEFT JOIN that merely looks absent.

### No hidden cross-owner locks

Current `ProjectDefinitionOperationRecords.cs` and migration `20261016163615_AddProjectDefinitionOperationLifecycle` define no FK on either metadata table or `Projects_DefinitionReceipts`, and no trigger. Keep that deliberate shape: **no FK in either direction between neutral metadata and owner receipts, no cascade, no user trigger on neutral metadata**. Do not add even a restrictive receipt-to-epoch FK: its RI trigger can reacquire an owner table when metadata is deleted. Neutral metadata has no navigation/FK at all in C2a; final epoch/state DELETE references only lifecycle and the two neutral tables.

Migration preflight rejects unexpected FKs touching the two old metadata tables or user triggers on them. The canonical model test checks both incoming and outgoing relationships; the real migrated-schema test inspects `pg_constraint` and `pg_trigger`, excluding PostgreSQL internal triggers only when no corresponding FK touches metadata. This is an integrity/lock-order gate, not a perpetual foreground catalogue query. Out-of-band triggers/receipt insertion are unsupported schema mutations. Integrity comes from the checked owner insertion perimeter, migration correspondence checks, exact readiness/version checks and retained state; no cascade may erase promised history.

Existing transfer still acquires lifecycle utility EXCLUSIVE before its first Serializable snapshot, complete sorted target-table locks, then exact captured binding validation. Project receipts remain in the target plan/residue rules. Neutral pool initialization alone does not make an empty business target nonempty. Metadata maintenance intentionally has no lifecycle SHARE; releasing each family transaction prevents the import A-then-B/purge-B-then-read-A cycle. Final deletion cannot reintroduce it through constraints. Database migrations/activation remain stopped-host control-plane work.

## Lossless migration and rollback refusal

Generate ordinary migration `ExtractFiniteOperationEpochMetadata` only after source release; EF supplies the timestamp then. Predecessor includes `20261016163615_AddProjectDefinitionOperationLifecycle` and any subsequently committed migrations. Never edit that historical migration/designer or pretend fresh install no longer needs it: a fresh database runs it and then this extraction. Remove old types only from the **current** model/snapshot and current owner tests.

Run in one PostgreSQL transaction with all writers/maintenance hosts stopped. Preflight before DDL:

1. No unexpected metadata FK/user trigger. Receipt table and both old control tables have their expected keys/checks and columns.
2. Every epoch has a namespace state and `0 < Epoch <= CurrentEpoch`; every receipt has its state and exact epoch. No decoding of private payloads.
3. If an Active lifecycle binding exists, its state exists, current epoch is Open and unique, every unretired earlier epoch is present/Closed. Prove the latter by bounded aggregate/count/min/max against `CurrentEpoch - RetiredThroughEpoch - 1`, without generating a potentially enormous number series. Retired prefix gaps are legal after purge.
4. Inactive namespaces have no Open epoch. Their current epoch may already have been purged while older promised epochs remain; do not incorrectly require an inactive current row. Missing retired inactive epochs are legal, orphan receipts are not. A fresh empty lifecycle/metadata/receipt set is valid. Existing non-Active lifecycle state refuses the cutover for explicit recovery.

Core DDL/data mapping, with names fixed in the migration (not supplied by user input):

```sql
ALTER TABLE "Projects_OperationState" RENAME TO "Infrastructure_FiniteOperationState";
ALTER TABLE "Projects_OperationEpochs" RENAME TO "Infrastructure_FiniteOperationEpochs";
ALTER TABLE "Infrastructure_FiniteOperationState" ADD COLUMN "PoolId" uuid;
ALTER TABLE "Infrastructure_FiniteOperationState" ADD COLUMN "RetentionProtocolVersion" integer;
ALTER TABLE "Infrastructure_FiniteOperationEpochs" ADD COLUMN "PoolId" uuid;
UPDATE "Infrastructure_FiniteOperationState"
SET "PoolId" = 'e6eaf1c1-5dbc-46bf-8641-1ae9fe3ae376', "RetentionProtocolVersion" = 1;
UPDATE "Infrastructure_FiniteOperationEpochs"
SET "PoolId" = 'e6eaf1c1-5dbc-46bf-8641-1ae9fe3ae376';
ALTER TABLE "Infrastructure_FiniteOperationState" ALTER COLUMN "PoolId" SET NOT NULL;
ALTER TABLE "Infrastructure_FiniteOperationState" ALTER COLUMN "RetentionProtocolVersion" SET NOT NULL;
ALTER TABLE "Infrastructure_FiniteOperationEpochs" ALTER COLUMN "PoolId" SET NOT NULL;
ALTER TABLE "Infrastructure_FiniteOperationState" DROP CONSTRAINT "PK_Projects_OperationState";
ALTER TABLE "Infrastructure_FiniteOperationEpochs" DROP CONSTRAINT "PK_Projects_OperationEpochs";
ALTER TABLE "Infrastructure_FiniteOperationState" ADD CONSTRAINT "PK_Infrastructure_FiniteOperationState"
PRIMARY KEY ("NamespaceId", "PoolId");
ALTER TABLE "Infrastructure_FiniteOperationEpochs" ADD CONSTRAINT "PK_Infrastructure_FiniteOperationEpochs"
PRIMARY KEY ("NamespaceId", "PoolId", "Epoch");
DROP INDEX "IX_Projects_OperationEpochs_NamespaceId";
CREATE UNIQUE INDEX "IX_Infrastructure_FiniteOperationEpochs_NamespaceId_PoolId"
ON "Infrastructure_FiniteOperationEpochs" ("NamespaceId", "PoolId") WHERE "State" = 1;
```

Rename the six existing check constraints into the corresponding neutral table naming domain, preserving their expressions; add explicit nonempty PoolId checks to both tables and positive RetentionProtocolVersion on state. Do not leave insertion defaults for PoolId/version: each writer must supply its registered identity. Preserve namespace, current/retired counters, all timestamps and issued intervals exactly. No new receipt index/table/column/FK; do not UPDATE, reserialize, rehash, delete or export accepted receipts. No lifecycle namespace/maintenance ID rotation. DDL rebuilding the small control-table indexes is the intended cost; report actual retained row counts and migration time in the isolated fixture before deployment.

Postflight repeats key/receipt correspondence and the expected new schema/constraint audit before commit. Retained-data proof compares exact old/new tuples and receipt bytes in active and historical namespaces; private payloads stay inside assertions, never diagnostic output. Migration failure rolls all DDL/data changes back. The generated idempotent script uses normal migration-history guards.

`Down` is an explicit unconditional refusal (`RAISE EXCEPTION` SQLSTATE P0001 with a fixed no-private-data message) before any DDL. P0001 is deliberately nontransient: the operator must resolve the incompatibility rather than retry the migration. This avoids pretending an old binary can understand a later installed pool/family or its issued promises. No reverse drop, compatibility view, dual-write path or automatic activation reset. Operational rollback is a separately authorized stopped-host restore of a matching backup/binary plus the existing managed restore/reactivation procedure; it is not a generated destructive Down script. Old binaries reference removed tables and cannot continue admission after this stopped-host cutover.

## Activation and maintenance orchestration

`DatasetActivationCoordinator.CreateStores` configures `FiniteOperationDbContext` for the exact target profile and the same ForProfile coordinator. Matching Active `EnsureReadyAsync`, already-Active Attach/Resume, new Attach and superseded-bootstrap Attach all call RequireReady for every installed pool. Missing state/current/protocol refuses without writing epoch 1 or marking a Prepared file Active. Expired Open is structurally ready; admission metadata remains unavailable until normal rotation.

Bootstrap/Reactivate's prepared plan is persisted before SQL as now. In the existing lifecycle-owner transaction, StageActivation first; Reactivate closes every previously installed pool for the previous namespace using database time; initialize each pool for the new namespace. Only acknowledged SQL commit permits MarkActive. Exact Prepared resume preserves IDs and existing issued values. No file lock enters normal SQL mutation transactions. Missing previously issued pool state must fail even on Reactivate; a migration installing a new pool explicitly distinguishes its absence in pre-install historical namespaces.

One Composition worker replaces the Projects loop. Move existing health/accounting logic rather than duplicate it. One fixed cursor per registered pool/family advances before await, survives budget exhaustion and never grows with operations. A due pool gets one cycle before a due peer receives another. A family with zero deletion/Present is contended for that cycle; no spin. A family fault preserves confirmed counts and permits peer scheduling within remaining budget. Metadata inspections/final cleanup count against the same two-second aggregate pool budget. Successful Empty across every required family, non-Busy retirement and no failed/unknown phase are required for Recovery. Rotation/retirement contention cannot masquerade as an empty backlog.

Use a neutral meter `CanDoItAll.FiniteOperations.Maintenance`, instrument prefix `candoitall.finite_operations.maintenance`, preserving existing count/byte/duration/observed-age meanings. Add only fixed pool/family labels and scheduled lateness; no namespace, operation, caller, exception text or payload metric labels. Document the intentional old Projects meter-name replacement in the validation/deployment record; do not double-publish counters. Logs retain sanitized failure type/SQLSTATE, pool/family and confirmed totals. No claim of globally oldest backlog, fair service within one selected eligible epoch or guaranteed throughput.

## Validation inventory and release order

These **current source** rows were counted from the named files' Fact/Theory/InlineData attributes; none uses MemberData/ClassData. They are expectations, not discovery/execution results for C2.

| Current selection | Expected rows |
| --- | ---: |
| Unit `ProjectOperationMaintenanceTests` (4 Facts + 8 inline rows) | 12 |
| Unit `ProjectsWorkbenchDbContextTests` | 2 |
| Unit `ProjectTransferTargetStateArchitectureTests` | 3 |
| Unit `AppDbContextMigrationAuthorityTests` (2 Facts + 5 inline rows) | 7 |
| Integration `ProjectOperationEpochTests` (10 Facts + 2 inline rows) | 12 |
| Integration `ProjectOperationMaintenanceTests` (6 Facts + 4 inline rows) | 10 |
| Integration `DatasetActivationCoordinatorTests` | 5 |
| Integration `DatabaseTransferLifecycleTests` (1 Fact + 6 inline rows) | 7 |
| Integration `ProjectDefinitionOperationTests` across its three files (8 Facts + 15 inline rows) | 23 |
| Integration `DatasetActivationCommandProcessTests` | 2 |
| Integration `ProjectsMaintenanceCapacityAccountingTests` | 2 |
| Existing focused total | **85 = 24 Unit + 61 Integration** |
| Separate LongRunning `ProjectsProtectedMutationPerformanceTests` / `ProjectsMaintenanceCapacityTests` | **2 + 4**, excluded from ordinary focused selection |

Mechanically move epoch/maintenance test fixtures and fault interceptors to neutral metadata contexts; retain owner receipt contexts. Tests injecting a failure after the first epoch flush must still exercise the actual rotation transaction. Update capacity SQL seed/count/physical accounting and the protected-operation probe to the renamed metadata tables plus fixed pool/version; preserve workload version and exact receipts. Historical migration test targets remain historical. Current Projects model expects exactly seven owner types; add the exact two-type neutral model assertion and no incoming/outgoing FK audit. Do not count moved tests as new tests.

Proposed additions below are **prospective**; derive exact source counts after implementation and compare runner discovery before execution. Prefer one Fact per listed scenario with bounded internal cases where they share one invariant:

- **8 Unit Facts:** nonempty IDs/immutable registration; duplicate/missing/unsupported family registration; byte cap fits each declared receipt; participant response subset/duplicate validation; pool cursor advances before a fault and peer gets its turn; family cursor persists after budget/contended batch; unknown peer/Busy retirement cannot clear health; neutral model has no owner entities/FKs/triggers and Projects registration owns exactly definition family.
- **16 PostgreSQL Facts:** (1) lossless all-namespace migration plus exact original replay; (2) orphan receipt/state preflight rolls back DDL; (3) unsupported metadata FK/trigger refuses and ordinary migrated model has none; (4) fresh migration/bootstrap and Down refusal; (5) matching Active missing state/current refuses across EnsureReady/Attach/Resume; (6) exact Prepared Bootstrap/Reactivate resume preserves counters/promises; (7) explicit new-pool installation records original timings and ordinary readiness never installs it; (8) separate pools admit concurrently while same-pool address exclusion remains; (9) each family nonempty/Unknown retains epoch metadata; (10) actual import A-then-B versus family-B purge/inspection completes without inversion; (11) concurrent cleanup workers remove only all-family empty closed keys; (12) binding/protocol change between inspection and final DELETE retains metadata; (13) post-close owner admission cannot insert after empty inspection; (14) confirmed family purge totals survive later family/metadata disposal failure; (15) one-statement Projects lookup across retirement/purge/reactivation never returns false ClosedAbsent; (16) mixed-family budget/cursor continuation and bounded rows/bytes, with a contended family not preventing a peer's next cycle.

New-pool/family test participants are isolated test-owned receipts created through the existing lease; they do not add production Workbench/Workspace tables. Test 10 uses actual table locks/barriers and supported bounded deadlines, not timing-only assertions. Assert canonical migrated constraints through EF and PostgreSQL catalogs. Use no new fixture/container and no production reflection/test-only hook.

Source preparation may proceed after the parent-reviewed C1 source checkpoint. Do not integrate C2 until C1 runtime proof and final static enforcement pass. Then inspect the generated migration/idempotent SQL, perform direct affected standard Release builds, compare source counts with discovery, run focused correctness and the static gate, then serialize matched Projects measurements. Only after that checkpoint add the Workbench pool/receipt card and its actual effect/disclosure proofs. Existing Projects-only capacity does not qualify multi-pool scheduling: run bounded mixed foreground/maintenance arrivals, forced family contention, due-pool lateness, WAL/index/TOAST and cleanup recovery before enabling a second owner. Default cadence stays 60 seconds; capacity and retained production-distribution preflight remain open gates.

## Source-preparation coordination

The independent first edits are the new neutral contracts, metadata model/context and store under Infrastructure. They are not registered or used by a live caller until the complete owner/composition change is reviewed. Parent owns migration generation after the current model is frozen; historical migrations are immutable. H2's `ProjectDefinitionOperationProtocol`, `ProjectMutationAuthority`, `ProjectDefinitionReceiptReader`, `ProjectDefinitionWriter` and offline command must be explicitly handed over before edits to those shared files. C1 files remain untouched.

The current-source caller audit also found mechanical adaptations outside the original named proof table: the Projects maintenance test host, capacity storage/probe, `SharedProviderPremergeUpgradeTests`' exact canonical inventory, and `ProjectStructureProjectCreatePendingTests`' hosted-service fixture removal. Move those references with the final composition cutover; do not preserve a second worker or change receipt wire/payload behavior to avoid fixture edits. Their source counts must be rechecked before the parent chooses the complete validation filter.
