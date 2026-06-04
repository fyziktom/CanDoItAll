# Validation Rule Family Map

Candidate rule families:

| Rule family | Current owner | Target helper | Driver-readiness role |
| --- | --- | --- | --- |
| Expected artifact path matching | ArtifactValidation | ProcessArtifactPathValidationRules | Drivers can report managed paths/evidence paths. |
| Title/slug matching | ArtifactValidation | ProcessArtifactTitleMatchRules | Drivers can label outputs consistently. |
| Text-content matching | ArtifactValidation | ProcessArtifactContentSignalRules | Drivers can supply text summaries/content snippets. |
| Provider-native visual matching | ArtifactValidation/BrowserProof | ProcessProviderNativeVisualArtifactRules | Browser/.NET/Web drivers can produce visual proof. |
| Placeholder detection | ArtifactValidation/Finalizer | ProcessArtifactPlaceholderRules | Drivers must distinguish real output from placeholder. |
| Quality/build/test/browser proof | ArtifactValidation | ProcessArtifactQualityValidationRules | SW-dev drivers can provide build/test/browser proof. |
| Project-structure preservation | ArtifactValidation/Grounding | ProcessProjectStructureRequirementPreservationRules | Business/SW drivers can preserve scoped requirements. |

Do not introduce driver APIs in this bundle. Use these names to keep future driver integration understandable.
