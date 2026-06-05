# Pre-Execution Method Map

Codex must complete this inventory in SB02 before production movement.

| Method/Region | Current source | Pure or side-effectful | Expected helper | Must preserve |
| --- | --- | --- | --- | --- |
| `BlockDispatchForDatabaseRequirementAsync` | Dispatch.cs | side-effectful transition | Database requirement blocker | target status, transition request, logging |
| `TryRequestMissingUpstreamArtifactMaterializationAsync` | Dispatch.cs | mixed | pre-execution guard handler + materialization coordinator | return semantics, block transition, journal, rerun |
| `ResolveMissingUpstreamArtifactInputs` | Dispatch.cs | pure | upstream gap facts | missing input detection |
| `IsRunnableUpstreamArtifactMaterializationTarget` | Dispatch.cs | pure | upstream gap facts | target eligibility |
| `CreateMissingUpstreamArtifactMaterializationFingerprint` | Dispatch.cs | pure | fingerprint helper | exact hash input |
| `RecordMissingUpstreamArtifactMaterializationAsync` | Dispatch.cs | side-effectful DB | journal coordinator | event type, correlation id, JSON detail, duplicate check |
| `BuildMissingUpstreamArtifactMaterializationBlockReason` | Dispatch.cs | pure | message builder | exact reason semantics |
| `BuildUpstreamArtifactMaterializationDirective` | Dispatch.cs | pure | rerun request builder | directive semantics |
