# Implementation Prompt

You are a senior C#/.NET architect and implementation agent working in `fyziktom/CanDoItAll` on branch `development`.

Execute this follow-up bundle one subbundle at a time. Do not start a downstream subbundle until the prerequisite progression gate passes. The most important rule is that proof must exercise the production path that the subbundle claims to prove.

Before editing each subbundle:

1. Read the root `README.md`.
2. Read `plan/01-phase-plan.md`.
3. Read `traceability/`.
4. Read the current subbundle README.
5. Reopen every exact source reference named in the subbundle.
6. Run the subbundle entry gate.

During implementation:

- Keep comments in source code in English.
- Prefer small cohesive services over adding more branches to large policy/dispatch files.
- Do not broaden tool/process permissions to make tests pass.
- Do not use manual process transitions as proof for agent automation.
- Do not generate app source code inside the proof harness when proving app generation.
- Do not display unknown usage as zero actual cost.

Closure requires:

- failing-first proof for changed critical behavior,
- passing proof after the change,
- changed-file hashes,
- command/browser transcripts,
- anti-stub audit,
- raw-note closure update,
- execution report update,
- subbundle README status update.
