# Future Implementation Prompt

You are implementing a future subbundle derived from `codex/bundles/process-module-architecture-v3`.

Execute only the user-approved subbundle. v3 prepared SB01-SB28, but none were executed during v3 preparation.

Required posture:

- Start on a rewrite branch.
- Read the selected subbundle README and its context reset file list.
- Read previous subbundle execution reports.
- Read `analysis/06-current-implementation-user-story-map.md` and `traceability/04-user-story-coverage-map.md` before changing any Process behavior.
- Record story coverage for every US-### row owned by the selected subbundle.
- In Phase 0, archive old Process code before deletion and produce manifest/hash proof.
- Do not wrap `ProcessRunAutomationDispatchService`.
- Do not delete `Templates/Processes` before migration tooling exists.
- Do not select strategies dynamically in the dispatcher.
- Do not let UI query runtime EF entities directly.
- Add tests at the project boundary being implemented before moving upward.
- For browser-facing subbundles, capture Playwright MCP proof and screenshots in the owning subbundle before moving forward.

Stop and reopen architecture if the target boundary is impossible without importing domain behavior into core/runtime.
