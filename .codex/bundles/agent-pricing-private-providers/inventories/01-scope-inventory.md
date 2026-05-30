# Scope Inventory

| Area | Files | Notes |
| --- | --- | --- |
| Provider model and metadata | `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderModels.cs`, `repo://src/CanDoItAll.Modules.AgentFramework/Providers/AgentFrameworkProviderMetadata.cs` | Add typed price rows and JSON persistence. |
| Agent save validation | `repo://src/CanDoItAll.AgentFramework.Models/Editors/EditorModels.cs`, agent catalog services | Enforce override price row before saving. |
| Runtime and analytics cost | `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`, `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs` | Calculate cost from usage metrics and provider pricing. |
| UI private badge | `repo://src/CanDoItAll.AgentFramework.Components/AgentSelectionCard.razor`, catalog/switch dialog callers | Render `Private` badge from provider metadata. |
