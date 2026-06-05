# Source Impact Inventory

| File | Expected impact |
| --- | --- |
| `ProcessRunAutomationDispatchService.Dispatch.cs` | Main migration target for pre-execution guard/materialization region. |
| `ProcessDispatchCandidateFactory.cs` | Entry parity check only; should not be changed unless candidate route facts require it. |
| `ProcessDispatchCandidateHydrationLoader.cs` | Entry parity check only; no new side effects. |
| `ProcessDispatchTechnicalAgentBindingCoordinator.cs` | Do not broaden side effects. |
| `ProcessDispatchRecoveryQueryHelper.cs` | May be extended only if recoverable execution query wrapper needs parity fix. |
| `ProcessRunAutomationDispatchService.Concurrency.cs` | Entry smoke only; no broad movement. |
| `ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Entry smoke only; no movement. |
