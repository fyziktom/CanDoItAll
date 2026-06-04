# Reviewed source anchors

Observed branch: `processes-hardening`  
Observed head: `0c5876df0fe42ffe3ecd2757257770683a9fb041`

Important paths inspected:

- `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/BuiltInWorkflowExecutorDescriptors.cs`
- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowDefinitionValidator.cs`
- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowCatalogServices.cs`
- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorContracts.cs`
- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs`
- `src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs`
- `src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `src/CanDoItAll.Modules.Plugins/Services/PluginsModuleServiceCollectionExtensions.cs`
- `src/CanDoItAll.Plugins.Abstractions/PluginManifestContracts.cs`
- `src/CanDoItAll.Plugins.Abstractions/PluginManifestValidation.cs`
- `src/plugins/CanDoItAll.Plugin.Gmail/GmailWorkflowExecutor.cs`
- `src/plugins/CanDoItAll.Plugin.Gmail/GmailBundledPlugin.cs`
- `src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs`
- `src/plugins/CanDoItAll.Plugin.Docker/DockerWorkflowExecutors.cs`
- `src/CanDoItAll.Modules.Plugins/Catalog/PluginLogServices.cs`
- `tests/CanDoItAll.Tests.Unit/WorkflowExecutorPolicyObservabilityTests.cs`
- `codex/bundles/workflow-maf-hardening/reviews/02-final-architecture-review.md`

External MAF references checked on 2026-05-29:

- Microsoft Agent Framework Workflows overview
- Workflow Builder & Execution
- Executors
- Human-in-the-loop
- Events
- Checkpoints
- NuGet package page for `Microsoft.Agents.AI.Workflows`
