# Source Impact Inventory

| Source | Expected action |
| --- | --- |
| `ProcessRunAutomationDispatchService.ArtifactProjection.cs` | Main migration target. Reduce repeated source-specific planning and first write path. |
| `ProcessRunAutomationDispatchService.ArtifactValidation.cs` | Consumer of expectation matching and source key helpers. Avoid broad movement. |
| `ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Consumer of validation rules. Do not move finalization logic. |
| `ProcessRunAutomationDispatchService.CompletionArtifactValidator.cs` | Consumer proof for required-artifact satisfaction. Do not rewrite. |
| `ProcessArtifactProjectionPlanner.cs` | Extend or split into source adapters; avoid becoming a monolith. |
| `ProcessArtifactProjectionLineageBuilder.cs` | Keep pure lineage behavior. |
| `ProcessArtifactExpectationMatcher.cs` | Harden around DTO snapshots; avoid direct dispatcher nested type dependency where practical. |
| `ProcessArtifactEvidenceValidationRules.cs` | Keep as selected pure validation rules; do not absorb full validation partial. |
