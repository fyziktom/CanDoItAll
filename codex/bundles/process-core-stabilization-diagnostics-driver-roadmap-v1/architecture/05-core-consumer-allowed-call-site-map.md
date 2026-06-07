# Core Consumer Allowed Call-Site Map

## Scope
This map defines where `CanDoItAll.Modules.Processes` may reference `CanDoItAll.Processes.Core`.
It is intentionally file-based and exact; wildcard directory exemptions are not allowed.

## Project-Level References
| File | Allowed Reference | Reason |
| --- | --- | --- |
| `src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj` | `CanDoItAll.Processes.Core` project reference | Required so the Processes module can adapt module runtime models to pure Core rules. |

## Global Usings
| File | Policy |
| --- | --- |
| `src/CanDoItAll.Modules.Processes/GlobalUsings.cs` | Must not contain `CanDoItAll.Processes.Core`; Core consumption must be visible in the local file. |

## Dispatch Core Consumers
| File | Allowed Core Area | Reason |
| --- | --- | --- |
| `ProcessArtifactExpectationMatcher.cs` | Artifacts | Adapter wrapper for expected-artifact match diagnostics. |
| `ProcessArtifactExpectationSatisfactionAdapter.cs` | Artifacts | Adapter wrapper for trust/sensitivity satisfaction rules. |
| `ProcessArtifactRecordedSatisfactionRules.cs` | Artifacts | Rule wrapper for recorded expected-artifact satisfaction. |
| `ProcessArtifactValidationDescriptorAdapter.cs` | Artifacts | Adapter wrapper for projection/validation descriptors and producer policy. |
| `ProcessCoreArtifactModelAdapters.cs` | Artifacts | Converts module artifact entities/snapshots to Core snapshots. |
| `ProcessSubprocessArtifactSourceResolver.cs` | Artifacts | Adapter wrapper for subprocess source artifact diagnostics and mappings. |
| `ProcessSubprocessLifecycleRules.cs` | Subprocess | Adapter wrapper for subprocess parent transition facts. |
| `ProcessTransitionIntentAdapters.cs` | Routing/Subprocess | Converts Core transition intent/facts to module transition requests. |
| `ProcessDispatchCandidateHeaderSelector.cs` | Routing | Uses route eligibility facts while selecting candidate headers. |
| `ProcessDispatchCandidateHydrationLoader.cs` | Routing | Uses route eligibility facts while hydrating dispatch candidates. |
| `ProcessDispatchRouteExecutionModels.cs` | Routing | Builds route snapshots for the route pipeline. |
| `ProcessDispatchRouteFacets.cs` | Routing | Defines route handler stage contract. |
| `ProcessDispatchRouteHandlerPipeline.cs` | Routing | Validates route handler order against Core stage order. |
| `ProcessDispatchRouteHandlers.cs` | Routing | Executes route stages using pure Core route decisions. |
| `ProcessDispatchRouteModelAdapters.cs` | Routing | Converts module dispatch candidates/claims/outcomes into Core route snapshots. |
| `ProcessDispatchRunClosureGuardService.cs` | Routing | Uses route closed/eligible facts while checking run closure. |
| `ProcessDispatchStartTransitionPlanner.cs` | Routing | Converts Core start-transition intent to a module transition request. |
| `ProcessRunAutomationDispatchService.Concurrency.cs` | Routing | Uses route eligibility facts during concurrency/candidate checks. |

## Denied Consumers
- UI, component, canvas, persistence configuration, import/export, template, and runtime service files outside the explicit dispatch list must not reference Core.
- Core references must not be hidden through project-wide global usings.
- No production driver, driver registry, or driver pack API may be introduced as a Core consumer.
