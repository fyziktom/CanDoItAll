# Process Module Core Descriptor Consumer Map

## Scope
This map covers production files under `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch` that directly consume `CanDoItAll.Processes.Core`.

Core consumer count: `25`

Global using policy: no production `global using CanDoItAll.Processes.Core` or `global using CanDoItAll.Processes.Drivers`.

## Allowed Consumers
| File | Core family | Boundary role |
| --- | --- | --- |
| `ProcessArtifactEvidenceReadOnlyAdapter.cs` | Artifacts | Converts supplied artifact evidence into read-only driver payloads. |
| `ProcessArtifactExpectationMatcher.cs` | Artifacts | Module-local adapter over pure Core artifact expectation matching. |
| `ProcessArtifactExpectationSatisfactionAdapter.cs` | Artifacts | Module-local adapter over pure Core artifact satisfaction diagnostics. |
| `ProcessArtifactProjectionEvidenceDescriptorAdapter.cs` | Artifacts | Maps process projection lineage to Core projection evidence descriptors. |
| `ProcessArtifactRecordedSatisfactionRules.cs` | Artifacts | Uses Core recorded-satisfaction rules without persistence. |
| `ProcessArtifactValidationDescriptorAdapter.cs` | Artifacts | Maps process artifact validation state to Core descriptor vocabulary. |
| `ProcessCoreArtifactModelAdapters.cs` | Artifacts | Converts process artifact records and expectations to Core snapshots. |
| `ProcessDispatchCandidateHeaderSelector.cs` | Routing | Uses Core route decisions while keeping data access in the module. |
| `ProcessDispatchCandidateHydrationLoader.cs` | Routing | Consumes Core routing facts at the hydration edge. |
| `ProcessDispatchRouteExecutionModels.cs` | Routing | Carries Core route snapshots as module execution facts. |
| `ProcessDispatchRouteFacets.cs` | Routing | Shapes Core route facets for process dispatch orchestration. |
| `ProcessDispatchRouteHandlerPipeline.cs` | Routing | Sequences Core routing stages without adding runtime driver behavior. |
| `ProcessDispatchRouteHandlers.cs` | Routing | Applies Core route decisions at module application edges. |
| `ProcessDispatchRouteModelAdapters.cs` | Routing | Converts module dispatch models to and from Core route models. |
| `ProcessDispatchRunClosureGuardService.cs` | Routing | Uses Core route diagnostics for read-only closure checks. |
| `ProcessDispatchStartTransitionPlanner.cs` | Routing | Builds transition intent from Core routing rules. |
| `ProcessExecutionEvidenceDescriptorAdapter.cs` | Execution | Maps execution details to Core execution evidence descriptors. |
| `ProcessFinalizerEvidenceDescriptorAdapter.cs` | Finalization | Maps finalizer context and result facts to Core finalizer descriptors. |
| `ProcessReadOnlyVerificationPayloadBuilder.cs` | Artifacts, Diagnostics, Execution, Finalization | Builds supplied verification payloads from already-resolved process facts. |
| `ProcessRetryDiagnosticDescriptorAdapter.cs` | Diagnostics | Maps retry and provider repair facts to Core retry descriptors. |
| `ProcessRunAutomationDispatchService.Concurrency.cs` | Routing | Uses Core routing facts for concurrency-sensitive dispatch decisions. |
| `ProcessRuntimeEvidenceVerificationReadOnlyAdapter.cs` | Artifacts, Diagnostics, Execution, Finalization | Verifies supplied Core descriptors through the read-only gateway. |
| `ProcessSubprocessArtifactSourceResolver.cs` | Artifacts | Adapts subprocess artifact source resolution to pure Core rules. |
| `ProcessSubprocessLifecycleRules.cs` | Subprocess | Uses Core subprocess lifecycle rules at the module boundary. |
| `ProcessTransitionIntentAdapters.cs` | Routing, Subprocess | Converts Core transition intents to module transition requests. |

## Denied Drift
- `ProcessDomainEvidenceReadOnlyAdapters.cs` is a source-reference marker only and is not an approved Core or driver consumer.
- Adding Core usage to any unlisted process dispatch file must update this map and the architecture tests in the same change.
- Adding production global usings for Core or driver namespaces is denied because it makes file-level ownership unreviewable.
- Core must not reference any `CanDoItAll.Processes.Drivers.*` namespace or project.
