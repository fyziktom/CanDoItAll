# SB13 Semantic Invariants

- Invariant ID: `SB13-INV-001`
- Expected behavior: The operator console must expose run health, recovery advice, manager resolution reason/confidence/candidates, artifact obligations, artifact roots, dispatch receipts, diagnostics, approvals, and rework/timeline controls from persisted runtime state.
- Disallowed shallow implementation: prompt-only, docs-only for runtime behavior, source-only proof for runtime behavior, UI-only hiding of errors, or hardcoded project/run/Tetris/Blazor special cases.
- Positive proof: `ProcessWorkspaceTests.Runs_operator_console_surfaces_escalation_rework_and_timeline_controls` records a runtime artifact against a persisted artifact expectation and asserts the operator console surfaces readback, artifact matrix, dispatch receipts, manager resolution, escalation/rework, and timeline sections.
- Runtime state proof: `ProcessRuntimeOperatorReadModelTests` covers blocked/missing artifact, escalation rework/manual rerun, dead-letter outbox health, and manual rerun inside a failed run.
- Browser proof: The freshly built web app served on `http://127.0.0.1:51313/processes?processId=840687f5-249b-4b79-9752-0bd17d4d6d7e&runId=dabb14ef-8053-48db-a83d-ca709858565a`; the rendered Control tab contained all SB13 operator sections and emitted no browser console errors.
