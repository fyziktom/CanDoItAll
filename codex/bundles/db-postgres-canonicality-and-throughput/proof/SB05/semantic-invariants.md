# SB05 semantic invariants

## SB05-I5 parallel work is lease-token scoped

- Source raw note: claiming a batch and then processing it sequentially leaves PostgreSQL throughput locked behind old SQLite-era assumptions.
- Expected behavior: claimed records can run concurrently only after durable claim and within partition boundaries that protect aggregate rows.
- Disallowed shallow implementation: increasing batch size without bounded parallel execution or without preserving lease checks.
- Passing proof: `bundle://proof/SB08/transcripts/focused-integration-tests.txt`.
- Changed source files and hashes: `bundle://proof/SB08/transcripts/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB05/transcripts/bounded-parallelism-source-audit.txt`.
- Red-team negative case: per-partition grouping and existing lease-token checks prevent duplicate completion under competing workers.
- Downstream dependency check: `bundle://proof/SB08/transcripts/semantic-invariant-index.txt`.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Automation delivery partition | `repo://src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs` | same service dispatch loop | `bundle://proof/SB08/transcripts/focused-integration-tests.txt` | `bundle://proof/SB05/transcripts/bounded-parallelism-source-audit.txt` |
| Process outbox partition | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | process outbox worker | `bundle://proof/SB08/transcripts/focused-integration-tests.txt` | `bundle://proof/SB05/transcripts/bounded-parallelism-source-audit.txt` |
| Connector outbox partition | `repo://src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs` | connector outbox worker | `bundle://proof/SB08/transcripts/focused-integration-tests.txt` | `bundle://proof/SB05/transcripts/bounded-parallelism-source-audit.txt` |
