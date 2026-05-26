You are working in `fyziktom/CanDoItAll` on the `processes-hardening` branch.

Use this bundle:

`codex/bundles/processes-hardening-followup-template-ui-readiness-v8`

Execute all subbundles in order. This is a follow-up after phase7. The goal is to make the Processes runtime, APIs, skills, docs, and process templates ready for a future UI-driven process run that creates a simple Tetris Blazor WASM PWA.

Non-negotiable requirements:

- Keep Processes above Workflows. Workflows may execute roles, but Processes own lifecycle, artifact contracts, transitions, block/recovery state, and validation.
- Keep the core generic. Do not hardcode Tetris or Blazor logic into the process core.
- Keep PostgreSQL-only runtime assumptions. Do not add SQLite.
- Treat compile/build failures as SB01 blockers.
- Do not solve process failures by loosening validation. Add typed causes, precise recovery, better templates, and better API/skill guidance.
- After every few subbundles, complete the refactor checkpoint before continuing.
- Every critical subbundle must include failing-first or adversarial proof, passing proof, source assertions, anti-stub audit, and changed-file hashes.
- Update process API, MAF process tool surface, skills, documentation, templates, and tests together.

Start by reading `README.md`, `analysis/02-verified-findings.md`, `plan/01-phase-plan.md`, and each subbundle README.
