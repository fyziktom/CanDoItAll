# Durable cancellation queue — 2026-10-22

Behavioral gate: **Pass for the P16 queue substrate**. All **136 distinct selected
cases** qualify, including **34 new cases**. The actual recovery worker is not activated.
The full concurrent-mutations outcome remains open.

## Behavior and boundaries

The runtime transaction stages one durable recovery row when a root first enters its
cancellation barrier. Root state, events, receipt and queue either commit together or
roll back together. Original receipt replay does not enqueue another row. Application
owns typed positions/intents; Persistence owns the queue, strict bounded wire codec and
database-clock leases. Runtime has no queue dependency. Module composition registers the
scoped queue and starts no worker. No project references or runtime partials were added.

A short SKIP LOCKED transaction claims one due root. Checkpoints compare token,
generation, revision and the database clock after obtaining the row lock. Unresolved
intents survive lease expiry and takeover unchanged. Clearing an intent requires its
exact action and a definite outcome. Corrupt evidence is retained, reported by root and
deferred so another root can progress. A bounded legacy seed page and its cursor commit
atomically; new barriers enqueue independently of that historical cursor.

Migration creates the stopped-root index concurrently and accepts an interrupted prior
index only when its actual PostgreSQL definition is exact and valid. Downgrade refuses
retained queue or seed evidence. Actual migration/restart tests preserve legacy rows and
verify model parity. No ordinary application database was migrated for testing.

## Qualification

| Owning class | Qualified cases |
| --- | ---: |
| Unit ProcessCancellationRecoveryContractTests | 9 |
| Unit ProcessCancellationRecoveryCodecTests | 7 |
| Integration ProcessCancellationRecoveryMigrationTests | 4 |
| Integration ProcessCancellationRecoveryAtomicTests | 4 |
| Integration ProcessCancellationRecoveryQueueTests | 7 |
| Integration ProcessCancellationRecoverySeedTests | 3 |
| Integration ProcessDecisionCompletionPersistenceTests | 38 |
| Integration ProcessRootQuiescenceTests | 19 |
| Integration ProcessRootWorkAdmissionTests | 14 |
| Integration ProcessDecisionPersistenceTests | 31 |

Every class used build-backed discovery with its full namespace/class prefix. Execution
used `dotnet test <owning csproj> --configuration Release --no-build --no-restore
-p:UseLocalCanDoItAllLibraries=true /m:1 --filter <recorded selection>` with TRX output.
The final large SQL run combined seven exact class prefixes; that assembly disables test
parallelization. Its migration prerequisite ran separately first. The final two repaired
classes were repeated in full, with unchanged-input proof for the other 125 cases.

Seven original direct Release builds pass: Application, Persistence, Migrations, Process
Module, Web, Unit and Integration. Each test-only correction rebuilt Integration in the
same standard output. Exact source/runtime comparisons show that production and Unit
inputs did not change across those corrections; only the reviewed test files, Integration
DLL/PDB and four intentional portability-baseline additions differ.

All final static checks pass: **15669 reviewed portability findings unchanged**, rendering
register, documentation and whitespace. The four new baseline entries are generated
migration model metadata for existing integer/boolean properties, with no platform behavior.
Final enforcement ran without `--write-baseline`. Scoped Application and Persistence
architecture snapshots have zero cycles and unchanged project-reference direction.
Application snapshot `snap-20261022104639-66db4cf6` has 396 types / 4152 members / 494 edges;
Persistence `snap-20261022104912-4d5e01f9` has 197 / 1486 / 219 and 34 entities. Ten existing
Persistence package-advisory diagnostics remain; direct builds succeed.

The real PostgreSQL history test uses 20,000 roots, 625 stopped roots and 16 pending queue
rows. Both keyset and due-row plans use the intended indexes. Its single diagnostic sample
records 189.80 ms to insert history, 167.22 ms for index/schema migration and 30.19 ms for
queue population. These are construction samples, not sustained throughput or an SLO.

- Final source manifest SHA256: `CAC07343BB4055413F041B3C8819F89C632E8A0E14B966B43E6A57E8E116539F`.
- Final runtime manifest SHA256: `91BEAEE20E8C38CB32AD6EFF91C30CB24395FB3B7AD2E670AA5E0FD0C6ED37B5`.
- Retained proof: `codex/bundles/concurrent-mutations/proof/2026-10-22-cancellation-recovery.json`.
- Proof: 516716 bytes; SHA256 `48B4A3E937A784B6902D802A7FF92DFE36C7CE94CA91D5C304BEA33693DB1F35`.

The proof retains every executed case, exact commands, unchanged-input bridges, index
measurements and resource reconciliation, and passes artifact secret/sentinel checks.

## Failures retained and resolved

| Attempt | Executed | Passed | Failed | Unexecuted |
| --- | ---: | ---: | ---: | ---: |
| Initial queue candidate | 20 | 19 | 1 | 116 |
| Migration fixture retry | 4 | 3 | 1 | 116 |
| Complete SQL selection | 120 | 118 | 2 | 0 |
| Two corrected owning classes | 11 | 11 | 0 | 0 |

The four failures were test defects: literal JSON braces passed to composite-format SQL;
expecting ForeignKeyViolation instead of PostgreSQL RestrictViolation; resolving a scoped
queue from the root DI provider; and sorting typed IDs without their Guid comparer. The
last correction compares underlying identities and proves both roots were claimed and
all transaction locks released. Migration assertions now also verify the exact restrictive
constraint and unchanged root/queue rows. Production code was unchanged throughout.
There were 155 executions, including four historical failures; the 136 qualified cases
are identified individually in the retained proof. No failed case is counted as passing.

## Resources and next integration

All eight execution commands across the four candidates are observed. Every candidate
ends with zero new process, database-name or temporary-path residue. The existing isolated
PostgreSQL 18 fixture and standard Release outputs remain necessary for continuing work.
No new container or application host was created. Historical policy-denied cleanup targets
remain preserved and unresolved; ordinary 5032, installed tools and unrelated data remain
untouched. SPA stays on `codex/concurrent-mutations`, with no main merge or push.

Next integrate finite stage scheduling, indexed metadata selection, actual Agent stop and
accounting, shared-scope/root completion, bounded HTTP/status and transfer safeguards.
The ordinary Agent candidate port still loads all selected records and does not enforce
Take; it cannot serve as a bounded worker operation. Its replacement must retain legacy
coverage and qualify cold metadata cost. Snapshot refresh, producer/Workflow/settings,
remaining writers, sustained capacity and the final integrated Stable gate remain open.
