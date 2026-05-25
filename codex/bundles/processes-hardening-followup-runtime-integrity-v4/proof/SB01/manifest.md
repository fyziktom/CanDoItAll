# SB01 Proof Manifest

## Status

Completed.

## Owned Requirements

- RQ01: Make upstream artifact materialization reactivation transaction-safe and ensure the just-recorded artifact can unblock dependent steps.
- Raw notes: N001, N002, N004, N005.

## Semantic Invariant Contract

- `bundle://proof/SB01/semantic-invariants.md`

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs` passes the newly tracked `ProcessArtifactRecord` into `ReactivateBlockedDownstreamStepsAfterArtifactMaterializationAsync`.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs` adds the tracked artifact to the artifact satisfaction set when the persisted query cannot see it yet.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs` includes `RecordArtifactAsync_SB01_INV_001_reactivates_blocked_downstream_with_tracked_materialized_artifact`.
- Transcript: `bundle://proof/SB01/transcripts/source-assertions.txt`

## Production Behavior Artifact Matrix

| Artifact/signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessArtifactRecord` tracked during materialization | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs` and `bundle://proof/SB01/transcripts/source-assertions.txt` | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs` `AreArtifactInputsSatisfiedForMaterializationResume` | `RecordArtifactAsync` records the artifact, re-evaluates blocked downstream steps, then saves in the same call | `bundle://proof/SB01/transcripts/failing-first.txt` |
| `missing-upstream-artifact-materialization-resolved` journal event | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs` | Runtime read/observation flows consume `ProcessJournalEntry` records | Produced only when the blocked step is reopened after all dependencies and artifact inputs are satisfied | `bundle://proof/SB01/transcripts/passing.txt` asserts the journal entry after production `RecordArtifactAsync` |

## Failing-First Or Red-Team Proof

Transcript: `bundle://proof/SB01/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB01/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB01/transcripts/changed-file-hashes.txt`

## Validation

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --no-build --filter "FullyQualifiedName~RecordArtifactAsync_SB01_INV_001"` passed.
- Source assertions passed.
- Anti-stub audit passed.

## Blockers

None recorded yet.
