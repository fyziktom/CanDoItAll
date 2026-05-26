You are working in `fyziktom/CanDoItAll` on the `processes-hardening` branch.

Use this bundle:

`codex/bundles/processes-hardening-followup-live-tetris-readiness-v9`

Execute subbundles in order. The user wants a real UI-driven process test after this bundle, where the system creates a simple Blazor WASM PWA Tetris app through the Processes UI.

Non-negotiable rules:

- Do not hardcode Tetris, Blazor, or software delivery behavior into the generic process runtime.
- Keep Processes above Workflows. Workflows can execute process roles, but Processes own lifecycle, artifacts, transitions, recovery, and validation.
- The first/architecture step must not implement. Implementation must happen only in the implementation step. QA/review must not mutate product files.
- Agents must have the necessary skills/tools so they do not improvise: Processes API, Blazor WASM/PWA, .NET build/test/run, browser/Playwright proof, project-structure writeback, artifact/lineage rules.
- A seeded baseline scenario is not a live test. Add a live-run profile/runbook so the UI test starts with no fake completed transitions.
- Keep PostgreSQL-only runtime assumptions.

Every critical subbundle must include source assertions, failing-first/adversarial proof, passing proof, anti-stub audit, and changed-file hashes.
