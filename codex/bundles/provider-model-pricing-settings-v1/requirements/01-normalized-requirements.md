# Normalized Requirements

| Requirement | Source notes | Observable success criteria |
| --- | --- | --- |
| `R001` Provider settings must keep editable per-model inference prices. | `N001`, `N002`, `N004`, `N005` | A provider editor can add/remove rows and save model, input, cached-input, and output USD-per-million-token prices; saved rows rehydrate through `ProviderPricingMetadata`. |
| `R002` Provider settings must load model pricing from provider APIs when adapters support pricing metadata. | `N003` | The provider adapter contract exposes typed pricing discovery; OpenAI-compatible responses containing explicit price fields produce exact `ProviderModelTokenPrice` rows. |
| `R003` Provider APIs that expose model names but not prices must still create editable model rows. | `N003`, `N005` | Ollama/OpenAI model-name-only discovery adds or preserves rows and reports that the API did not return exact price metadata. |
| `R004` Refresh must preserve manual pricing. | `N004` | Existing manual prices are preserved unless the same model has explicit API price values; non-discovered manual rows remain present. |
| `R005` Validation must cover exact pricing, local-LLM discovery, persistence, and UI wiring. | `N001`-`N005` | Targeted proof exists and is recorded in `reviews/01-execution-report.md` with closure status for each raw note. |

## Out Of Scope

- External billing reconciliation or historical cost backfill.
- Claiming official provider price support for APIs that only return model names.
- Adding database columns when existing pricing metadata can persist the settings.
