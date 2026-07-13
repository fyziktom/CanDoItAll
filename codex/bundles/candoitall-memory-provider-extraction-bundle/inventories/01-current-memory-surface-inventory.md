# Current Memory Surface Inventory

| Surface | Current source | Future owner |
| --- | --- | --- |
| Native recall contracts and services | `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Recall` | Native service application, exposed through generic protocol. |
| Native ingestion services | `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Ingestion` | Split: generic Source Gateway in main repo, native ingestion in native service. |
| Native quality/dream/cluster logic | `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Quality` | Native service application/domain. |
| Native scoring geometry | `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Scoring` | Native service domain/application. |
| Native signals and accepted-use feedback | `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Signals` | Native service plus generic feedback/event bridge. |
| Native UI tabs/pages | `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Pages` | Provider-specific RCL or native standalone UI. |
| Review UI | `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/ReviewUi` | Native provider UI surface. |
| Current MAF contributor/executors | `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs` | Generic MAF memory package, native-specific logic removed. |
| Current MAF context contribution seam | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentContextContributionContracts.cs`, `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentContextContributionProvider.cs` | Generic memory context contributor using current MAF bridge. |
| Current MAF runtime tool seam | `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs`, `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderContext.cs` | Generic memory tool provider integrated through existing runtime tool provider pipeline. |
| Current MAF workflow executor seam | `repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions/WorkflowExecutorContracts.cs`, `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs` | Generic memory workflow executor and descriptor source. |
| Current source snapshot contracts | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Sources/MemorySourceSnapshotContracts.cs` | Rehome, wrap, or explicitly migrate into Source Gateway; do not duplicate. |
| Current Workbench source provider | `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/WorkbenchProjectStructureSourceSnapshotProvider.cs` | Source Gateway adapter input for project/workbench snapshots. |
| Current Workflow source provider | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/WorkflowRuntimeEvidenceSourceProvider.cs` | Source Gateway adapter input for workflow runtime snapshots. |
| Current Process source placeholder | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/UnavailableProcessRuntimeEvidenceSourceProvider.cs` | Must be replaced or extended when process runtime source snapshots are required. |
| Current host API | `repo://src/App/CanDoItAll.Web/Api/CognitiveMemoryApi.cs` | Generic Memory API in main host plus native service API. |
| Current DB model registration | `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContextModelRegistry.cs` | Generic metadata only in main DB; native records moved to native DB. |
