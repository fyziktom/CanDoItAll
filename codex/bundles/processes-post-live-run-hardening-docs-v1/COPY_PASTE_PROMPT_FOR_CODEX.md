You are working in `fyziktom/CanDoItAll`, branch `processes-hardening`.

Use this bundle:

`codex/bundles/processes-post-live-run-hardening-docs-v1`

Execute subbundles in order.

Main goal: after the first successful full Blazor app process run, harden the generic Processes runtime, artifacts, project-structure integration, manager chat, tests, documentation, and skills.

Hard rules:

- Start by reading this bundle and the recent local bundle reports:
  - `codex/bundles/maf16-processes-final-preflight-hardening-v4/reviews/01-execution-report.md`
  - `codex/bundles/process-run-output-manager-artifact-tuning-v1/reviews/01-execution-report.md`
- Close proof debt from previous blocked/no-go gates before claiming broad readiness.
- Refactor where the implementation has become too heuristic or too partial-class-heavy.
- Update docs and skills together with runtime changes.
- Do not hardcode the successful Blazor/Tetris run details.
- Preserve generic process behavior.
