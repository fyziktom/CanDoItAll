# Agent Pricing and Private Providers

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Ready for validator`
- Execution status: `Implemented`
- Subbundle gate review: `Passed with source-backed browser limitation`
- Final closure gate: `Passed`
- Browser validation analytics: `Agent catalog route validated; seeded in-memory data did not include a private-backed agent`

## Source Input

- User request: realistic agent work pricing, provider-level model price tables, required pricing for manual model overrides, private-model pricing defaults, correct process and workflow cost analytics, and a visible `Private` badge on agent cards backed by private providers.
- Official pricing source: `https://developers.openai.com/api/docs/pricing`, checked on 2026-05-30.

## Bundle Shape

- Profile: `initiative`
- Subbundles:
  - `01-provider-pricing-foundation`: provider pricing records, defaults, metadata persistence, and validation contracts.
  - `02-run-cost-analytics`: runtime cost calculation and process/workflow analytics propagation.
  - `03-private-agent-card-badges`: private provider identification in agent card surfaces.

## Success Definition

- Providers can store and edit a price table per model with input, cached-input, and output token prices.
- OpenAI defaults match the official pricing page for the seeded/default supported models.
- Ollama/private-style providers get explicit, editable non-zero defaults.
- Agent model overrides cannot save unless the selected provider has pricing for the override model.
- Agent run metrics and process/workflow analytics use token pricing rather than placeholder labor-hour estimates when model usage exists.
- Agent cards backed by private-style providers show a `Private` badge consistently across catalog and switcher surfaces.
