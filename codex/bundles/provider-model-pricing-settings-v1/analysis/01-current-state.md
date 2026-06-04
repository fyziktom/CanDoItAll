# Current State

## Repo Observations

- `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs` already defines `ProviderModelTokenPrice`, editor models, hardcoded OpenAI/private defaults, normalization, validation, metadata round-trip, and runtime cost calculation.
- `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/ProviderModelPricingEditor.razor` already renders manual model price rows and supports add/remove/reset defaults.
- `repo://src/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs` persists model prices inside `ProviderPricingMetadata` in `ProviderProfile.ExtraSettingsJson`.
- `repo://src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs` and `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/ProviderManagementPanel.razor.cs` duplicate provider-editor normalization and both invoke `ProviderModelPricingEditor`.
- `repo://src/CanDoItAll.Modules.Workspace/Providers/ProviderExecution.cs` owns provider adapter boundaries. OpenAI health already calls `GET /models`; Ollama health already calls `GET /api/tags`.
- No existing provider adapter method loads model prices or model rows into provider settings.

## Gap

Manual prices exist, but provider settings have no supported way to ask provider APIs for model/pricing data. OpenAI-compatible APIs with explicit pricing metadata are unused, and local LLM users must manually type each model row even when Ollama can list installed model names.
