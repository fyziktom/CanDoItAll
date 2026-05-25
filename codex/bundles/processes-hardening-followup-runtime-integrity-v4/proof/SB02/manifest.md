# SB02 Proof Manifest

## Status

Completed.

## Owned Requirements

- RQ02: Replace long lineage encoded in `ExternalReferenceKey` with typed provenance/hash metadata.
- Raw notes: N001, N002, N004, N005.

## Semantic Invariant Contract

- `bundle://proof/SB02/semantic-invariants.md`

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactProjectionLineage.cs` defines typed projection lineage and source kinds.
- `repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs` persists `ProjectionLineageJson`.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs` writes compact `manager-recovery-artifact|sha256:` keys and stores full typed lineage in record requests.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` resolves producer/current-run checks from typed lineage before legacy key/provenance text.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkflowRunCoordinator.cs` and `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` persist workflow and subprocess lineage payloads.
- Transcript: `bundle://proof/SB02/transcripts/source-assertions.txt`

## Production Behavior Artifact Matrix

| Artifact/signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| `ProjectionLineageJson` | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs`, projection services, and `bundle://proof/SB02/transcripts/source-assertions.txt` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Added to `Processes_ArtifactRecords` by `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/20260525140500_ProcessArtifactProjectionLineage.cs` | `bundle://proof/SB02/transcripts/failing-first.txt` |
| Compact manager recovery key | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs` | Artifact dedupe and validation consumers use compact key plus typed lineage | Produced when recovery lineage exists; full lineage remains in JSON payload | `bundle://proof/SB02/transcripts/passing.txt` |

## Failing-First Or Red-Team Proof

Transcript: `bundle://proof/SB02/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB02/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB02/transcripts/changed-file-hashes.txt`

## Validation

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --no-build --filter "FullyQualifiedName~SB02_INV_001"` passed.
- Source assertions passed.
- Anti-stub audit passed.

## Blockers

None recorded yet.
