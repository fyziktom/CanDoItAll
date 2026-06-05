# Source Impact Inventory

| Source | Current role | Planned treatment |
| --- | --- | --- |
| `ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Main target, 2091 lines in SB01 baseline scan | Gradually reduce through local helpers |
| `ProcessRunAutomationDispatchService.ArtifactValidation.cs` | Existing artifact rules extracted earlier | Consumer proof only |
| `ProcessRunAutomationDispatchService.ArtifactProjection.cs` | Projection write coordinator already extracted | Consumer proof only |
| `ProcessRunAutomationDispatchService.ToolValidation.cs` | Tool/recovery helper extraction completed | Consumer proof only |
| `ProcessArtifactEvidenceValidationRules.cs` | Existing validation rule helper | May consume module-local finalizer enums after type extraction |
| `ProcessArtifactValidationSnapshot*.cs` | Existing local validation snapshots | Reuse; do not move to Core |
| `ProcessArtifactProjection*.cs` | Existing projection helpers/coordinators | Reuse; do not broaden |
