# Dispatcher Scope Boundary

This bundle must not attempt to decompose the dispatcher. The dispatcher is documented here only to prevent accidental scope creep and to preserve the next refactoring path.

## Dispatcher Partial Inventory Summary

Largest partials by observed line count:

| File | Approx. lines | Bundle action |
| --- | ---: | --- |
| `ProcessRunAutomationDispatchService.ArtifactValidation.cs` | 3,933 | Do not move in this bundle |
| `ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | 2,137 | Do not move in this bundle |
| `ProcessRunAutomationDispatchService.Dispatch.cs` | 1,994 | Do not move in this bundle |
| `ProcessRunAutomationDispatchService.ToolValidation.cs` | 1,992 | Do not move in this bundle |
| `ProcessRunAutomationDispatchService.ArtifactProjection.cs` | 1,699 | Do not move in this bundle |
| `ProcessRunAutomationDispatchService.Concurrency.cs` | 1,477 | Do not move in this bundle |
| `ProcessRunAutomationDispatchService.ExecutionMetadata.cs` | 1,272 | Do not move in this bundle |
| `ProcessRunAutomationDispatchService.ImplementationProof.cs` | 1,221 | Do not move in this bundle |
| `ProcessRunAutomationDispatchService.GovernedRules.cs` | 902 | Inventory only; later driver extraction candidate |
| `ProcessRunAutomationDispatchService.DotnetRunCleanup.cs` | 589 | Inventory only; later DotNet driver extraction candidate |
| `ProcessRunAutomationDispatchService.BrowserProof.cs` | 575 | Inventory only; later BrowserProof driver extraction candidate |
| `ProcessRunAutomationDispatchService.WebHostProof.cs` | 83 | Inventory only; later DotNet/WebHost driver extraction candidate |

## Boundary Rule

This bundle may add tests and documentation around dispatcher behavior, but it must not move dispatcher responsibilities into the new agent tooling seam. The seam is only for runtime tool exposure from modules to MAF.

## Later Work Candidate

After this bundle closes, the next phase can safely plan:

```text
Process contracts/core split
Process agent execution gateway
Process driver pack foundation
DotNet/SWDev/BrowserProof driver extraction
```
