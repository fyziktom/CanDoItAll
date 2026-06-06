# Current State Review

## Completed previous step

The previous bundle replaced the broad projection-host interface with a set of module-local facets. This is a meaningful improvement because projection source coordinators no longer consume a single large host contract.

## Current residual issue

The current branch still has a single dispatcher-backed services implementation:

`src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionServices.cs`

That class is nested inside `ProcessRunAutomationDispatchService`, implements all projection facets, and mostly forwards calls back to static or instance methods on the dispatcher service.

This is acceptable as a temporary adapter, but it should not become the long-term seam before a future process-core split.

## Why Process Core is still premature

The projection boundary is getting better, but the implementation still depends on dispatcher nested models and dispatcher wrappers. Moving this to `Process Core` now would either:
- drag dispatcher types into the core,
- create a fake core containing adapter code,
- or force too much behavior movement in one risky step.

The safe next step is to split the projection facet implementations module-locally, without public API changes.

## Key source hotspots

- `ProcessArtifactProjectionFacets.cs`
- `ProcessRunAutomationDispatchService.ArtifactProjectionServices.cs`
- `ProcessArtifactProjectionOrchestrator.cs`
- `ProcessExecutionArtifactProjectionCoordinator.cs`
- `ProcessMockArtifactProjectionCoordinator.cs`
- `ProcessWorkspaceWrittenArtifactProjectionCoordinator.cs`
- `ProcessExistingManagedArtifactProjectionCoordinator.cs`
- `ProcessResponseTextArtifactProjectionCoordinator.cs`
- `ProcessProviderNativeBrowserArtifactProjectionCoordinator.cs`
- `ProcessCompletedDecisionArtifactCoordinator.cs`
- `ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `ProcessRunAutomationDispatchService.ArtifactValidation.cs`

## Desired architectural trend

Move from:

```text
source coordinator -> many tiny facets -> one giant nested dispatcher-backed implementation -> dispatcher methods
```

toward:

```text
source coordinator -> precise facet -> focused module-local implementation -> pure helper/coordinator where possible
```

Only after that should the team reassess whether a small `Processes.Core` extraction is safe.
