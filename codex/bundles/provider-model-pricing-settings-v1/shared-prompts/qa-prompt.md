# QA Prompt

Validate `SB01 provider-model-pricing-settings`.

Check that explicit API pricing becomes exact `ProviderModelTokenPrice` rows, model-name-only APIs do not claim exact pricing, manual rows survive refresh, local LLM rows remain editable, and both provider settings surfaces share the same refresh behavior. Review `proof/SB01/manifest.md`, `proof/SB01/semantic-invariants.md`, targeted test transcripts, source assertions, anti-stub audit, and raw-note closure before accepting completion.
