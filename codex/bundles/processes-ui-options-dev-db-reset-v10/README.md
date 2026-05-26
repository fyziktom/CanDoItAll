# Process UI Options And Development Process Reset

This bundle is a coordination and execution package for `processes-ui-options-dev-db-reset-v10`.

## Profile

- `feedback`

## Mission

- Bring the process authoring UI and typed process contracts into parity with the current template-pack vocabulary, then clear only process-owned data from the development PostgreSQL database and reload current process templates without deleting agents, plugins, memory, projects, project structure, or managed files.

## Outcome Contract

- Requested outcome: role definition options and process step definition options in the Blazor UI expose all supported typed choices used by process templates, and the development database contains freshly reloaded process templates after process history/runs are cleared.
- Hard constraints: preserve non-process data; keep project structures and related files; keep changes strongly typed; do not add stringly-typed UI branching; do not drop or recreate the whole development database.
- Evidence required before closure: template vocabulary audit, focused component/integration tests, build or targeted compile proof, process-only database delete transcript, template reload transcript, before/after table counts, and final bundle validation.
- Known blockers or explicit scope exceptions: browser proof is planned for the process editor route if a local app server can be started in the available environment; component tests remain required even if browser proof is blocked.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-01-template-vocabulary-and-ui-option-parity`
2. `subbundles/02-02-process-only-development-db-reset-and-template-reload`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Passed`
- Browser validation analytics: `Not required; component and database proof captured`
