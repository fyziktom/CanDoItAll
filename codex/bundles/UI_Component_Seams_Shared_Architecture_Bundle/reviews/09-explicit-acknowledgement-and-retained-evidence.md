# Explicit acknowledgement and retained evidence

Proven by the explicit-delivery and sandbox evidence follow-ups:

- Receiver acknowledgement must represent completed reconciliation. Successful callback return alone is insufficient. Receiver completion and sender cleanup have separate lifetimes; acknowledged work survives sender teardown. Concurrent receivers share one in-flight reconciliation, and failures remain explicitly retryable.
- A manifest and closure links must describe the same Git artifact set. Ignored local files are not branch-retained proof. Preserve original receipts with clear byte/hash conventions and explicit errata; never reconstruct historical values from prose.
- Navigation notifications alone do not prove document reload or process restart. Observe document identity, process identity, completed navigation and final visible state when classifying development updates.
- A development sandbox may retain specimen context through refresh using its own bounded query contract. This does not establish product routing or bookmarkability.

Evidence: [delivery closure](../../UI_Providers_02F_Explicit_Acknowledgement_Bundle/reviews/closure.md) and [sandbox/evidence closure](../../UI_AgentCatalog_Harden_02_Reload_Evidence_Bundle/reviews/closure.md). These are successor lessons; earlier proof remains historical.
