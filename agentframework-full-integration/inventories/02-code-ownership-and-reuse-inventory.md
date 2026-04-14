# 02 — Code Ownership And Reuse Inventory

## Existing Reuse Candidates

| Helper / service | Path | Reuse intent |
| --- | --- | --- |
| `IClock` | `C:\repositories\CanDoItAll/src/CanDoItAll.SharedKernel/IClock.cs` | Use for all new timestamps and timeout windows. |
| `IActivityStream` | `C:\repositories\CanDoItAll/src/CanDoItAll.SharedKernel/ActivityStream.cs` | Emit audit/projection records from new modules. |
| `SecretService` | `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Security/SecurityModels.cs` | Resolve provider credentials in integrated runtime. |
| `IAutomationMessagePublisher` | `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Automation/AutomationMessagingServices.cs` | Durable transport for agent/process orchestration signals. |
| `ProcessOutboxService` | `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessOutbox.cs` | Reliable boundary between process transaction and side effects. |
| `IProjectPartyIntegrationBridge` | `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs` | Reuse integration-contract pattern for cross-module resource resolution. |
| `IStorageCatalogService` | `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Storage/Abstractions/StorageContracts.cs` | Canonical managed artifact persistence. |
| `ISearchIndexService` | `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Search/SearchIndexing.cs` | Search projections for agents/collaboration records. |

## Existing Duplicate Hotspots

| Duplicate hotspot | CanDoItAll path | AgentFramework path | Required action |
| --- | --- | --- | --- |
| Provider model | `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs` | `C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Models/ProviderModels.cs` | Decide canonical fields and build bridge. |
| Provider runtime | `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/ProviderExecution.cs` | `C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Core` | Retire Workspace execution path. |
| AI agent profile vs definition | `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs` | `C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Models/AgentModels.cs` | Introduce explicit binding, split business vs technical ownership. |
| Agent chat vs human collaboration | none canonical | `C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Models/ConversationModels.cs` | Add Collaboration module and bridge chat/run context. |
| Artifact evidence | `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs` | `C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Models/ConversationModels.cs` | Build artifact projection bridge. |

