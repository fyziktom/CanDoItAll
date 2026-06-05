# Pre-Execution Method Map

Codex must complete this inventory in SB02 before production movement.

| Method/Region | Current source | Pure or side-effectful | Expected helper | Must preserve |
| --- | --- | --- | --- | --- |
| `BlockDispatchForDatabaseRequirementAsync` | Dispatch.cs | side-effectful transition | Database requirement blocker | target status, transition request, logging |
| `TryRequestMissingUpstreamArtifactMaterializationAsync` | Dispatch.cs | mixed | pre-execution guard handler + materialization coordinator | return semantics, block transition, journal, rerun |
| `ResolveMissingUpstreamArtifactInputs` | Dispatch.cs wrapper | pure | `ProcessMissingUpstreamArtifactMaterializationFactsResolver` | missing input detection |
| `IsRunnableUpstreamArtifactMaterializationTarget` | Dispatch.cs wrapper | pure | `ProcessMissingUpstreamArtifactMaterializationFactsResolver` | target eligibility |
| `CreateMissingUpstreamArtifactMaterializationFingerprint` | Dispatch.cs wrapper | pure | `ProcessMissingUpstreamArtifactMaterializationFingerprint` | exact hash input |
| `RecordMissingUpstreamArtifactMaterializationAsync` | Dispatch.cs wrapper | side-effectful DB | `ProcessMissingUpstreamArtifactMaterializationJournalCoordinator` | event type, correlation id, JSON detail, duplicate check |
| `BuildMissingUpstreamArtifactMaterializationBlockReason` | Dispatch.cs wrapper | pure | `ProcessMissingUpstreamArtifactMaterializationBlocker` | exact reason semantics |
| `BuildUpstreamArtifactMaterializationDirective` | Dispatch.cs wrapper | pure | `ProcessMissingUpstreamArtifactRerunRequestBuilder` | directive semantics |
