# Semantic invariants SB05

## Invariants to prove

- No stale worker may write canonical process DB state.
- Lease ownership must be explicit and verifiable.
- Retry behavior must be idempotent.
- PostgreSQL runtime must remain canonical.

## Negative proof

- Pre-hardening source inventory had generic composite indexes for process outbox, automation delivery, and connector command claims, but no PostgreSQL query-plan proof that the hot claim SQL avoided large sequential scans.
- `bundle://proof/SB05/query-plans/process-step-dispatch-header.txt` also proved the existing `IX_Processes_StepRuns_ProcessRunId_Sequence` index is selected for dispatch header scans, so no redundant step index was added.

## Positive proof

- `bundle://proof/SB05/index-inventory.md` maps each hot claim path to its backing index.
- `bundle://proof/SB05/postgres-claim-index-tests.log` proves the PostgreSQL regression test passed.
- `bundle://proof/SB05/query-plans/*.txt` contains `EXPLAIN (ANALYZE, BUFFERS)` output for seeded datasets and shows index scans on every hot claim table.
- `bundle://proof/SB05/ef-pending-model-changes.log` proves EF model drift is closed.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `Processes_Outbox` pending claim order | `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/20260524183000_ProcessClaimHotPathIndexes.cs` | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | `bundle://proof/SB05/query-plans/process-outbox-claim.txt` | Missing plan proof before SB05 |
| `Processes_StepRuns` dispatch header order | Existing baseline `IX_Processes_StepRuns_ProcessRunId_Sequence` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | `bundle://proof/SB05/query-plans/process-step-dispatch-header.txt` | Redundant partial index candidate rejected by plan proof |
| `Automation_EnvelopeDeliveries` delivery claim order | `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/20260524183000_ProcessClaimHotPathIndexes.cs` | `repo://src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs` | `bundle://proof/SB05/query-plans/automation-delivery-claim.txt` | Missing plan proof before SB05 |
| `Workspace_ConnectorCommands` pending claim order | `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/20260524183000_ProcessClaimHotPathIndexes.cs` | `repo://src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs` | `bundle://proof/SB05/query-plans/connector-command-claim.txt` | Missing plan proof before SB05 |
