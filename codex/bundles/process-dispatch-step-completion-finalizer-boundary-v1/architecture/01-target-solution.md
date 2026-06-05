# Target Solution

The target is a module-local finalizer boundary, not Process Core.

## Desired shape

`ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` should become primarily an orchestration partial:

- load current step snapshot,
- call artifact projection when needed,
- call validation orchestration,
- optionally trigger manager recovery,
- call invariant audit,
- build transition request,
- apply transition through existing dispatcher service path.

Supporting responsibilities should move into small module-local files:

- `ProcessRunAutomationDispatchService.StepCompletionFinalizer.Types.cs`
- `ProcessRunAutomationDispatchService.StepCompletionFinalizer.ArtifactContentReaders.cs`
- `ProcessRunAutomationDispatchService.StepCompletionFinalizer.ValidationOrchestration.cs`
- `ProcessRunAutomationDispatchService.StepCompletionFinalizer.RuntimeInvariantAudit.cs`
- `ProcessRunAutomationDispatchService.StepCompletionFinalizer.TransitionRequestBuilder.cs`
- `ProcessFinalizerDriverReadinessMap.md` under bundle architecture/inventories, documentation-only.

The extracted source files remain `internal` members of the existing `ProcessRunAutomationDispatchService` partial. This preserves the current nested type names used by tests and runtime callers while reducing the orchestration file.

## Non-goals

- No Process Core extraction.
- No driver-pack implementation.
- No public contracts or API exposure.
- No migration of EF or storage implementation out of the module.
- No public type promotion for finalizer vocabulary.
