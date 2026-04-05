# Hard gates
HG-01. Legacy carrier retirement must be complete.
HG-02. Marker truth must be single-source.
HG-03. Plugin editors must be manifest-driven, not hardcoded by field key.
HG-04. Legacy enum identity must be compatibility-only, never synthesized in active save flows.
HG-05. Node references must be open-world, not enum/property closed-world.
HG-06. Load paths must not normalize+persist compatibility state.

MG-01. Manual gate: the generic durable connector command boundary for write-side plugins must exist before email / LinkedIn / custom API write-side work starts.
