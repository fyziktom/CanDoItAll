# Complete Pricing and Thinking descendants

## Pricing

Move/reuse the actual `ProviderModelPricingEditor` rendering, not a summary card plus a retained backend child. Preserve input, cached input, cache-write, output, image-input/cached-image-input, long-context threshold and all long-context rates. Preserve private-provider defaults and locked behavior, model catalog coverage, explicit unpriced state, source-managed model display names, reset and local discovery actions. [R20]

An absent published price is not zero. Missing long-context/image/cache-write values remain missing. Units stay USD per one million tokens; do not add exchange rates or infer prices from current public websites. All network fixtures supply synthetic prices. Historical usage/cost evidence must not be revalued merely because an operator edits a current price.

Keep all current non-PP1 callers working. The pricing component uses an existing `IProviderModelPricingEditorModel`; inspect actual callers, and retain a minimal compatibility adapter if needed. Do not make a neutral shared layer reference this product's module implementation. Update compiled/scoped asset consumers when moving CSS.

Exercise invalid partial numeric input, removal before another row, changed model name, price reset, missing rates, model discovery while editing, new model no selected default, saved model becoming unavailable and read-only imported rows. Test the full table's supported desktop scrolling instead of hiding columns to fit a phone.

## Thinking

Preserve the real table, search, paging, automatic/built-in/discovered/admin source labels, visible unsupported overrides, modal and local Apply. Keep `ProviderModelThinkingConfiguration` and `AgentThinkingEffortPolicy` as their existing single source of truth. Do not copy them into a scenario-only or feature-specific competing policy. [R19]

Provider default/upstream inheritance differs from explicit None. Boolean thinking control differs from an effort-level list. Clearing an override differs from configuring Unsupported. Provider kind, transport and purpose constrain what may be written. Preserve automatic metadata while editing explicit configuration and keep unrelated JSON keys unchanged.

The Thinking edit has its own provisional data and origin. Cancel must not change provider configuration. Apply must check the originating target and relevant model/configuration revision, including A→B→A and same draft changed kind/transport. A stale row cannot silently add an override for a different provider. Validation errors belong to the right row/dialog; do not overwrite raw JSON with defaults when parsing fails.

Source-managed Thinking is a read-only presentation with an injected source-refresh integration, not a local write path. A sandbox uses the real button/presentation contract with a labeled scenario owner; only production proves source synchronization. No paid capability probes or model maintenance are authorized by this task.
