# Artifact Validation Method Inventory Seed

Codex must refresh this inventory in SB02 from live source before movement.

| Method/Region | Expected category | Side effects? | Candidate helper |
| --- | --- | --- | --- |
| `ResolveArtifactExpectation*` | expectation resolution | none / reads candidate state | validation context resolver |
| `MatchExpectedArtifactId*` | matching orchestration | none | expectation matcher |
| `MatchesExpectedArtifact` | title/path matching | none | title/path rules |
| `MatchExpectedArtifactIdByTextContent` | text-content matching | none | content signal rules |
| `ScoreProviderNativeVisualArtifactExpectation` | visual proof scoring | none | provider-native visual rules |
| `ResolveMissingConcreteProofSummary` | concrete proof validation | none | quality validation rules |
| `ResolveInvalidQualityValidationProofSummary` | build/test/browser quality validation | none | quality validation rules |
| `ResolveDowngradedProjectStructureRequirementSummary` | project-structure preservation | none | project-structure preservation rules |
| Placeholder/status classification methods | artifact status validation | none | placeholder rules |
| Direct file reads/record queries | orchestration | yes | do not move in this bundle |
