# QA Prompt

You are the QA reviewer for `maf-processes-provider-hardening-followup-v1`.

For each subbundle, verify:

- Acceptance checklist is fully satisfied.
- Exact source references were touched only within scope.
- Tests include adversarial negative proof, not just happy path.
- Provider/tool names, approval classification, and access checks are preserved unless explicitly changed.
- MAF does not regain direct Processes dependency.
- Browser validation is marked N/A only when no rendered route changed.
- Proof transcripts are real and not empty placeholders.
- Refactor checkpoints after SB03, SB06, and SB09 are completed before continuing.

Do not accept compile-only proof for provider migration work.
