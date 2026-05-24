# SB05 Proof Manifest

## Subbundle

SB05-postgresql-batch-claim-outbox — Completed.

Owned requirements: R8, R9, R10.

Semantic invariant contract: `bundle://proof/SB05-postgresql-batch-claim-outbox/semantic-invariants.md`.

## Changed Files

| File | SHA-256 before | SHA-256 after | Reason |
|---|---|---|---|
| `repo://src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs` | See `bundle://proof/SB08-final-validation-benchmark-gate/changed-file-hashes.tsv` | See hash inventory | Adds PostgreSQL batch claim for automation deliveries. |
| `repo://src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs` | See hash inventory | See hash inventory | Adds PostgreSQL batch claim for connector commands. |
| `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | See hash inventory | See hash inventory | Adds PostgreSQL batch claim for process outbox messages. |

## Commands

| Command | Transcript path | Result |
|---|---|---|
| Focused PostgreSQL integration sweep | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-test-integration-focused.txt` | Passed 452 tests, including automation, connector outbox, and process outbox integration filters. |
| Source assertions | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt` | Shows `FOR UPDATE SKIP LOCKED` claim paths. |
| Residue/bottleneck audit | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/audit-residue-and-bottlenecks.txt` | Passed and lists the new PostgreSQL claim markers. |

## Semantic Positive Proof

Due queue rows are claimed in atomic PostgreSQL batches through `FOR UPDATE SKIP LOCKED` and `UPDATE ... RETURNING` while preserving lease token, attempt count, stale rescue, and terminal-state filters.

## Adversarial Negative Proof

Focused integration tests exercise concurrent outbox/automation/process paths. The SQL claims skip locked non-stale work instead of stealing it, rejecting the shallow implementation that would select due rows first and update them later.

## Canonicality Proof

Claim ownership is stored in PostgreSQL rows, not only in process-local memory. External work is started only after a worker owns the lease token.

## Anti-Stub Audit

`bundle://proof/SB08-final-validation-benchmark-gate/transcripts/anti-stub-audit.txt` found no stub markers in changed production files.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Automation delivery lease | `repo://src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs` | Same service delivery execution path | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-test-integration-focused.txt` | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt` |
| Connector command lease | `repo://src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs` | Connector outbox dispatcher path | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-test-integration-focused.txt` | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt` |
| Process outbox lease | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | Process outbox dispatcher path | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-test-integration-focused.txt` | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt` |

## Browser Validation Analytics

N/A. SB05 has no UI behavior.

## Remaining Risks

The broad integration command remains blocked by local PostgreSQL auth outside these focused claim paths. The changed claim surfaces passed the focused integration sweep.
