# Source To Subbundle Map

| Source file | Subbundles | Expected action |
| --- | --- | --- |
| `ProcessRunAutomationDispatchService.ToolValidation.cs` | SB02, SB13, SB17-SB40 | Primary target for observation/outcome/completion extraction. |
| `ProcessRunAutomationDispatchService.Concurrency.cs` | SB02, SB15, SB33-SB36 | Observation/no-progress consumer cleanup only. |
| `ProcessRunAutomationDispatchService.ArtifactValidation.cs` | SB02, SB14, SB16 | Observation consumer cleanup only. |
| `ProcessRunAutomationDispatchService.Execution.cs` | SB02, SB15, SB43 | Observation/response consumer cleanup only. |
| `ProcessAutomationReceiptObservationHelper.cs` | SB02, SB13-SB16 | May become thin wrapper over observation snapshot. |
| `ProcessToolReceiptFacts.cs` | SB02, SB05-SB16 | Reuse; do not duplicate normalization rules. |
| tests | all gates | Add focused tests without deleting existing behavior coverage. |
