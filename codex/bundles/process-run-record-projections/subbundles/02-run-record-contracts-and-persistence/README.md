# run-record-contracts-and-persistence

## Status

- `Completed`

## Objective

- Add the strongly typed compact run-record contract, bounded queries, dedicated EF persistence, indexes, serialization/version rules, DI registration, and additive PostgreSQL migration.

## Success Criteria

- One record is uniquely keyed by run ID and supports versioned disposition replacement.
- Common filters/order/paging use scalar indexed columns.
- Hard facts, completeness, narrative state, and optional participant index are strongly typed.
- JSON columns have centralized serialization and schema versions.
- Entity model has no navigation relationships or foreign keys to canonical records.

## Covered Inputs

- R01-R04, R06, R10-R11, R13; N002, N003, N005, N009.

## Prerequisites

- SB01 progression gate passes.
- Architecture checkpoint A0 passes.

## Exact Source References

- `C:\repositories\CanDoItAll\src\Processes\CanDoItAll.Processes.Projections`
- `C:\repositories\CanDoItAll\src\Processes\CanDoItAll.Processes.Persistence\ProcessPersistenceEntities.cs`
- `C:\repositories\CanDoItAll\src\Processes\CanDoItAll.Processes.Persistence\ProcessPersistenceConfigurations.cs`
- `C:\repositories\CanDoItAll\src\Foundation\CanDoItAll.Migrations.PostgreSql`

## UI Composition Contract

- N/A: storage/contracts have no rendered UI.

## Deliverables

- Run-record disposition, hard-fact, step/attempt, usage/cost/tool, completeness, narrative-state, query/page, summary, analytics, and graph contracts.
- `IProcessRunRecordStore` (or equivalently narrow contracts) with get/list/upsert/analytics/narrative claim/update operations.
- Dedicated EF entity/configuration/store and participant lookup shape where agent history needs an index.
- DI registration and additive migration.
- Unit/model/integration tests.

## Architecture Impact

- Projections owns read-model contracts; Persistence owns implementation; Runtime remains unchanged.
- Interfaces are limited to persistence/provider/test boundaries.
- The table is derived state and cannot drive runtime commands.

## Dependency Impact

- SB03-SB06 compile and behave against this contract. Schema changes after this gate reopen all downstream work.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical foundation: record identity, serialization, query/index shape, and migration.

## Implementation Steps

1. Add cohesive top-level projection contracts and validated query limits/cursors.
2. Add entity/configuration with unique run key and composite query indexes.
3. Add centralized JSON mapping and schema version.
4. Implement idempotent upsert, bounded list/get/analytics, participant query, and narrative claim/update.
5. Register services through existing composition conventions.
6. Generate and inspect the additive PostgreSQL migration.
7. Add tests and run Architecture Checkpoint A1.

## Scope Exceptions

- No lifecycle trigger or LLM generation in this phase.

## Do Not Do

- Do not add ORM navigations/cascade relationships.
- Do not expose arbitrary dictionaries or magic-string statuses.
- Do not add a generic repository or new project.

## Acceptance Checklist

- [x] Strong contracts cover R01-R05 data states.
- [x] EF filters/limits precede payload materialization.
- [x] Agent/project/definition/date/disposition lookup is indexed or explicitly bounded.
- [x] Migration is additive and reversible through its generated `Down`.
- [x] Architecture A1 and focused tests pass.

## Proof Required

- Focused contract/store/model tests.
- Affected project builds.
- Migration diff and model snapshot inspection.
- Architecture A1 decision in execution report.

## Browser Validation Logging

- N/A: no browser-visible change.

## Actual Proof And Progression

- Entry and closure gates: `Pass`.
- `EfProcessRunRecordStoreTests` proves bounded keyset/exact-ID/participant queries, compact versus full payloads, source/lease guards, analytics denominators/watermarks, backfill idempotency, and stale-backfill rejection after reactivation.
- `20260724224501_AddProcessRunRecords` and the model snapshot contain additive relation-free tables/indexes; `dotnet ef migrations has-pending-model-changes` reports no drift.
- The direct store tests include negative stale token/source, invalid bound, unavailable facts, and non-consuming deferral cases.
- Dependent-flow proof: production projector lifecycle and record-backed API/project consumers compile and pass focused tests.
- Progression decision: `Completed; SB03 may trust the record identity, claim, serialization, query, and source-validation contracts.`

## Behavioral Semantic Adequacy

- Raw note owned: `N002`, `N003`, `N005`, and `N009`: a reusable architecture, complete hard facts, join-light ID/JSON storage, and a governed C# boundary.
- Shipped behavior: strongly typed record/facts/completeness/lifecycle contracts, scalar-filtered compact/full reads, lease/source-guarded stage updates, participant indexing, additive persistence, and validated idempotent backfill are implemented.
- Source proof: `ProcessRunRecordContracts.cs`, `EfProcessRunRecordStore.cs`, `EfProcessRunRecordBackfillSource.cs`, `ProcessRunRecordPersistenceCodec.cs`, `ProcessRunRecordConfigurations.cs`, and migration `20260724224501_AddProcessRunRecords`.
- Test proof: `EfProcessRunRecordStoreTests` covers disposition round-trip, duplicate seed and revision, exclusive claims, failure schedules, compact/full paging, scalar analytics, index shape, backfill, stale-backfill rejection, and PostgreSQL query translation.
- Shallow-pass trap: a DTO-only snapshot backed by JSON deserialization/in-memory filtering, `Skip` paging, ORM navigations, or an unguarded upsert would retain the original I/O and lifecycle races.
- Adversarial negative proof: invalid bounds, stale claim tokens/source sequences, duplicate seeds, non-consuming deferrals, superseded records, descendant exclusion, and a captured seed applied after reactivation are rejected.
- Semantic positive proof: every typed disposition round-trips, keyset pages advance without duplicates, compact results omit full payloads, participant lookup uses its index shape, and analytics distinguish terminal data-through/source sequence from later stage updates.
- Anti-stub audit: store tests exercise `EfProcessRunRecordStore` through EF and inspect Npgsql SQL translation plus the generated migration/model; no generic repository, fake persistence implementation, navigation, TODO migration, or `NotImplementedException` carries production behavior.

## Progression Gate

- SB03 starts only after store behavior, indexes, migration, serialization, dependency direction, and idempotent upsert are proven.

## Reopen Triggers

- Downstream assembly needs missing data; a query requires JSON scanning; schema cannot represent escalation supersession or completeness; a forbidden project reference appears.

## Suggested Agent Prompt

```text
Implement SB02 only. Keep contracts strongly typed and persistence join-free. Prove model/index/query behavior and stop before lifecycle or API work.
```
