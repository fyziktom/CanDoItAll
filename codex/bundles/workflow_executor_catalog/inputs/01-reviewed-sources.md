# Reviewed source references

Comparison:
- Base: `0c5876df0fe42ffe3ecd2757257770683a9fb041`
- Head: `processes-hardening`

Key reviewed files:
- `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowDefinitionValidator.cs`
- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs`
- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowContracts.cs`
- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowPayloadPolicyService.cs`
- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExternalRequestRuntime.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/BuiltInWorkflowExecutorDescriptors.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/WorkspaceFileWorkflowExecutor.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/SourceIngestionWorkflowExecutor.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/HttpFetchWorkflowExecutor.cs`
- `src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs`
- `src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`
- `src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `src/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs`
- `Templates/Workflows/manifest.yaml`
- `codex/bundles/workflow-maf-hardening-followup/reviews/02-final-architecture-review.md`

External reference:
- NuGet `Microsoft.Agents.AI.Workflows` latest checked as `1.8.0` on 2026-05-29.
- Microsoft Agent Framework workflow docs for workflows, events, HITL, and checkpoints.
