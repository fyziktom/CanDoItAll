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

Implemented source changes:

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactExpectationMatcher.cs` added as the pure strong-match selector used by `MatchExpectedArtifactId`.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionLineageBuilder.cs` added for recovery-aware external-reference keys, lineage records, and provenance text.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs` added for pure projection plans and source-adapter external-reference key builders.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactEvidenceValidationRules.cs` added for selected required-artifact producer/path/content rules.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs` migrated the execution-artifact projection path through `ProcessArtifactProjectionPlanner.PlanExecutionArtifact` while leaving storage placement and DB recording in the dispatcher.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs` now delegates strong-match selection and source-adapter key construction to the new helpers.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` now delegates selected validation-rule decisions to `ProcessArtifactEvidenceValidationRules`.
