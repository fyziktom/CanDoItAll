# Process Run With Agents Review

This bundle is a review and improvement-preparation package for `process-run-with-agents-review`.

## Profile

- `initiative`

## Mission

Review the completed `process-run-with-agents-fix` implementation from the UI operator perspective and prepare follow-up work so process runs with agents can be launched, observed, diagnosed, recovered, and controlled from the Process Workspace without relying on logs or backend-only integration tests.

## Original Review Conclusion

The backend can run the deterministic process mock happy path through launch planning, durable outbox dispatch, AgentFramework executions, branch routing, artifact projection, and final completion. The UI can partially participate: it can create and approve launch plans, execute ready launches, select runs, view active agents, inspect execution runs, view artifacts, and manually transition steps.

The implementation is not yet operator-complete. The UI does not expose outbox/dead-letter state, missing required artifact obligations, retry attempt policy, context-loss recovery state, or a clear "do the job again with proper instructions" control. Some failure paths become logs or backend state only, which means a user can see `Blocked` or `Failed` without enough actionable UI context to recover safely.

## Implementation Result

Executed on 2026-04-25. The Process Workspace now exposes agent-backed run health, step attempt history, artifact obligation satisfaction, outbox dispatch health, dead-lettered automation state, and a manual rerun path for eligible blocked/failed agent-owned steps. Manual rerun creates an auditable recovery directive, preserves previous attempts/artifacts, starts with fresh dispatch context, and enqueues a new automation dispatch record.

## Bundle Layout

- `inputs/` raw request, source artifacts, and structured interpretation
- `analysis/` current-state review, assumptions, risks, and critical failure points
- `requirements/` normalized requirements for future implementation
- `architecture/` proposed target runtime/UX shape
- `plan/` subbundle order and progression gates
- `traceability/` requirement-to-subbundle mapping
- `shared-prompts/` implementation and QA prompts for later execution
- `subbundles/` detailed implementation workstreams
- `inventories/` UI/runtime/artifact/test coverage inventories
- `reviews/` bundle self-review and non-execution report
- `templates/` subbundle README template

## Recommended Execution Order

1. `subbundles/01-ui-run-observability-and-controls`
2. `subbundles/02-artifact-contract-and-missing-artifact-recovery`
3. `subbundles/03-agent-crash-context-loss-and-retry-orchestration`
4. `subbundles/04-outbox-deadletter-and-run-health-operations`
5. `subbundles/05-ui-e2e-browser-proof-for-agent-process-runs`

## Validation Summary

- Bundle preparation status: `Complete`
- Execution status: `Implemented and validated`
- Subbundle gate review: `01-05 closed`
- Final closure gate: `Closed with focused validation`
- Browser validation analytics: `Passed for recovery/dead-letter browser proof`
- Known repository-wide blocker: `dotnet build CanDoItAll.slnx` still fails in unrelated pre-existing projects; see `reviews/01-execution-report.md`.
