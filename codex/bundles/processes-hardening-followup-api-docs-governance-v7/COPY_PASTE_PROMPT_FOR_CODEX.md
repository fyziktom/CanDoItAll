You are Codex working in the CanDoItAll repository.

Use this bundle:

`codex/bundles/processes-hardening-followup-api-docs-governance-v7`

Branch context:
- Work on the current `processes-hardening` branch unless the local repo has a differently named branch corresponding to the user's `process-hardening`.
- PostgreSQL-only: do not reintroduce SQLite paths, SQLite migrations, or provider-switching behavior for process runtime.

Main goal:
- Verify phase6 runtime governance changes and complete the next hardening phase.
- Ensure new process typed governance fields are reflected in runtime, Processes API/tools, import/export, skills, templates, and documentation.
- Avoid process blocks caused by stale tool/API contracts or missing docs.
- Keep `Processes` generic; workflows are below processes and cannot own process lifecycle or artifact validation.

Required execution pattern:
1. Read `README.md`, `analysis/02-verified-findings.md`, `requirements/01-normalized-requirements.md`, and `plan/01-phase-plan.md`.
2. Execute subbundles in order.
3. After SB04, SB08, and SB12, run the refactor checkpoint subbundles before continuing.
4. Each subbundle must update its proof manifest and produce failing-first/red-team proof, passing proof, source assertions, anti-stub audit, and changed-file hashes.
5. Final closure must run focused tests, unit tests, integration tests, build, PostgreSQL audit, skill/docs checks, and a generic scenario harness.

Critical:
- Do not solve API/docs drift by only editing prompts.
- Do not rely on text-only parsing where typed fields exist.
- Do not let `processes_*` tools accept stale request/response schemas.
- Do not let workflow/subprocess artifacts satisfy process expectations without explicit process-owned mapping and validation.
