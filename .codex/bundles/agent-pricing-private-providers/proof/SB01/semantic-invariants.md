# SB01 Semantic Invariants

- Invariant ID: SB01-PRICE-ROWS
- Source raw note: Each provider must allow a table of prices for each model, and manual model overrides must also provide pricing.
- Expected behavior: Provider pricing has separate input, cached-input, and output token rates, and agent save rejects a manual override model without a matching provider price row.
- Disallowed shallow implementation: A flat provider cost, a zero fallback, or a UI-only price field that is not persisted is not acceptable.
- Failing-first test: N/A process exemption; no pre-existing negative test was available in the prepared bundle, so the focused regression was added with the implementation.
- Passing test: ProviderPricingTests verifies OpenAI defaults, private defaults, separate cached token math, and pricing metadata round-trip.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs`, `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/ProviderModelPricingEditor.razor`, and `repo://src/CanDoItAll.AgentFramework.Core/Catalog/AgentFrameworkWorkspaceCatalogService.Agents.cs`.
- Production assertions: Provider save/load paths persist price rows and private flags through the same profile metadata consumed by runtime provider lookup.
- Red-team negative case: An agent with a manual model override and no matching provider model price row fails save validation instead of silently running with missing cost data.
- Downstream dependency check: SB02 consumes the typed price rows for runtime cost calculation, and SB03 consumes the private-provider flag for card badges.
