# Semantic Invariant Contract

## Invariant INV-API-PRICE-EXACT

- Invariant ID: `INV-API-PRICE-EXACT`
- Source raw note: `N003`
- Expected behavior: When a supported provider API returns canonical per-million-token input, cached-input, and output price fields, refresh produces exact `ProviderModelTokenPrice` rows.
- Disallowed shallow implementation: A button that only resets hardcoded defaults or a parser that treats model-name discovery as exact pricing.
- Failing-first test: explicit process/non-production exemption; pre-change source had no provider pricing refresh method.
- Passing test: `OpenAi_pricing_discovery_reads_explicit_price_metadata`
- Changed source files: `repo://src/CanDoItAll.Modules.Workspace/Providers/ProviderExecution.cs`, `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs`
- Production assertions: OpenAI-compatible adapter implements `IProviderModelPricingSource`; merge consumes `ProviderDiscoveredModelPrice` and produces runtime `ProviderModelTokenPrice`.
- Red-team negative case: `OpenAi_pricing_discovery_requires_secret`
- Downstream dependency check: Runtime cost calculators continue to consume `ProviderProfile.ModelPrices` without source-specific branching.

## Invariant INV-MODEL-NAME-MANUAL

- Invariant ID: `INV-MODEL-NAME-MANUAL`
- Source raw note: `N003`, `N005`
- Expected behavior: Provider APIs that return model names without prices create or preserve editable model rows and report that exact API pricing was not returned.
- Disallowed shallow implementation: Inventing exact prices from local model names or silently setting zero-price local rows.
- Failing-first test: explicit process/non-production exemption; pre-change source had no provider pricing refresh method.
- Passing test: `Ollama_pricing_discovery_returns_model_names_without_prices`
- Changed source files: `repo://src/CanDoItAll.Modules.Workspace/Providers/ProviderExecution.cs`, `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs`
- Production assertions: Ollama discovery returns `ProviderDiscoveredModelPrice` without explicit prices; merge creates manual price templates only for missing discovered models.
- Red-team negative case: the Ollama test asserts every discovered row has `HasExplicitPrices == false`.
- Downstream dependency check: Local LLM costs remain user-owned provider settings.

## Invariant INV-MANUAL-PRESERVE

- Invariant ID: `INV-MANUAL-PRESERVE`
- Source raw note: `N004`
- Expected behavior: Refresh preserves non-discovered manual rows and overrides only the same model when exact API prices are present.
- Disallowed shallow implementation: Replacing the entire pricing list with discovered API rows.
- Failing-first test: explicit process/non-production exemption; pre-change source had only manual/default rows and no refresh merge path.
- Passing test: `Discovered_prices_override_same_model_but_preserve_manual_rows`
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs`
- Production assertions: `MergeDiscoveredModelPrices` starts from normalized configured prices and mutates by model key.
- Red-team negative case: the merge test includes a `manual-only` row that is not returned by discovery and remains priced after refresh.
- Downstream dependency check: Process analytics and runtime usage pricing keep access to existing manual profile pricing.

## Invariant INV-UI-PARITY

- Invariant ID: `INV-UI-PARITY`
- Source raw note: `N001`, `N002`
- Expected behavior: Provider settings expose a refresh action next to editable model price rows, and both settings hosts route to the same service method.
- Disallowed shallow implementation: Adding refresh to only one settings host or placing HTTP logic in the Razor component.
- Failing-first test: explicit process/non-production exemption; browser/source inspection established the missing visible refresh control before this change.
- Passing test: browser proof in `bundle://proof/SB01/transcripts/browser-proof.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/ProviderModelPricingEditor.razor`, `repo://src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor`, `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/ProviderManagementPanel.razor`, `repo://src/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs`
- Production assertions: `ProviderModelPricingEditor` exposes `Load from provider`; both hosts pass `RefreshProviderModelPricesAsync`; service owns adapter/secret/merge orchestration.
- Red-team negative case: source assertions verify no `TODO`, `NotImplemented`, fake refresh, or stub markers in changed files.
- Downstream dependency check: Provider settings and runtime provider metadata continue to use existing save/persist flow.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| `ProviderDiscoveredModelPrice` | Provider adapters in `ProviderExecution.cs` | `ProviderPricingDefaults.MergeDiscoveredModelPrices` | Created during refresh, not persisted directly | `Ollama_pricing_discovery_returns_model_names_without_prices` |
| `ProviderModelTokenPrice` rows | Merge contract in `ProviderPricingModels.cs` | `WorkspaceService.RefreshProviderModelPricesAsync`, `ProviderPricingMetadata`, runtime pricing calculators | Editor rows are saved through existing provider metadata and later consumed by runtime cost calculation | `Discovered_prices_override_same_model_but_preserve_manual_rows` |
| `ProviderModelPricingRefreshResult` | `WorkspaceService.RefreshProviderModelPricesAsync` | `SettingsPage`, `ProviderManagementPanel`, `ProviderModelPricingEditor` | Returned after refresh to replace editor rows and notify exact/model-name-only status | `OpenAi_pricing_discovery_requires_secret` |
| `Load from provider` UI action | `ProviderModelPricingEditor.razor` | Provider settings user | Visible in provider settings; invokes host callback | Browser proof in `bundle://proof/SB01/transcripts/browser-proof.txt` |
