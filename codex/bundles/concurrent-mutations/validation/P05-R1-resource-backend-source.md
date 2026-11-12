# Resource R1 backend source checkpoint

Status: authored in the managed continuation checkout; unbuilt and unexecuted. This
implements the approved [R1 readiness card](../plan/P05-R1-resource-implementation-readiness.md)
and [original-state design](../plan/P05-R1-resource-original-state.md). It does not close
the migration, retained-data compatibility, runtime, browser, static or capacity gates.
The parent owns those serialized validations and the canonical migration.

## Owner boundary

Registry save/delete and governed storage-object promotion now share one Resource-owned
writer. Commands retain an immutable original profile/namespace, candidate or existing
identity, revision and project binding. Raw fields and configuration are bounded before
copying. Connector normalization occurs on a private copy before SQL; the installed
key/schema and actual legacy-kind mapping are captured into immutable dictionaries.
Fresh stored-row eligibility uses those dictionaries and bounded JSON parsing, with no
plugin invocation, file access or provider call under SQL exclusion.

The PostgreSQL writer uses ReadCommitted, exact owner-profile enrollment, lifecycle SHARE,
then sorted original-project/target-project/Resource keys. It reads the current row after
waiting, compares its exact original revision and binding, checks the current target
Project admission, and writes Resource plus Search in the same transaction. The final
canonical profile/generation check follows all awaited staging. The owner has finite
transaction, statement and lock deadlines; it does not use the process-global mutation
gate or an InMemory runtime fallback.

The conditional read SQL bounds all seven payload columns and their bigint aggregate
before returning any text. Missing is explicit. Unsupported, invalid or oversized rows
remain observable as identity/length metadata; deletion reads only the header and Search
key. RetainedProject and RetainedUnbound preserve cleanup without inventing a current
Project lifetime. Replacing a stored governed connector is refused even when the new
proposal names an ordinary connector. Promotion checks at most two candidate IDs under
the target Project key, refuses duplicates and returns the exact reused snapshot without
changing its revision or accepting the proposal as stored metadata.

Search upsert/delete select only the existing key. The new enlisted delete and existing
enlisted upsert share the Resource transaction. Search faults therefore prove no commit;
Activity remains a postcommit diagnostic. Acknowledged acceptance retains the exact
normalized snapshot across cleanup/publication/logging faults. Every unacknowledged
CommitAsync attempt remains Unconfirmed, including a lost acknowledgement followed by a
cleanup concurrency exception. Caught failures before that call are typed no-effect
rejections; early cancellation still propagates. Diagnostics contain identity and failure
types, not exception messages, SQL, locators or secret values. Current reads do not prove
an uncertain operation committed, and no Resource receipt or automatic retry was added.

The former identity-only committed exceptions are removed. ResourceMutationKind remains
in its own Contracts file. The existing mutable Resource editor is preserved as an
editor model; the immutable commands are separate. No new project reference is
introduced. The native source checkpoint is owned separately by the UI peer.

## Model and migration handoff

`ProjectResource.Revision` is an application-generated nonempty Guid concurrency token
with no database default. The model adds its nonempty check and a nonunique
`(ProjectId, ProjectLifetimeId, ConnectorPluginKey)` promotion candidate index. No new
entity/table is added. The parent must generate the canonical migration, backfill each
retained row once with a nonempty distinct revision without changing old fields, and
prove model parity, restart stability and retained rows. The Resource owner model Fact
asserts the exact property/check/index and canonical parity. No migration or snapshot
file is included in this backend inventory.

## Source-derived proof selection

These are expectations from the final attributes and fixed theory rows, not discovery
or runtime evidence. None of these classes has MemberData, ClassData or a skipped row;
the selected test classes have no inherited attributed tests. HostPlatform is retained.
The two ResourceMetadataOwnerTests partials form one class with eighteen Facts.

| Owning project/class | Expected rows |
| --- | ---: |
| Unit: ResourceMetadataContractTests | 8 |
| Unit.Storage: StorageObjectResourceConnectorTests | 10 (6 Facts + 4 inline rows) |
| Unit.Storage: ResourceStorageObjectPromotionTests | 9 (6 Facts + 3 inline rows) |
| Unit.CrmHr: CrmAgentCommittedSaveTests | 5 (1 Fact + 4 inline rows; Search interface adaptation) |
| Integration: ResourceMetadataOwnerTests, both partials | 18 |
| Integration: ResourceTestLabAdmissionIntegrationTests | 10 (4 Facts + 6 inline rows) |
| Integration: OwnerPostcommitPersistenceTests | 16 inline rows |
| Integration: ResourcePromotionOutcomeTests | 13 (3 Facts + 10 inline rows; HostPlatform) |
| Integration.Persistence: ResourceStorageObjectIntegrationTests | 1 (HostPlatform) |
| Integration: ResourcesOwnerPersistenceTests | 3 |
| Integration.External: ConnectorPluginIntegrationTests | 2 |
| Integration.CrmHr: CrmHrCrossModuleIntegrationTests | 2 |
| Integration.External: UnknownConnectorManifestIntegrationTests | 1 (HostPlatform) |
| Integration.ProjectStructure: ProjectsServiceIntegrationTests | 8 (Search interface adaptation) |
| Integration: SearchStorageOwnerPersistenceTests, unchanged regression | 6 (4 Facts + 2 inline rows) |

The backend selection is 32 Unit + 80 Integration = 112 disjoint expected rows, including
the unchanged six-row Search regression. The first 26 rows added in new files comprise
8 Unit and 18 Integration. One of those Integration Facts replaces the former positive
InMemory storage-object writer Fact; the old connector class decreases from 11 to 10.
That moved proof now uses real PostgreSQL, exact stored configuration and reused metadata,
ordinary overwrite refusal, and atomic Search. The net backend addition is 25 rows.

Existing Search-fault arrangements in OwnerPostcommitPersistenceTests retain all sixteen
rows: the four Resource Search/SearchCancellation save/delete rows now assert rollback
after an actual enlisted Search write; the two Resource Activity rows assert exact
acceptance/deletion across restart. The TestLab six postcommit rows remain unchanged.
The two Resource/TestLab denial rows and two cancellation rows retain their original
precommit purpose with the Resource typed result boundary.

ResourceTestLabAdmissionIntegrationTests keeps all ten rows. Its former Resource
postcommit-Search context pause is replaced with an actual enlisted Search barrier,
an independently started Project deletion and positive live `pg_blocking_pids` observation.
Both pending tasks drain after the barrier is released. The postrestart retained lifetime
and absent Search projection are still asserted; the TestLab postcommit path is preserved.

The extra browser fixture adaptations are separate: DataSourcesBrowserTests has one Fact
and Playwright.Flows.CrmHrRegressionTests has one Fact; their helper reads/seeds use the
typed owner contract. They are not included in the 112 ordinary rows. The UI peer's
79 lightweight + 30 monolithic + 2 Resources browser rows remain a separate source
inventory and should not be double-counted here.

## Meaningful new arrangements and remaining gates

The eighteen owner Facts exercise same-original races, explicit absence, original revision
SQL, normalization and no-op revision advancement, stale deletion/rebind, dataset
reactivation, captured mutable-editor changes, fresh reads after a real Project-key wait,
atomic Search rollback, actual server commit/lost acknowledgement/conflicting disposal,
known acceptance after disposal/activity failure, promotion reuse/duplicate refusal,
whole-row raw bounds, encoded expansion, unsupported retained values, header-only unbound
deletion including a large old Search body, final generation retirement, foreign profile,
explicit nonrelational refusal and early cancellation. These are source arrangements only.

The finite byte budgets remain an engineering envelope, not measured retained-data
compatibility. Production distribution preflight must reveal only sizes/statuses and is
still pending. Promotion query/index plans, disjoint-owner contention and maximum-shape
latency/allocation measurements remain required; no capacity claim follows from source.
The parent must build the affected standard Release projects, compare exact discovery,
execute the frozen selections against the owned PostgreSQL fixture, run native/browser
proof, complete the Resource migration proof, and enforce the full portability-static
gate without writing a baseline before this slice can close.
