# Historical rollback fixture scope after C2

Status: source prepared in the managed engine checkout; no build, test, database command or portability scan executed for this repair. Preserve the parent's original `transfer18` result (16 passed, 2 failed). This note does not replace that evidence.

The new finite-pool migration deliberately refuses `Down`. A historical test that first migrates to latest can no longer reach an older migration's rollback. A generic PostgreSQL `RaiseException` assertion can also pass on the new guard while failing to exercise the intended historical protection. The repair selects the historical subject before the rollback and asserts both the exact applied head and the relevant guard message. No production migration, rollback restriction, coverage checker or model was changed.

## Scope and seed compatibility

| Test class | Historical arrangement | Existing full-class rows | Directly affected rows |
|---|---|---:|---:|
| `LlmChatReceiptMigrationAndTransferIntegrationTests` | Unmigrated lease → `20260910163827_AddLlmChatDefinitionCreateReceipts`; explicit subject reapply and later latest upgrade | 8 | 2 |
| `CrmProjectionMigrationIntegrationTests` | Exact `20260910213323_AddCrmTechnicalProjectionProvenance` before rollback | 4 | 3 |
| `ProjectLifetimeMigrationIntegrationTests` | Exact `20260910183745_AddProjectLifetimes`; historical retirement insert includes only its three original columns | 6 | 3 |
| `ProjectExternalIdentityPersistenceTests` | Exact `20261007193102_AddProjectExternalIdentity`; project projection and two-field update avoid querying the later concurrency column | 10 | 1 |
| `WorkflowReceiptMigrationIntegrationTests` | Exact receipt subject for the first roundtrip; explicit pre-C2 seed boundary for current owner record shapes before the existing historical downgrade | 6 | 6 |
| `SourceBoundExecutionAdmissionMigrationTests` | Explicit pre-C2 seed boundary, then existing owner-lifetime/source-bound subject and guard | 10 | 7 |
| `WorkAssignmentMigrationIntegrationTests` | Explicit pre-C2 seed boundary, then existing owner-lifetime/assignment subject and guard | 10 | 2 |
| Total | 26 Facts plus 28 inline theory rows; no member/class providers or new rows | **54** | **24** |

The seed boundary is `20261016163615_AddProjectDefinitionOperationLifecycle`, an explicit reviewed historical endpoint before C2. These three classes use later current owner columns (including retained/imported history) and therefore cannot safely seed directly at their earliest subject migration. They still downgrade to and assert their original subject before testing its guard. Forward-to-latest assertions use the complete compiled migration chain, rather than mistaking the seed boundary for the current head.

Historical guards remain specific: Simple Chats creation receipts, CRM technical projection provenance, project lifetime/access-recovery evidence, assigned external identities, Workflow/Storage evidence, retained source-bound evidence, retained owner lifetimes and conflicting assignment identities. Existing data/history and constraint checks remain. Current-model parity is checked after deliberate final upgrades, not inferred from a historical database's applied-head state.

`WorkflowReceiptMigrationIntegrationTests` also compared legacy project JSON with newly introduced `ConcurrencyToken`. Its business-data projection now excludes only that generated revision, while the upgrade test separately requires a nonempty revision and exact stability across two reopened providers. The current model assertion is 168 entities with explicit dataset-lifecycle, neutral-state, neutral-epoch and definition-receipt table mappings. The 168 value was independently counted from distinct entity declarations in the parent's current generated model snapshot and agrees with the reviewed canonical provider-upgrade test. Historical subject assertions are unchanged.

## Read-only audit perimeter

Reviewed targeted `MigrateAsync` calls across the test source, then distinguished upgrade-only arrangements and local migration helper calls from actual historical rollback paths. `OwnerLifetimeHistoryMigrationTests` and `WorkflowProviderDisclosureMigrationTests` already select their exact subject by migration suffix; `ProcessPlanMigrationIntegrationTests` uses an explicit previous/hash-versioning/current/previous sequence. They need no C2 rollback repair.

The following arrangements start from a fresh lease and upgrade forward, select a historical schema only as a fresh fixture baseline, or call a helper that always migrates latest: `DatabaseMigrationIntegrationTests`, `MergeTargetSchemaUpgradeIntegrationTests`, `MigrationAuthorityIntegrationTests` (including owned-host setup), `HistoryPersistenceTestDatabase`, historical Simple Chat title/evidence upgrade fixtures, `ProcessAuthoringStoreTests`, `ProcessPredecessorRecoveryTests`, `ProviderUsageWindowPostgreSqlTests`, `SharedProviderPersistenceIntegrationTests`, `SharedProviderPremergeUpgradeTests`, `WorkAssignmentCoherencePersistenceTests`, `ProjectDefinitionMigrationTests` and `WorkspaceSettingsMigrationTests`. No additional latest-to-historical rollback repair was identified there. This is source evidence, not execution of those fixtures.

The parent's new finite-operation migration tests and HTTP OpenAPI resolver repair remain separately owned. Neither is modified by this slice.

## Validation handoff

The seven class names in the table form an OR `FullyQualifiedName~...` filter for **54 expected discovery/runtime cases**, independently counted from source attributes. The first repeat may run the two original failing Simple Chat methods; the full affected-class lane remains 54. No test was deleted, skipped, renamed or given a longer timeout.

All database leases, temporary paths and service providers still use their existing fixture disposal. This authoring pass created no runtime resources. Parent must run refreshed build-backed discovery, the chosen PostgreSQL lane and the required final portability-static enforcement. The seven-source-plus-note hashes are recorded in `validation/P04-C2-historical-rollback-source-inventory.json`; a clean scoped `git diff --check` is only a whitespace check, not runtime or static-gate proof.
