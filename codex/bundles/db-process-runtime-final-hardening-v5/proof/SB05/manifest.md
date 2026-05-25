# Proof manifest SB05

## Status

Completed.

## Owned requirements

- R4: PostgreSQL claim queries must be backed by appropriate indexes and plan proof.
- R8: Broad validation caveats must be closed or classified.

## Changed files

| File | SHA-256 | Reason |
|---|---:|---|
| `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/20260524183000_ProcessClaimHotPathIndexes.cs` | `C35401A000726176963B109DA8852815FD1342CF5F0B9CFC5E5820037EF7C84F` | Adds PostgreSQL expression/partial hot-path indexes for process outbox, automation delivery, and connector command claims. |
| `repo://tests/CanDoItAll.Tests.Integration/PostgreSqlClaimQueryPlanIntegrationTests.cs` | `580997B600C2B4516D3B5ED17A4DB2531E37173108E8FD38C5CF433E4C86655B` | Verifies index presence, seeds large PostgreSQL datasets, captures query plans, and asserts no hot-table sequential scans. |
| `bundle://proof/SB05/index-inventory.md` | `84A97678E511526A07B09B887E1196EE168C487637D33D2499615F1869AFC4A5` | Documents the hot-path index inventory and the step-dispatch decision to reuse an existing index. |

## Validation commands

| Command | Result | Transcript |
|---|---|---|
| Focused PostgreSQL claim index and plan test with `CANDOITALL_SB05_QUERY_PLAN_DIR` | Passed, 1 test | `bundle://proof/SB05/postgres-claim-index-tests.log` |
| `dotnet ef migrations has-pending-model-changes --project src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj --startup-project src\CanDoItAll.Web\CanDoItAll.Web.csproj --context AppDbContext` | Passed, no pending model changes | `bundle://proof/SB05/ef-pending-model-changes.log` |
| Claim index source audit | Passed | `bundle://proof/SB05/claim-index-source-audit.log` |

## Source assertions

- `IX_Processes_Outbox_PendingClaimOrder` matches the process outbox `COALESCE(NextAttemptAtUtc, CreatedAtUtc), CreatedAtUtc` claim order and filters to pending rows.
- `IX_Automation_EnvelopeDeliveries_DueClaimOrder` covers due pending/retry/stale-running automation deliveries without completed/dead-lettered rows.
- `IX_Workspace_ConnectorCommands_PendingClaimOrder` matches connector command claim ordering and excludes commands still waiting for approval.
- Step dispatch header scans already use `IX_Processes_StepRuns_ProcessRunId_Sequence`; the redundant partial step index candidate was not kept.

## Semantic adequacy

The shallow-pass trap was to see `FOR UPDATE SKIP LOCKED` and assume concurrency alone solved throughput. The seeded `EXPLAIN (ANALYZE, BUFFERS)` plans prove the hot claim queries use indexes on large tables:

- `bundle://proof/SB05/query-plans/process-outbox-claim.txt`
- `bundle://proof/SB05/query-plans/process-step-dispatch-header.txt`
- `bundle://proof/SB05/query-plans/automation-delivery-claim.txt`
- `bundle://proof/SB05/query-plans/connector-command-claim.txt`

## Residual risks

The new indexes are PostgreSQL-specific raw migration artifacts, so EF model drift checks do not model them directly. The integration test closes that gap by verifying `pg_indexes` and query-plan usage against a migrated PostgreSQL database.
