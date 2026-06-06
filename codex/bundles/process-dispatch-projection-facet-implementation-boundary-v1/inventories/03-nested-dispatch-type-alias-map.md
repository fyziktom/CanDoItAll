# Nested Dispatch Type Alias Inventory

Current alias examples:
- `DispatchCandidate`
- `DispatchArtifactExpectation`
- `ProcessStepDispatchClaim`
- `ProcessMockArtifactProjection`
- `SessionFileContent`
- `ArtifactProjectionLineage`

This bundle should not fully migrate these models. It should document usage and optionally introduce module-local read-only view adapters only when low-risk.

Future extraction candidate:
- `ProcessArtifactProjectionCandidateSnapshot`
- `ProcessArtifactProjectionExpectation`
- `ProcessArtifactProjectionClaim`
- `ProcessArtifactProjectionLineageContext`
