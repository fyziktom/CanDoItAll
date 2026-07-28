# Scope Inventory

## Shared foundation

- `src/Foundation/CanDoItAll.SharedKernel/Activity/ActivityStream.cs` — existing persisted business-activity contract; must remain distinct.
- `src/Foundation/CanDoItAll.SharedKernel` — candidate owner for transport-neutral typed sequenced-stream contracts/primitive.
- `src/Foundation/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs` — profile generation/switch notifications.

## Agent contracts and execution

- `src/MAF/Common/CanDoItAll.AgentFramework.Models/Conversations/ConversationModels.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Models/Conversations/FloatingAgentChatModels.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Models/Runtime/AgentRuntimeContextAssemblyModels.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentChatContextRegistry.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentChatExecutionNotificationHub.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Chat.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Helpers.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/ExecutionEventServices.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/ReferenceData/AgentReferenceDataCache.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/ReferenceData/WorkspaceBackedAgentReferenceDataProvider.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/AgentFrameworkWorkspaceService.cs`

## Runtime and persistence

- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeToolProviderComposer.cs`
- `src/MAF/Skills/CanDoItAll.AgentFramework.Skills/Loading/FileSkillLoader.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceStore.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceChatProjectionStore.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceCrossProcessLock.cs`

## Agent module and UI

- `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentChatExecutionOrchestrator.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentChatPreparationPool.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Services/FloatingAgentChatCoordinator.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/FloatingAgentChatHost.razor.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/AgentFrameworkWorkspaceFactory.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/CurrentProfileAgentFrameworkWorkspaceService.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceBackedAgentProviderProfileRegistry.cs`

## Module context contributors

- `src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureAgentChatContextProvider.razor`
- `src/Modules/CanDoItAll.Modules.Projects/Pages/Components/ProjectsAgentChatContextProvider.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowAgentChatContextProvider.razor`
- `src/Modules/CanDoItAll.Modules.Processes/AgentChat/ProcessAgentChatContextBuilder.cs`
- `src/Modules/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor`
- `src/Modules/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
- Process projection/query stores under `src/Processes`.

## Tests and measurement seams

- `tests/Unit/CanDoItAll.Tests.Unit/FloatingAgentChatArchitectureTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/AgentReferenceDataProviderTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/MafRuntimeArchitectureServicesTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/FloatingAgentChatHostLifecycleTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/AgentChatPanelResponsivenessTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/ProcessWorkspaceShellTests.cs`
- `tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRunTrackingIntegrationTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/FileSandboxWorkspaceChatProjectionStoreTests.cs`
- `tests/Integration/CanDoItAll.Tests.Integration/FileSandboxWorkspaceStoreLockIntegrationTests.cs`

## Documentation and validation

- Product architecture/API/module docs discovered during SB07.
- Relevant API and skill sources under `C:\repositories\CanDoItAll.SharedInfo`.
- `CanDoItAll.slnx`.
- Web host configuration and launcher used for port 5032.
