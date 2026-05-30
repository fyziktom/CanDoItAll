# Target Solution

## Provider Pricing Model

- Add typed model-pricing records to agent framework models with decimal prices per 1M tokens for input, cached input, and output.
- Add provider-level pricing and private-provider metadata as init properties so existing constructors remain source-compatible.
- Persist pricing rows and the private flag in provider configuration JSON through `AgentFrameworkProviderMetadata`.

## Pricing Defaults

- Seed OpenAI defaults from the official pricing page for the app-supported models.
- Seed Ollama/private-style providers with editable non-zero defaults that represent low local or self-hosted compute cost.
- Normalize missing provider pricing on load so old provider records become usable without manual JSON editing.

## Cost Calculation

- Centralize token-cost calculation in a small typed service/helper shared by runtime and process analytics.
- Calculate cost from non-cached input tokens, cached-input tokens, and output tokens.
- Reject manual model overrides without a matching provider price row instead of falling back to zero.

## Private Badge

- Pass private-provider knowledge into `AgentSelectionCard` as a typed boolean.
- Resolve that boolean from provider metadata in each card-owning surface.
