# Source Impact Inventory

Primary files:

| File | Role | Expected movement |
| --- | --- | --- |
| `ProcessRunAutomationDispatchService.Dispatch.cs` | Main dispatch orchestration | Extract local route/claim/context builders only |
| `ProcessRunAutomationDispatchService.Concurrency.cs` | Execution-run concurrency rules | Extract pure selection rules |
| `ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Finalization orchestration | Touch only context construction if needed |
| `ProcessRunAutomationDispatchService.Execution.cs` | Agent execution loop | Do not expand scope |
| `ProcessRunAutomationDispatchService.ToolValidation.cs` | Required tool and completion rules | Keep stable; only smoke tests |
| `ProcessRunAutomationDispatchService.ArtifactProjection.cs` | Projection side effects | Keep stable |
