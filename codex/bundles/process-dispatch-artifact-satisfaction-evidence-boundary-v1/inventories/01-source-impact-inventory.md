# Source Impact Inventory

Codex must refresh this inventory in SB02 before production movement.

| Source file | Current role | Planned movement |
| --- | --- | --- |
| `ProcessRunAutomationDispatchService.ArtifactValidation.cs` | Main target: satisfaction, provider-native evidence, response text, external target, shallow path, quality validation | Gradually delegate to module-local helpers |
| `ProcessArtifactQualityValidationRules.cs` | Existing quality validation rule helper | Reuse; avoid duplication |
| `ProcessArtifactProviderNativeVisualValidationRules.cs` | Existing browser/visual helper | Reuse for provider-native evidence facts |
| `ProcessArtifactPathValidationRules.cs` | Existing managed path helper | Reuse for shallow path and path normalization |
| `ProcessArtifactTextMatchRules.cs` | Existing text/token helper | Reuse for response and narrative signals |
| `ProcessRunAutomationDispatchService.ImplementationProofBridges.cs` | Existing process mock/workspace write bridges | Reuse or replace with typed satisfaction helper wrappers |
| `ProcessRunAutomationDispatchService.ToolValidation.cs` | Consumer of completion blocker summaries | Update only through wrappers; do not rewrite status logic |
| `ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Consumer of artifact validation result statuses | Do not alter finalizer transition behavior |
| `ProcessRunAutomationDispatchService.ArtifactProjection.cs` | Projection layer; may share response/provider-native semantics | Do not move writes here unless explicitly required |
