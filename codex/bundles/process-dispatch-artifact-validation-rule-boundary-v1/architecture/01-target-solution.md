# Target Solution

The target state for this bundle is a process-module-local artifact validation boundary:

```text
Dispatcher orchestration
  -> ProcessArtifactValidationContextBuilder / snapshots
  -> ProcessArtifactExpectationMatcher / path-title-content rules
  -> ProcessArtifactEvidenceValidationRules / mode-producer-path-content rules
  -> ProviderNativeVisualEvidenceRules
  -> PlaceholderQualityValidationRules
  -> ProjectStructureRequirementPreservationRules
```

The dispatcher still owns orchestration, candidate state, retries, storage, EF, and step finalization. The helpers own pure decisions and are tested directly.

## Must Preserve

- exact artifact expectation matching order,
- required artifact satisfaction behavior,
- provider-native visual evidence behavior,
- placeholder/quality validation behavior,
- project-structure requirement preservation behavior,
- existing external reference keys and lineage behavior.
