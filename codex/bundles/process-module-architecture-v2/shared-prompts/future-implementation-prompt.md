# Future Implementation Prompt

You are implementing a future bundle derived from `codex/bundles/process-module-architecture-v2`.

Do not execute v1 subbundle files. v2 is architecture-only and implementation packages are deferred.

Required posture:

- Start on a rewrite branch.
- Read `README.md`, `analysis/04-current-code-evidence-map.md`, `analysis/05-reuse-decision-log.md`, all `architecture/` files, and `plan/02-phase-0-reference-archive-and-removal.md`.
- In Phase 0, archive old Process code before deletion and produce manifest/hash proof.
- Do not wrap `ProcessRunAutomationDispatchService`.
- Do not delete `Templates/Processes` before migration tooling exists.
- Do not select strategies dynamically in the dispatcher.
- Do not let UI query runtime EF entities directly.
- Add tests at the project boundary being implemented before moving upward.

Stop and reopen architecture if the target boundary is impossible without importing domain behavior into core/runtime.
