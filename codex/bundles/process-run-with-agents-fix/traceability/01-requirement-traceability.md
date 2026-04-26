# Requirement Traceability

| Requirement | Inputs | Analysis | Subbundle | Proof |
| --- | --- | --- | --- | --- |
| REQ-001 | `inputs/00-original-request.md`, `evidence/01-test-results.md` | `analysis/01-current-state.md` | 01 | Process outbox and process-service tests pass without `primary.db` lock failures. |
| REQ-002 | `evidence/01-test-results.md` | `analysis/01-current-state.md` | 01 | Focused `CanDoItAll.Mcp.Processes.Tests` template tests compile and run. |
| REQ-003 | `evidence/01-test-results.md` | `analysis/01-current-state.md` | 01 | `ProcessRunAutomationDispatchServiceTests` updated and passing for current semantics. |
| REQ-004 | `inputs/02-structured-input.md` | `inventories/02-template-flow-inventory.md` | 02 | Calculator process graph has scope, architecture, implementation, QA reject, repair, QA approve, release. |
| REQ-005 | `inputs/02-structured-input.md` | `analysis/01-current-state.md` | 02, 04 | Branch outcome keys `repairs-required` and `approved` resolve to process branch IDs. |
| REQ-006 | `inputs/02-structured-input.md` | `analysis/01-current-state.md` | 03 | Launch plan staffing binds intended mock technical agents. |
| REQ-007 | `inputs/00-original-request.md` | `architecture/01-target-solution.md` | 03, 05 | E2E tests assert mock provider and no real provider execution. |
| REQ-008 | `analysis/01-current-state.md` | `architecture/01-target-solution.md` | 04 | Dispatcher tests prove explicit mock evidence mapping. |
| REQ-009 | `analysis/01-current-state.md` | `architecture/01-target-solution.md` | 04 | Process artifact records match expected required artifacts. |
| REQ-010 | `inputs/00-original-request.md` | `plan/01-phase-plan.md` | 05 | E2E mock-agent process run completes with repair loop and artifact assertions. |
| REQ-011 | `inputs/00-original-request.md` | `analysis/02-assumptions-and-risks.md` | 04, 05 | Negative-path tests assert specific diagnostic messages. |
