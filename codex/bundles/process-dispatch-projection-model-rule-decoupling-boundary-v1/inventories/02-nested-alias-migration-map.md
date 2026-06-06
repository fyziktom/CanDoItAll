# Nested Alias Migration Map

| Current alias | Target module-local model | Migration note |
| --- | --- | --- |
| `DispatchCandidate` | `ProcessProjectionCandidateSnapshot` + `ProcessProjectionMutableCandidateState` | Split read-only fields from mutable recorded/external-reference state. |
| `DispatchArtifactExpectation` | `ProcessProjectionArtifactExpectation` | Include id, title, kind, required flag, trust/sensitivity, validation summary, allowed usage. |
| `ProcessMockArtifactProjection` | `ProcessProjectionProcessMockArtifact` | Keep role key, relative path, content signal text, branch outcome if used. |
| `SessionFileContent` | `ProcessProjectionSessionFileContent` | Path/content only. |
| `ArtifactProjectionLineage` | `ProcessProjectionLineageInput` | Recovery lineage only. |
| `ProcessStepDispatchClaim` | Keep as dispatch-only claim until a later claim boundary. | Facet can still accept it at claim guard only. |
