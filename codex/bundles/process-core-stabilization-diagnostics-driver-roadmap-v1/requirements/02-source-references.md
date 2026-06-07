# Exact Source References

## Current Core
- `src/CanDoItAll.Processes.Core/CanDoItAll.Processes.Core.csproj`
- `src/CanDoItAll.Processes.Core/Routing/ProcessDispatchRoutePipeline.cs`
- `src/CanDoItAll.Processes.Core/Routing/ProcessDispatchRouteSnapshot.cs`
- `src/CanDoItAll.Processes.Core/Routing/ProcessDispatchRoutePlanner.cs`
- `src/CanDoItAll.Processes.Core/Subprocess/ProcessSubprocessLifecycleRules.cs`
- `src/CanDoItAll.Processes.Core/Artifacts/ProcessCoreArtifactModels.cs`
- `src/CanDoItAll.Processes.Core/Artifacts/ProcessSubprocessArtifactSourceResolver.cs`
- `src/CanDoItAll.Processes.Core/Artifacts/ProcessArtifactExpectationMatcher.cs`

## Process module adapter/application edges
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessCoreArtifactModelAdapters.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerAdapter.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerApplicationService.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationService.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.DotnetRunCleanup.cs`

## Tests / guardrails
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Prior bundle proof to review
- `codex/bundles/process-core-pure-rules-expansion-subprocess-artifact-driver-readiness-v1/reviews/01-execution-report.md`
- `codex/bundles/process-core-pure-rules-expansion-subprocess-artifact-driver-readiness-v1/reviews/02-final-red-team-review.md`
- `codex/bundles/process-core-pure-rules-expansion-subprocess-artifact-driver-readiness-v1/proof/shared/transcripts/build.txt`
- `codex/bundles/process-core-pure-rules-expansion-subprocess-artifact-driver-readiness-v1/proof/shared/transcripts/core-forbidden-scan.txt`
