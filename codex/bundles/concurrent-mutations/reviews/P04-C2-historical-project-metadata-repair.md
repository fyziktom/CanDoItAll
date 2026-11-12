# Historical project payload and current metadata repair

Status: source prepared in the managed checkout; no build, test, database command or portability scan run by this author. The original 54-row execution remains **47 passed, 7 failed, 0 skipped**. Its retained TRX is `C:/repositories/CanDoItAll/.artifacts/concurrent-mutations/validation/c2-repair-validation/historical-migration-guards.trx`, SHA-256 `C17FBD4DB065BF81DFCD71DD8A05F564083309A839B37818B08FC2570327E510`.

Six failures compare the project entry in an otherwise retained JSON collection: the three upgrade/roundtrip/backfill Facts in each of `SourceBoundExecutionAdmissionMigrationTests` and `WorkAssignmentMigrationIntegrationTests`. The truncated xUnit messages identify that entry but do not expose a complete field diff. Source inspection identifies the incompatible schema boundary: `20261016151626_AddProjectDefinitionConcurrency` adds the nonnullable `Projects_Projects.ConcurrencyToken` with `gen_random_uuid()`, and its `Down` removes exactly that column. The early historical subjects precede it; the reviewed seed endpoint and latest schema include it. The seventh failure is the remaining `164` current-model assertion in the empty Workflow receipt roundtrip. Four more `164` assertions in the other two classes are masked by the earlier payload failures.

The repair changes only the project JSON projection in those two classes to subtract **`ConcurrencyToken` alone**. `to_jsonb(row)` still compares every original project field, including the original identity/lifetime, scalar values, timestamps and existing external-identity normalization. No field whitelist or general metadata filter replaces that comparison. Other owner payloads, human edits, orphan/duplicate rows, exact historical migration heads, specific guard messages, retained history and rollback assertions remain unchanged.

`HistoricalProjectMigrationAssertions` is a small test-only helper shared by these three classes. It requires a nonempty stored project revision, exact equality when a retained revision is supplied, the current model total of **168**, and the named dataset lifecycle, finite state, finite epoch and definition receipt entity/table mappings. The main checkout's generated model snapshot independently contains 168 distinct entity declarations and all four names (snapshot SHA-256 `B78FAEF2D168FCBC6D2E319697177CEB98D2F48004D524D4442031316E5A7E58`). The managed checkout's earlier generated snapshot is not the current candidate; this repair does not modify generated migration files.

The two upgrade Facts capture the first new revision and require it unchanged through both reopened providers. The backfill Facts require a revision at the explicit seed endpoint, retain the exact historical rollback proof, and capture a new nonempty revision after the deliberate downgrade/reupgrade that removes and recreates its column. They require that new value to survive repeated migration readiness. The other roundtrip Facts likewise require the newly introduced revision to survive repeat migration. They do not compare a regenerated revision with a value whose column was legitimately removed. Workflow's existing restart assertions now use the same helper; its previously missed empty roundtrip receives the identical current-metadata checks.

## Validation handoff

| Scope | Facts | Inline rows | Expected rows |
| --- | ---: | ---: | ---: |
| Source-bound admission class | 4 | 6 | 10 |
| Work assignment class | 5 | 5 | 10 |
| Workflow receipt class | 2 | 4 | 6 |
| These three classes | 11 | 15 | **26** |
| Original seven-class historical lane | 26 | 28 | **54** |

No row was added, removed, skipped or renamed. The three-class filter is `FullyQualifiedName~SourceBoundExecutionAdmissionMigrationTests|FullyQualifiedName~WorkAssignmentMigrationIntegrationTests|FullyQualifiedName~WorkflowReceiptMigrationIntegrationTests`. Eight Facts have changed source: the seven original failures plus Workflow's already-passing upgrade Fact, which now calls the shared helper. Parent may validate that eight-Fact delta first; the complete affected-class count is 26 and the retained historical lane remains 54.

The final source was parsed with the already-loaded Roslyn parser (zero syntax errors in the four C# files) and its scoped whitespace diff was checked. These checks are not compilation, execution or portability-static proof. The helper is included in `validation/P04-C2-historical-project-metadata-source-inventory.json`. Parent owns refreshed build-backed discovery, the PostgreSQL lane and final portability enforcement. Existing fixture disposal remains unchanged; this author created no runtime resources.
