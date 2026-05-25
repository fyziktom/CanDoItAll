# SB05 PostgreSQL claim index inventory

## Hot claim paths

| Claim path | Query source | Index | Status | Plan proof |
|---|---|---|---|---|
| Process outbox batch claim | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | `IX_Processes_Outbox_PendingClaimOrder` | Added raw PostgreSQL expression/partial index in `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/20260524183000_ProcessClaimHotPathIndexes.cs` | `bundle://proof/SB05/query-plans/process-outbox-claim.txt` |
| Process step dispatch header scan | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | `IX_Processes_StepRuns_ProcessRunId_Sequence` | Existing baseline index. It is used for ordered per-run step dispatch; no new duplicate step index was kept. | `bundle://proof/SB05/query-plans/process-step-dispatch-header.txt` |
| Automation envelope delivery claim | `repo://src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs` | `IX_Automation_EnvelopeDeliveries_DueClaimOrder` | Added raw PostgreSQL partial/index-with-include migration. | `bundle://proof/SB05/query-plans/automation-delivery-claim.txt` |
| Connector command claim | `repo://src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs` | `IX_Workspace_ConnectorCommands_PendingClaimOrder` | Added raw PostgreSQL expression/partial index in the same migration. | `bundle://proof/SB05/query-plans/connector-command-claim.txt` |

## Index rationale

- Process outbox and connector command claim queries order by `COALESCE(NextAttemptAtUtc, CreatedAtUtc), CreatedAtUtc`; raw PostgreSQL expression indexes match that order without changing the canonical EF model.
- Automation delivery claims order by `AvailableAtUtc, CreatedAtUtc` across pending, retry, and stale running deliveries; the partial index keeps completed/dead-lettered rows out of the hot path.
- Step dispatch is naturally partitioned by `ProcessRunId` and ordered by `Sequence`; the existing baseline index was selected by PostgreSQL on a seeded dataset, so an additional filtered index would be redundant.

## Regression coverage

`repo://tests/CanDoItAll.Tests.Integration/PostgreSqlClaimQueryPlanIntegrationTests.cs` creates a migrated PostgreSQL database, seeds large hot-path datasets, verifies index presence via `pg_indexes`, captures `EXPLAIN (ANALYZE, BUFFERS)` plans when `CANDOITALL_SB05_QUERY_PLAN_DIR` is set, and asserts the hot tables do not use sequential scans.
