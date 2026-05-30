# Assumptions and Risks

## Working Assumptions

- Provider `ExtraSettingsJson` is the right persistence surface for pricing because provider runtime metadata already lives there.
- Ollama and unknown non-OpenAI provider kinds are treated as private-style providers unless explicitly marked otherwise.
- A manual agent model override means a non-empty agent model value that is not relying on the provider default.

## Critical Path Risks

- Workflow runs may not currently expose token usage in the same shape as direct agent execution, so workflow cost coverage may depend on available metrics.
- Existing provider configuration UI may have multiple entry points; missing one would create inconsistent pricing setup.
- Zero or missing model prices would silently undercount analytics if validation is only UI-side.

## Validation Risks

- Browser validation may be slower than targeted component/service tests if the app build takes longer than the test timeout.
- Pricing defaults can drift as OpenAI updates the pricing page, so tests should assert code behavior rather than hard-code an evergreen claim beyond the checked source date.

## Reopen Triggers

- A provider can save without a price table or manual model override pricing.
- A private/Ollama-backed agent card renders without the `Private` badge.
- Process live or history cost remains tied only to target lead hours after token metrics exist.
