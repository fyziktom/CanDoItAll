# Core Adapter Ownership Map

## Scope
- This map defines every `CanDoItAll.Modules.Processes/Automation/Dispatch` file that may reference `CanDoItAll.Processes.Core`.
- It is intentionally exact. Wildcard directory exemptions are denied.
- Runtime side-effect files may call module adapters, but they must not import Core directly.

## Project-Level Reference
| File | Allowed Reference | Reason |
| --- | --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj` | `CanDoItAll.Processes.Core` project reference | Required so the Processes module can adapt module runtime models to pure Core rules. |

## Global Usings
| File | Policy |
| --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/GlobalUsings.cs` | Must not contain `CanDoItAll.Processes.Core`; Core consumption must stay visible in local adapter/route files. |

## Dispatch Core Consumers
| File | Core Area | Owner Contract |
| --- | --- | --- |
| `ProcessArtifactExpectationMatcher.cs` | Artifacts | Expected-artifact match diagnostics adapter. |
| `ProcessArtifactExpectationSatisfactionAdapter.cs` | Artifacts | Trust/sensitivity satisfaction adapter. |
| `ProcessArtifactProjectionEvidenceDescriptorAdapter.cs` | Artifacts | Projection source order, lineage, and provider-native browser evidence descriptor adapter. |
| `ProcessArtifactRecordedSatisfactionRules.cs` | Artifacts | Recorded expected-artifact satisfaction adapter. |
| `ProcessArtifactValidationDescriptorAdapter.cs` | Artifacts | Projection eligibility, validation requirement, and producer policy adapter. |
| `ProcessCoreArtifactModelAdapters.cs` | Artifacts | Shared artifact/source/producer model conversion adapter. |
| `ProcessSubprocessArtifactSourceResolver.cs` | Artifacts | Subprocess source artifact diagnostics and mapping adapter. |
| `ProcessSubprocessLifecycleRules.cs` | Subprocess | Subprocess parent transition facts adapter. |
| `ProcessTransitionIntentAdapters.cs` | Routing/Subprocess | Core transition intent/facts to module transition request adapter. |
| `ProcessDispatchCandidateHeaderSelector.cs` | Routing | Route eligibility facts while selecting candidate headers. |
| `ProcessDispatchCandidateHydrationLoader.cs` | Routing | Route eligibility facts while hydrating dispatch candidates. |
| `ProcessDispatchRouteExecutionModels.cs` | Routing | Route snapshot construction for the route pipeline. |
| `ProcessDispatchRouteFacets.cs` | Routing | Route handler stage contract. |
| `ProcessDispatchRouteHandlerPipeline.cs` | Routing | Route handler order validation against Core stage order. |
| `ProcessDispatchRouteHandlers.cs` | Routing | Route stages using pure Core route decisions. |
| `ProcessDispatchRouteModelAdapters.cs` | Routing | Module dispatch candidates/claims/outcomes to Core route snapshots. |
| `ProcessDispatchRunClosureGuardService.cs` | Routing | Route closed/eligible facts while checking run closure. |
| `ProcessDispatchStartTransitionPlanner.cs` | Routing | Core start-transition intent to module transition request. |
| `ProcessExecutionEvidenceDescriptorAdapter.cs` | Execution | Execution run and post-attempt facts to Core execution evidence descriptors. |
| `ProcessFinalizerEvidenceDescriptorAdapter.cs` | Finalization | Finalizer context/result facts to Core finalizer evidence descriptors. |
| `ProcessRetryDiagnosticDescriptorAdapter.cs` | Diagnostics | Retry, no-progress, and provider repair facts to Core diagnostic descriptors. |
| `ProcessRunAutomationDispatchService.Concurrency.cs` | Routing | Route eligibility facts during concurrency/candidate checks. |

## Denied Consumers
- UI, component, canvas, persistence configuration, import/export, template, runtime service, storage, provider, and validation orchestration files outside the explicit dispatch list must not reference Core.
- Core references must not be hidden through project-wide global usings.
- No production process-driver pack, registry, selector, manager command, or DI runtime selector may be introduced as a Core consumer in this bundle.
