# Source Assertions

## Assertions

- `INV-API-PRICE-EXACT`: `repo://src/CanDoItAll.Modules.Workspace/Providers/ProviderExecution.cs` adds `IProviderModelPricingSource`, OpenAI-compatible `DiscoverModelPricingAsync`, and parser support for canonical per-million-token price fields.
- `INV-MODEL-NAME-MANUAL`: `repo://src/CanDoItAll.Modules.Workspace/Providers/ProviderExecution.cs` adds Ollama `/api/tags` model discovery that returns `ProviderDiscoveredModelPrice` rows without explicit prices.
- `INV-MANUAL-PRESERVE`: `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs` adds `MergeDiscoveredModelPrices`; model-name-only rows add defaults only for missing models, while non-discovered manual rows stay in the merged dictionary.
- `INV-WORKSPACE-ORCHESTRATION`: `repo://src/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs` adds `RefreshProviderModelPricesAsync`, resolves provider adapters/secrets, calls the typed pricing source, and returns editor rows instead of doing HTTP in Razor.
- `INV-UI-PARITY`: `repo://src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor` and `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/ProviderManagementPanel.razor` both pass `RefreshProviderModelPricesAsync` to `ProviderModelPricingEditor`.
- `INV-UI-AFFORDANCE`: `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/ProviderModelPricingEditor.razor` adds the `Load from provider` button while keeping add, remove, reset, and editable price inputs.
- `INV-TEST-PROOF`: `repo://tests/CanDoItAll.Tests.Unit/ProviderPricingTests.cs` covers explicit OpenAI-compatible pricing metadata, missing OpenAI secret, Ollama model-name-only discovery, and manual row preservation.

## Source Assertion Command

`rg -n "ProviderDiscoveredModelPrice|MergeDiscoveredModelPrices|DiscoverModelPricingAsync|RefreshProviderModelPricesAsync|Load from provider|provider-pricing-refresh-button|Discovered_prices_override|OpenAi_pricing_discovery|Ollama_pricing_discovery" <changed files> -S`

Result: all assertion anchors were found in changed source or test files.
