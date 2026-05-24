# SB05 Semantic Invariants

## Invariants

### SB05-I1 Queue/outbox work is claimed atomically in PostgreSQL batches

Raw note: "Outbox/message/workflow queues must use PostgreSQL batch claim patterns instead of single-row sequential claim loops when safe."

Expected behavior: eligible rows are selected with `FOR UPDATE SKIP LOCKED` and claimed through `UPDATE ... RETURNING`, preserving lease token, attempt count, terminal-state checks, and stale rescue.

Shallow-pass trap: query due rows first and update them later, which duplicates work under concurrent workers.

Adversarial negative proof: focused integration tests include automation, connector outbox, and process outbox filters; source assertions show claim SQL uses locked update/returning patterns.

Semantic positive proof: `repo://src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs`, `repo://src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs`, and `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs`.

Production assertions: `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt`.

Changed source files: see `bundle://proof/SB08-final-validation-benchmark-gate/changed-file-hashes.tsv`.

Downstream dependency check: SB06 durable step claims build on the same PostgreSQL-owned-work principle.

### SB05-I2 External work does not run inside claim transactions

Raw note: "Avoid holding DB transactions during external agent/plugin execution."

Expected behavior: batch claim returns owned rows; external delivery/execution occurs after claim ownership is recorded.

Shallow-pass trap: wrap claim and external call in one transaction, serializing workers and risking long lock holds.

Adversarial negative proof: source assertions and focused integration execution complete without deadlocking concurrent claim paths.

Semantic positive proof: production claim methods return claimed rows before dispatcher work.

Production assertions: `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt`.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Work-claim lease rows | `repo://src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs`, `repo://src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs`, `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | Respective dispatcher execution paths | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-test-integration-focused.txt` | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt` |
