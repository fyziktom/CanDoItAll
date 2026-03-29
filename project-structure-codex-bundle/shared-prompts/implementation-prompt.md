# Implementation Prompt

Implement only the current subbundle.

Required behavior:
- Read the root `README.md`, `plan/01-phase-plan.md`, `traceability/01-requirement-traceability.md`, and the selected subbundle README.
- Confirm prerequisites, impacted features, and exact source references before editing.
- Make the smallest correct change set.
- Validate with the listed tests, Playwright MCP actions, screenshots, and any counter or persistence evidence required by the subbundle.
- Update `reviews/01-execution-report.md` while proof is fresh.
- Reopen the subbundle instead of moving on if proof is weak.

Do not:
- widen scope into downstream tasks,
- replace browser proof with reasoning,
- treat a failing shared-canvas regression as acceptable collateral.
