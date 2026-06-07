# Exact Source References

## Existing Core seed
- `src/CanDoItAll.Processes.Core/CanDoItAll.Processes.Core.csproj`
- `src/CanDoItAll.Processes.Core/Routing/ProcessDispatchRoutePipeline.cs`
- `src/CanDoItAll.Processes.Core/Routing/ProcessDispatchRoutePlanner.cs`
- `src/CanDoItAll.Processes.Core/Routing/ProcessDispatchRouteSnapshot.cs`

## Existing process module pure-rule candidates
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessLifecycleRules.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessArtifactSourceResolver.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactValidationSnapshot.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactExpectationMatcher.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactExpectationMatcher.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactSatisfactionSnapshot.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactRecordedSatisfactionRules.cs`

## Application-local boundaries that must not move to Core
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationService.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionPersistenceService.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerApplicationService.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerAdapter.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentRuntimeService.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentExecutionAdapter.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`

## Tests
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
