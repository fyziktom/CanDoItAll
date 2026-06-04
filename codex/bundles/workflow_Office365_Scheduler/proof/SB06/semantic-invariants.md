# Semantic Invariants SB06

- No shallow-pass proof: source assertions and passing integration tests cover production Scheduler launcher/handler paths and production project runtime gateway paths.
- No live Office365/Graph dependency in automated tests: tests use deterministic workflow/runtime gateway inputs and fake launch results where the Scheduler handler is the unit under test.
- No silent external write approval bypass: SB06 only adds scheduler no-message status and project-write idempotency metadata; it does not change Office365 processed-category approval/preapproval policy.
- No duplicate project output on retry: duplicate asset writes replay the original node by persisted `workflowProjectWrite.idempotencyKey`.
- No duplicate project output under concurrent dispatch: concurrent task-node writes with the same idempotency key serialize through the runtime gateway lock and return the same node id.
- Write-before-mark ordering remains in templates: idempotency settings were added to existing project write nodes before the mark-processed nodes; no mark-before-write edge was introduced.
- No-message runs are success/no-action: Scheduler records `NoMessages`, leaves plan/run errors empty, and does not relaunch on the same dedupe key.
- Code comments must be in English: no non-English comments were introduced.
