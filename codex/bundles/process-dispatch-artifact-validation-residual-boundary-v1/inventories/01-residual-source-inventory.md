# Residual ArtifactValidation Source Inventory

| Family | Current source | Target helper | Side-effect level | Notes |
| --- | --- | --- | --- | --- |
| Critical failure suppression | ArtifactValidation.cs | ProcessCriticalToolFailureSuppressionRules.cs | Pure plus delegates | Must preserve recovered scaffold and browser probe suppression |
| Browser output facts | ArtifactValidation.cs | ProcessProviderNativeBrowserOutputFacts.cs | Pure/read-only file existence checks only where explicit | Keep safe path checks |
| Browser file probe suppression | ArtifactValidation.cs | ProcessProviderNativeBrowserProbeFailureRules.cs | Read-only filesystem check | Must not mutate workspace |
| Artifact kind/content classification | ArtifactValidation.cs | ProcessArtifactKindClassificationRules.cs | Pure | Must preserve content type and kind mappings |
| Storage kind resolution | ArtifactValidation.cs | ProcessStorageContentKindRules.cs | Pure | Can reuse content type helper |
| Execution artifact fallback metadata | ArtifactValidation.cs | ProcessExecutionArtifactMetadataRules.cs | Pure | Title, external key, storage relative path |
| Technical agent diagnostic | ArtifactValidation.cs / Dispatch wrappers | ProcessTechnicalAgentBindingDiagnostics.cs | Pure | No bridge calls |
| Driver readiness map | Bundle architecture | Documentation only | N/A | No production API |
