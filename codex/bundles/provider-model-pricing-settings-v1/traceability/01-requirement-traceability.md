# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| `N001` / `R001` | `requirements/01-normalized-requirements.md` | `subbundles/01-provider-model-pricing-settings` | Source assertion and component/service build proof | Both `/settings` and `/agents` provider surfaces must remain wired. |
| `N002` / `R001` | `analysis/01-current-state.md` | `subbundles/01-provider-model-pricing-settings` | `ProviderPricingMetadata` persistence test/source assertion | Existing JSON metadata is expected to persist rows. |
| `N003` / `R002` / `R003` | `architecture/01-target-solution.md` | `subbundles/01-provider-model-pricing-settings` | Adapter tests for explicit API prices and model-name-only API discovery | Do not claim exact API prices when the API only returns names. |
| `N004` / `R004` | `requirements/01-normalized-requirements.md` | `subbundles/01-provider-model-pricing-settings` | Merge-rule test preserving manual rows | Manual rows are production user input. |
| `N005` / `R003` | `requirements/01-normalized-requirements.md` | `subbundles/01-provider-model-pricing-settings` | Ollama model discovery fixture and UI source assertion | Local LLMs keep editable prices. |
