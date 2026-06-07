# Core Public API Inventory

## Scope
- Source root: `repo://src/CanDoItAll.Processes.Core`
- Executable guard: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Guard test: `Process_core_public_api_surface_is_explicitly_guarded`

## Type Surface

| Namespace | Public type | Owner classification |
| --- | --- | --- |
| `CanDoItAll.Processes.Core.Artifacts` | `ProcessArtifactExpectationMatcher` | Pure artifact expectation matching rule |
| `CanDoItAll.Processes.Core.Artifacts` | `ProcessArtifactExpectationSnapshot` | Pure artifact expectation snapshot |
| `CanDoItAll.Processes.Core.Artifacts` | `ProcessArtifactRecordSnapshot` | Pure artifact record snapshot |
| `CanDoItAll.Processes.Core.Artifacts` | `ProcessArtifactRecordedSatisfactionRules` | Pure recorded-satisfaction rule |
| `CanDoItAll.Processes.Core.Artifacts` | `ProcessArtifactValidationSnapshot` | Pure validation snapshot |
| `CanDoItAll.Processes.Core.Artifacts` | `ProcessCoreArtifactKind` | Core artifact kind enum |
| `CanDoItAll.Processes.Core.Artifacts` | `ProcessCoreArtifactTrustRequirement` | Core trust requirement enum |
| `CanDoItAll.Processes.Core.Artifacts` | `ProcessCoreArtifactTrustStatus` | Core trust status enum |
| `CanDoItAll.Processes.Core.Artifacts` | `ProcessCoreSensitivityLevel` | Core sensitivity enum |
| `CanDoItAll.Processes.Core.Artifacts` | `ProcessSubprocessArtifactSourceResolver` | Pure subprocess artifact source resolver |
| `CanDoItAll.Processes.Core.Artifacts` | `ProcessSubprocessOutputArtifactMapping` | Pure subprocess output mapping |
| `CanDoItAll.Processes.Core.Routing` | `ProcessDispatchRouteDecision` | Pure route decision value |
| `CanDoItAll.Processes.Core.Routing` | `ProcessDispatchRouteEligibility` | Pure route eligibility rules |
| `CanDoItAll.Processes.Core.Routing` | `ProcessDispatchRouteKind` | Route decision enum |
| `CanDoItAll.Processes.Core.Routing` | `ProcessDispatchRouteOrderAssertion` | Pure route order guard |
| `CanDoItAll.Processes.Core.Routing` | `ProcessDispatchRoutePipeline` | Canonical route stage order |
| `CanDoItAll.Processes.Core.Routing` | `ProcessDispatchRoutePlanner` | Pure route decision planner |
| `CanDoItAll.Processes.Core.Routing` | `ProcessDispatchRouteSnapshot` | Pure route input snapshot |
| `CanDoItAll.Processes.Core.Routing` | `ProcessDispatchRouteStage` | Route stage enum |
| `CanDoItAll.Processes.Core.Routing` | `ProcessDispatchTriggerFacts` | Pure dispatch trigger facts |
| `CanDoItAll.Processes.Core.Subprocess` | `ProcessSubprocessLifecycleRules` | Pure subprocess lifecycle facts |
| `CanDoItAll.Processes.Core.Subprocess` | `ProcessSubprocessParentTransitionFacts` | Pure parent transition facts |
| `CanDoItAll.Processes.Core.Subprocess` | `ProcessSubprocessRunFacts` | Pure subprocess run facts |

## Stability Rule
- New public Core types, enum values, constructors, properties, or public methods must update the reflection snapshot in `Process_core_public_api_surface_is_explicitly_guarded`.
- Public Core expansion remains allowed only for deterministic rules/read models. EF, workspace/storage/filesystem, AgentFramework execution, claim lifecycle, transition execution, finalizer application, projection persistence, validation orchestration, and production process-driver APIs remain outside Core.
