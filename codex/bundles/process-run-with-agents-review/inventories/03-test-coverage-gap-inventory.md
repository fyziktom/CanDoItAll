# Test Coverage Gap Inventory

## Existing Proof

- `ProcessMockAgentRuntimeIntegrationTests` proves deterministic mock catalog seeding, launch staffing, backend E2E completion through durable outbox dispatch, artifacts, branches, decisions, and outbox completion.
- `ProcessRunAutomationDispatchServiceTests` covers blocking execution detection, stale active execution windows, recoverable execution IDs, reusable sessions, and dispatcher helper behavior.
- `ProcessOutboxIntegrationTests` covers durable outbox behavior.
- `ProcessWorkspaceTests` covers process workspace authoring and template/component paths.

## Missing Proof

- Browser E2E launch and observation of agent process run from Process Workspace.
- UI display of missing artifact state.
- UI display of outbox dead-letter and retry state.
- UI-driven retry/rerun after missing artifact or agent crash.
- Deterministic negative runtime scenarios for agent no-artifact delivery, crash, context-loss retry, stale run recovery, and dead-lettered dispatch.
- Screenshots or DOM assertions for run health and artifact ledger states.
