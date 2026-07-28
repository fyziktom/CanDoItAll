# Bundle Index

## Start Here

1. `00-user-summary-cs.md` — Czech owner summary.
2. `README.md` — mission, constraints, and execution order.
3. `analysis/02-impact-matrix.md` — concrete 1.13-to-1.15 impact map.
4. `analysis/06-session-approval-migration.md` — highest-risk cross-version state issue.
5. `analysis/07-workflow-handoff-streaming.md` — handoff and terminal-output analysis.
6. `plan/01-phase-plan.md` — ordered execution plan and gates.
7. `machine/migration-tasks.json` — machine-readable Codex task graph.
8. `subbundles/01-baseline-discovery-and-1-13-fixtures/README.md` — mandatory first implementation workstream.

## Bundle Characteristics

- Repository snapshot: `59f558bc866d39d438b53f5f743dd5e87c2a6253`
- Source branch: `agents-loading-refactor`
- Target release train: stable MAF `1.15.0`; A2A preview `1.15.0-preview.260722.1`
- Subbundles: 8
- Normalized requirements: 22
- Machine-readable migration tasks: 15
- Initial implementation state: not started
- Required first gate: capture 1.13 fixtures before package changes

## Highest-Risk Decisions

- Keep approval-response binding enabled.
- Preserve 1.13 mixed-approval semantics during the initial parity pass.
- Migrate or reissue approvals that were pending before deployment.
- Do not assume the MAF non-streaming terminal-output fix applies to CanDoItAll's streaming-first runtime.
- Keep custom workspace/file-tool security boundaries.
- Keep live runtime objects per execution; do not extend the preload cache to mutable agents or sessions.
