# Source surface inventory

## Current feature projects

| Project | Approximate size | Current ownership |
|---|---:|---|
| src/Modules/CanDoItAll.Modules.LlmChats | 49 files / 6.3k lines | Domain, Application, ports, operations, DI |
| src/Modules/CanDoItAll.Modules.LlmChats.Persistence | 32 files / 5.0k lines | EF, transfer, provider runtime, conversation engine, profile/lease infrastructure |
| src/Modules/CanDoItAll.Modules.LlmChats.Ui | 25-29 files / 4.3-4.7k lines | Reusable UI, routed page, navigation, shell adapter |

Counts vary slightly by generated/linked-file exclusion; the ownership conclusion does not.

## Current generic MAF/chat libraries

- src/MAF/Common/CanDoItAll.AgentFramework.Llm.Abstractions
- src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations
- src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime
- src/UI/CanDoItAll.Conversations.Components
- src/UI/CanDoItAll.Conversations.Shell

## High-risk source anchors

- src/Modules/CanDoItAll.Modules.LlmChats/Application
- src/Modules/CanDoItAll.Modules.LlmChats/Operations
- src/Modules/CanDoItAll.Modules.LlmChats/Ports
- src/Modules/CanDoItAll.Modules.LlmChats.Persistence/LlmChatsPersistenceServiceCollectionExtensions.cs
- src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/CanonicalLlmChatProviderResolver.cs
- src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/LlmChatConversationEngine.cs
- src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/AuditedLlmChatInvocationPort.cs
- src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/AuditedLlmChatStreamingInvocationPort.cs
- src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Entities/LlmChatPersistenceRows.cs
- src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatConversationWorkspace.razor
- src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatConversationWorkspaceController.cs
- src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatConversationShellContributor.cs
- src/Modules/CanDoItAll.Modules.LlmChats.Ui/Pages/LlmChatsPage.razor
- src/Modules/CanDoItAll.Modules.LlmChats.Ui/Navigation/LlmChatsShellNavigationContributor.cs

## Agent usage/UI anchors

- src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderUsageModels.cs
- src/MAF/Common/CanDoItAll.AgentFramework.Models/Workspace/AgentOverviewModels.cs
- src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Usage.cs
- src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/ProviderUsageNormalization.cs
- src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/AgentUsageTotalsQueryService.cs
- src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/AgentFrameworkWorkspaceService.cs
- src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceExecutionSliceStore.cs
- src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor
- src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor.cs
- src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentUsageDialog.razor
- src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/ProviderUsageDialog.razor
- src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/ModelUsageDialog.razor

## Composition/caller anchors

- src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs
- src/App/CanDoItAll.Composition/LlmChatOperationDispatcherHostedService.cs
- src/App/CanDoItAll.Composition/ModuleAssemblies.cs
- src/App/CanDoItAll.Web/Program.cs
- src/App/CanDoItAll.Web/Composition/LlmChatsUiComposition.cs
- src/App/CanDoItAll.Web/Api/LlmChat*.cs
- src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs
- src/Foundation/CanDoItAll.Migrations.PostgreSql
- CanDoItAll.slnx

## Baseline graph

No LlmChats project cycle exists. CodeAnalytics reported unrelated existing AgentFramework module/type cycles; SB01 records their exact identities and every checkpoint proves no new cycle/no enlargement.

