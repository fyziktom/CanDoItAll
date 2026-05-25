# SB05 proof manifest

## Status

Completed.

## Owned requirements

Turn PostgreSQL batch claims into bounded safe parallel processing for automation deliveries, process outbox, and connector outbox.

## Changed files

- `repo://src/CanDoItAll.Modules.Automation/Runtime/AutomationRuntimeOptions.cs`
- `repo://src/CanDoItAll.Modules.Automation/Services/AutomationHostedServices.cs`
- `repo://src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeOptions.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs`
- Hash proof: `bundle://proof/SB08/transcripts/changed-file-hashes.txt`

## Command transcripts

- `bundle://proof/SB05/transcripts/bounded-parallelism-source-audit.txt`
- `bundle://proof/SB08/transcripts/focused-integration-tests.txt`
- `bundle://proof/SB08/benchmark-report.md`

## Source assertions

- Automation claim SQL now returns `EnvelopeId`, groups claimed deliveries by envelope, and processes groups through bounded `Parallel.ForEachAsync`.
- Process outbox claim SQL returns `ProcessRunId` and `CommandKey`, builds a partition key, and processes partitions with bounded parallelism.
- Connector outbox claim SQL returns `ProjectId`, `ConnectorPluginKey`, and `CommandKey`, builds a partition key, and processes partitions with bounded parallelism.
- Each item uses fresh context creation inside the per-item processor.

## Semantic positive proof

Focused integration tests pass for the concurrency and lease/claim paths included in the bundle closure set. Source audit proves conservative default parallelism greater than one and partitioned scheduling.

## Adversarial negative proof

Lease token checks remain on item completion paths; partitioning prevents same-envelope or same-aggregate work from being processed concurrently in the same batch.

## Residual risks

No numeric wall-clock benchmark was captured. The bundle is closed with deterministic concurrency stress proof and source-level throughput proof; `bundle://proof/SB08/benchmark-report.md` records this explicitly.
