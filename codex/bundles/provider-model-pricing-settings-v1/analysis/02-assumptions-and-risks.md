# Assumptions And Risks

## Assumptions

- Provider APIs are inconsistent. Some OpenAI-compatible APIs may include pricing metadata in `/models`; official OpenAI model discovery may only expose model identities.
- Local Ollama APIs expose model names through `/api/tags`, not token pricing. Local costs therefore remain user-owned manual values.
- Existing `ExtraSettingsJson` pricing metadata is sufficient; no migration is expected.

## Critical Path Risks

- `SB01` is the only critical foundation. If its merge rules are wrong, runtime cost calculation can undercharge, overcharge, or erase user-entered local prices.
- Provider refresh must preserve manual rows unless an API explicitly supplies prices for the same model.

## Validation Risks

- Live provider APIs and secrets are environment-dependent, so proof should use adapter-level HTTP fixtures for exact pricing and model-name-only discovery.
- UI browser proof may be expensive if the full app requires a live database. If not run, closure must record the browser validation gap and rely on targeted build/source proof.
- Existing broader test projects may have unrelated baseline failures; targeted tests should be captured first.

## Reopen Triggers

- Reopen `SB01` if refresh replaces manual rows for undiscovered models.
- Reopen `SB01` if an API model response with explicit pricing does not produce exact `ProviderModelTokenPrice` rows.
- Reopen `SB01` if provider settings can save rows that fail `ProviderPricingDefaults.TryValidateModelPrices`.
- Reopen `SB01` if `/settings?tab=providers` and `/agents?tab=providers` diverge in pricing refresh behavior.
