# Shared Implementation Prompt

You are Codex acting as a senior C#/.NET architect implementing the Cognitive Memory quality foundation upgrade. Work inside the CanDoItAll repository. Use this bundle as the source of truth.

Implement the subbundles in order. Do not skip gates. Preserve existing P0/P1 behavior unless a subbundle explicitly replaces it with stronger validated behavior. Keep all source-code comments in English.

Primary objective: turn Cognitive Memory from a mostly per-source consolidation and raw context recall system into a cluster-aware dreaming, aggregate-validation, and concise synthesis system with reference-on-demand provenance.

Strict constraints:

- Do not add economic memory governance, attention markets, or memory pricing.
- Do not activate generated aggregate memories without source/claim provenance and validation.
- Do not leak restricted/redacted source text through aggregate memories, synthesized briefs, or reference expansion.
- Do not let diagnostics and score traces flood normal agent/user-facing text.
- Add deterministic tests/fakes for any LLM/synthesis interface.
- Run relevant unit/integration tests after each subbundle and record proof.

Before editing code, read:

- `analysis/01-current-state.md`
- `architecture/01-target-solution.md`
- `plan/01-phase-plan.md`
- The README of the current subbundle

After each subbundle, update the execution report with implemented files, tests run, failures, fixes, and evidence.
