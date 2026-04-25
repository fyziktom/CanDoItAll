# Assumptions And Risks

## Assumptions

- The intended production flow is launch-plan-first, not direct run creation from UI.
- Agent-owned steps should be recoverable without silently relaxing required artifact, tool, branch, or governed outcome contracts.
- Operators need enough UI state to recover a process without reading server logs or database rows.
- Existing backend retry and recovery logic should be reused where correct; follow-up work should add read models, UI affordances, and missing policies before adding new orchestration layers.
- Deterministic mock agents remain the first proof target for browser E2E because they make failure and repair paths repeatable.

## Critical Path Risks

- If the UI observability model is wrong, later retry controls may act on incomplete or misleading state.
- If artifact expectation satisfaction is not modeled explicitly, agents can produce files that are visible in AgentFramework but still not satisfy process artifacts, leaving users unsure what to do.
- If dispatcher exceptions mark steps failed while the outbox record completes, outbox health alone will not represent process health.
- If dead-letter state is not surfaced, a run may appear stuck while the only actionable failure is hidden in the outbox table.
- If context-loss recovery only changes prompts and chat sessions, a user cannot verify that the new attempt inherited the right artifacts and instructions.

## Validation Risks

- Backend unit/integration tests are insufficient for UI readiness because they do not exercise the Process Workspace route and browser workflow.
- Browser proof must wait for a seeded deterministic process and mock agents; otherwise the run path depends on real provider availability and can be flaky.
- Long-running recovery tests may be slow unless time windows and workers are made controllable through test hooks.
- Negative-path proof needs deterministic fake or mock runtime modes for missing artifact, crash, context reset, and dead-letter scenarios.

## Reopen Triggers

- Reopen UI observability if operators still need logs or DB access to diagnose a run after subbundle 01.
- Reopen artifact contract work if a required artifact can be displayed as produced but not clearly mapped to an expectation.
- Reopen crash recovery if a failed or cancelled agent execution cannot be rerun from UI with explicit next-attempt instructions.
- Reopen outbox operations if dead-lettered records do not create an actionable process health signal.
- Reopen browser proof if the Playwright flow does not validate launch, observation, artifacts, retry, and failure interaction in one operator path.
