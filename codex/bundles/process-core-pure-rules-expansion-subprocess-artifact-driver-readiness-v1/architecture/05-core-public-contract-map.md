# Core Public Contract Map

## Approved Namespaces
- `CanDoItAll.Processes.Core.Routing`
- `CanDoItAll.Processes.Core.Subprocess`
- `CanDoItAll.Processes.Core.Artifacts`

## Routing
- `ProcessDispatchRoutePipeline`
- `ProcessDispatchRoutePlanner`
- `ProcessDispatchRouteEligibility`
- route snapshots and route outcome facts from the existing seed

## Subprocess
- `ProcessSubprocessRunFacts`
- `ProcessSubprocessParentTransitionFacts`
- `ProcessSubprocessLifecycleRules`

## Artifacts
- `ProcessCoreArtifactKind`
- `ProcessCoreArtifactTrustRequirement`
- `ProcessCoreArtifactTrustStatus`
- `ProcessCoreSensitivityLevel`
- `ProcessArtifactValidationSnapshot`
- `ProcessArtifactExpectationSnapshot`
- `ProcessArtifactRecordSnapshot`
- `ProcessArtifactExpectationMatcher`
- `ProcessArtifactRecordedSatisfactionRules`
- `ProcessSubprocessArtifactSourceResolver`
- `ProcessSubprocessOutputArtifactMapping`

## Module Adapter Boundary
- `ProcessSubprocessLifecycleRules` remains a module-local compatibility wrapper that maps Core transition facts to `ProcessStepTransitionRequest`.
- `ProcessSubprocessArtifactSourceResolver` remains a module-local compatibility wrapper that maps module entities to Core snapshots and returns module records.
- `ProcessArtifactExpectationMatcher` and `ProcessArtifactRecordedSatisfactionRules` remain module-local compatibility wrappers over Core pure rules.
- `ProcessCoreArtifactModelAdapters` is the only new artifact enum/read-model translation surface.

## Disallowed Contracts
- No `CanDoItAll.Modules.Processes.Core` project.
- No production process-driver interfaces.
- No registry, DI registration, runtime selector, manager command, or execution-capable helper driver.
- No EF, workspace, storage, filesystem, AgentFramework, finalizer application, claim lifecycle, or process mutation dependency inside Core.
