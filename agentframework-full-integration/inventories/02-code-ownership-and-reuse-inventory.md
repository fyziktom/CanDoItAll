# 02 — Code Ownership And Reuse Inventory

## Existing Reuse Candidates

| Helper / service | Path | Reuse intent |
| --- | --- | --- |
| `IClock` | `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.SharedKernel/IClock.cs` | Use for all new timestamps and timeout windows. |
| `IActivityStream` | `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.SharedKernel/ActivityStream.cs` | Emit audit/projection records from new modules. |
| `SecretService` | `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Security/SecurityModels.cs` | Resolve provider credentials in integrated runtime. |
| `IAutomationMessagePublisher` | `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Automation/AutomationMessagingServices.cs` | Durable transport for agent/process orchestration signals. |
| `ProcessOutboxService` | `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Processes/ProcessOutbox.cs` | Reliable boundary between process transaction and side effects. |
| `IProjectPartyIntegrationBridge` | `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs` | Reuse integration-contract pattern for cross-module resource resolution. |
| `IStorageCatalogService` | `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Infrastructure/Storage/Abstractions/StorageContracts.cs` | Canonical managed artifact persistence. |
| `ISearchIndexService` | `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Infrastructure/Search/SearchIndexing.cs` | Search projections for agents/collaboration records. |

## Existing Duplicate Hotspots

| Duplicate hotspot | CanDoItAll path | AgentFramework path | Required action |
| --- | --- | --- | --- |
| Provider model | `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs` | `/mnt/data/work/agentfw/CanDoItAll.AgentFramework-main/src/CanDoItAll.AgentFramework.Models/ProviderModels.cs` | Decide canonical fields and build bridge. |
| Provider runtime | `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Workspace/ProviderExecution.cs` | `/mnt/data/work/agentfw/CanDoItAll.AgentFramework-main/src/CanDoItAll.AgentFramework.Core` | Retire Workspace execution path. |
| AI agent profile vs definition | `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs` | `/mnt/data/work/agentfw/CanDoItAll.AgentFramework-main/src/CanDoItAll.AgentFramework.Models/AgentModels.cs` | Introduce explicit binding, split business vs technical ownership. |
| Agent chat vs human collaboration | none canonical | `/mnt/data/work/agentfw/CanDoItAll.AgentFramework-main/src/CanDoItAll.AgentFramework.Models/ConversationModels.cs` | Add Collaboration module and bridge chat/run context. |
| Artifact evidence | `/mnt/data/work/cando/CanDoItAll-development/src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs` | `/mnt/data/work/agentfw/CanDoItAll.AgentFramework-main/src/CanDoItAll.AgentFramework.Models/ConversationModels.cs` | Build artifact projection bridge. |
