# Hotspot Inventory

| File | Current risk | Bundle action |
| --- | --- | --- |
| `ProcessRunAutomationDispatchService.ArtifactValidation.cs` | Large mixed validation/matching rules | Primary target for pure rule extraction |
| `ProcessRunAutomationDispatchService.ArtifactProjection.cs` | Projection orchestration now improved but still large | Consumer proof only; no broad migration |
| `ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Artifact satisfaction and finalization rules | Extract only selected pure validation delegates if needed |
| `ProcessRunAutomationDispatchService.ToolValidation.cs` | Required-tool and receipt validation | Do not migrate; keep as alternate next candidate |
| `ProcessArtifactProjectionWriteCoordinator.cs` | New write boundary | Entry smoke only; do not expand into validation rules |
| `ProcessArtifactEvidenceValidationRules.cs` | Already extracted selected producer/path/content decisions | Candidate to expand or split if it stays cohesive |
