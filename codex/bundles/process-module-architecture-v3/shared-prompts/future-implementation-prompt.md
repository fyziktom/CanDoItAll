# Future Implementation Prompt

You are implementing a future subbundle derived from `codex/bundles/process-module-architecture-v3`.

Execute only the user-approved subbundle. v3 prepared SB01-SB28, but none were executed during v3 preparation.

Required posture:

- Start on a rewrite branch.
- Read the selected subbundle README and its context reset file list.
- Read previous subbundle execution reports.
- Read `analysis/06-current-implementation-user-story-map.md` and `traceability/04-user-story-coverage-map.md` before changing any Process behavior.
- Read `architecture/19-dotnet-performance-guardrails.md` and `validation/05-dotnet-performance-antipattern-checklist.md` before creating or modifying C# hot-path code.
- If the selected subbundle touches launch planning or assignment resolution, read `architecture/20-role-candidate-selection-and-readiness.md` and `validation/06-role-candidate-readiness-validation.md`.
- Record story coverage for every US-### row owned by the selected subbundle.
- In Phase 0, archive old Process code before deletion and produce manifest/hash proof.
- Do not wrap `ProcessRunAutomationDispatchService`.
- Do not delete `Templates/Processes` before migration tooling exists.
- Do not select strategies dynamically in the dispatcher.
- Do not let UI query runtime EF entities directly.
- Do not introduce sync-over-async, unbounded event/projector queues, per-call `HttpClient`, per-call `JsonSerializerOptions`, load-all UI history queries, or LINQ-heavy hot-path projectors without recorded mitigation.
- Do not treat HR candidate score as executable readiness. Missing required tools, rights, capabilities, approvals, bindings, or access must be typed findings that block approval/execution unless policy explicitly allows an audited override.
- Add tests at the project boundary being implemented before moving upward.
- For browser-facing subbundles, capture Playwright MCP proof and screenshots in the owning subbundle before moving forward.
- For C# hot-path subbundles, record exact .NET performance scan counts in the execution report.

Stop and reopen architecture if the target boundary is impossible without importing domain behavior into core/runtime.
