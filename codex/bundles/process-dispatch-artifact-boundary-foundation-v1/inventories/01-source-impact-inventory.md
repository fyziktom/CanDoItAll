# Source Impact Inventory

Primary source files:

| Source | Planned treatment |
| --- | --- |
| `ProcessRunAutomationDispatchService.ArtifactValidation.cs` | Inventory first; extract selected high-risk validation rules late in bundle. |
| `ProcessRunAutomationDispatchService.ArtifactProjection.cs` | Extract matcher/lineage/planner; migrate execution-artifact path first. |
| `ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs` | Inventory only unless a helper is already extracted and safe to consume. |
| `ProcessRunAutomationDispatchService.CompletionArtifactValidator.cs` | Inventory and selected validation rule usage only. |
| `ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Do not move transition/finalization ownership; use only as validation consumer proof. |
| `ProcessRunAutomationDispatchService.ToolValidation.cs` | Ensure required-tool behavior remains stable; do not migrate wholesale. |
| `ProcessAutomationExecutionClient.cs` | Entry smoke only; do not expand scope unless artifact helper needs snapshots. |
