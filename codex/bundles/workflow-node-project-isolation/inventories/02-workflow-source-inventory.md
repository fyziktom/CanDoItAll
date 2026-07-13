# Workflow Source Inventory

| Surface | Current reference | Target owner | Owning subbundle |
| --- | --- | --- | --- |
| Workflow models | `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs` | Models or Workflows.Abstractions if proven needed | SB02 |
| Executor models | `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs` | Models plus executor abstractions adapters | SB02, SB06 |
| Workflow catalog contracts | `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowCatalogContracts.cs` | Workflows.Abstractions | SB02 |
| Runtime/store contracts | `repo://src/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowContracts.cs` | Workflows.Runtime | SB02, SB04 |
| Executor contracts | `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorContracts.cs` | WorkflowExecutors.Abstractions/Core | SB06 |
| Definition validator | `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowDefinitionValidator.cs` | Workflows.Core | SB03 |
| Catalog services | `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowCatalogServices.cs` | Workflows.Core | SB03 |
| Runtime manager | `repo://src/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowRuntimeManager.cs` | Workflows.Runtime | SB04 |
| Routing compiler | `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRoutingCompiler.cs` | Workflows.Core | SB03 |
| Preview simulation renderer | `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowPreviewSimulationRenderer.cs` | Workflows.Core or WorkflowExecutors.Core | SB03, SB06 |
| Payload policy | `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowPayloadPolicyService.cs` | Workflows.Core or WorkflowExecutors.Core | SB03, SB06 |
| Artifact content stores | `repo://src/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowArtifactContentStores.cs` | Workflows.Runtime | SB04 |
| External request runtime | `repo://src/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowExternalRequestRuntime.cs` | Workflows.Runtime | SB04 |
| MAF compiler | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs` | Workflows.MafAdapter | SB11 |
| MAF in-process backend | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs` | Workflows.MafAdapter | SB11 |
| LLM component invoker | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs` | Workflows.MafAdapter | SB11 |
| Event normalizer | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowEventNormalizer.cs` | Workflows.MafAdapter or Workflows.Runtime | SB11 |
| Handoff factory | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafHandoffWorkflowFactory.cs` | Workflows.MafAdapter | SB11 |
| Persistent workflow stores | `repo://src/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs` | Workflows.Persistence | SB04, SB12 |
| Runtime evidence source | `repo://src/CanDoItAll.Modules.AgentFramework/Persistence/WorkflowRuntimeEvidenceSourceProvider.cs` | Workflows.Persistence | SB04 |
| Workflow API | `repo://src/CanDoItAll.Web/Api/WorkflowsApi.cs` | Web consuming workflow services | SB12 |
| Workflow page/canvas | `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`, `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs` | UI consuming isolated services | SB12 |
| Workbench workflow nodes | `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureWorkflowNodeService.cs` | Workbench consuming isolated runtime/catalog services | SB12 |
| Workbench agent workflow tools | `repo://src/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs` | Workbench agent-tool API consuming isolated workflow runtime and project-structure workflow node services | SB12, SB13 |
| Scheduler workflow input options | `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerWorkflowInputSchemaService.cs`, `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerWorkflowInputOptionService.cs`, `repo://src/CanDoItAll.Composition/SchedulerPlannerWorkflowInputOptionProviders.cs` | Scheduler/composition consumers of workflow input option contracts and template/runtime metadata | SB10, SB12 |
| Cognitive Memory workflow executors | `repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs` | Feature-module executor source consuming executor abstractions without MAF/Core executor-contract ownership | SB06, SB09 |

## SB02 Execution Update

- Existing workflow serialized model contracts remain in `repo://src/CanDoItAll.AgentFramework.Models/Workflows` for compatibility.
- New workflow-owned abstractions are in `repo://src/CanDoItAll.AgentFramework.Workflows.Abstractions`.
- New workflow-owned builders and deterministic fixtures are in `repo://src/CanDoItAll.AgentFramework.Workflows.Builder`.
- Focused SB02 coverage is in `repo://tests/CanDoItAll.Tests.Unit/WorkflowAbstractionsBuilderTests.cs`.

## SB03 Execution Update

- Workflow core services moved to `repo://src/CanDoItAll.AgentFramework.Workflows.Core`.
- Moved implementation files: `WorkflowDefinitionValidator.cs`, `WorkflowCatalogServices.cs`, `WorkflowRoutingCompiler.cs`, `WorkflowPreviewSimulationRenderer.cs`, `WorkflowPayloadPolicyService.cs`, `WorkflowFailureDisplayFormatter.cs`, and `WorkflowProcessExecutorBridge.cs`.
- Added workflow core registration in `WorkflowCoreServiceCollectionExtensions.cs`; Hosting and `CanDoItAll.Modules.AgentFramework` now call `AddWorkflowCoreServices()`.
- Added typed validation diagnostic mapping in `WorkflowFailureDiagnosticMapper.cs`; catalog validation failures preserve exact `InvalidOperationException` compatibility and attach typed diagnostic envelopes.
- Runtime manager, runtime stores, external request runtime, execution backend contracts, and executor contracts remain in their current locations for SB04 and SB06.
- Focused SB03 coverage is in `repo://tests/CanDoItAll.Tests.Unit/WorkflowCoreExtractionTests.cs`.

## SB04 Execution Update

- Workflow runtime and store contracts moved to `repo://src/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowContracts.cs`.
- Runtime manager, in-memory run store, null event sink, external request runtime, artifact content stores, event payload helpers, and node execution progress scope moved to `repo://src/CanDoItAll.AgentFramework.Workflows.Runtime`.
- Runtime registration is centralized in `repo://src/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowRuntimeServiceCollectionExtensions.cs`.
- Runtime diagnostics are in `repo://src/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowRuntimeFailureDiagnostics.cs`.
- Persistent workflow stores remain in `repo://src/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs` until persistence adoption can be separated from the module DbContext.
- Focused SB04 coverage is in `repo://tests/CanDoItAll.Tests.Unit/WorkflowRuntimeExtractionTests.cs`.
