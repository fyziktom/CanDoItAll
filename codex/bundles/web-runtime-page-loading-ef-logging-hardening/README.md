# Web Runtime Page Loading and EF Logging Hardening

This bundle repairs runtime regressions reported after the PostgreSQL migration hardening work.

## Profile

- `feedback`

## Mission

Reduce avoidable page-load and mutation latency in the Processes, Project Structure, and Workflows surfaces while making Entity Framework console output opt-in and disabled by default.

## Outcome Contract

- Requested outcome: affected pages should defer expensive catalog/runtime/template loads until the user opens the relevant section, project-structure node creation should update the canvas without a full surface reload, and EF console output should be controlled by configuration with default off.
- Hard constraints: preserve existing public behavior, keep changes tightly scoped, do not hide failures with silent fallback behavior, and do not introduce new storage contracts unless required.
- Evidence required before closure: prepared-bundle validation, targeted component/unit tests, web project build, web-app startup proof, and execution-report rows for every subbundle.
- Known blockers or explicit scope exceptions: no broad performance instrumentation framework is introduced; proof focuses on removing identified eager calls and covering the visible regressions with tests.

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

1. `subbundles/01-sb01-current-state-and-diagnostics`
2. `subbundles/02-sb02-processes-lazy-loading`
3. `subbundles/03-sb03-project-structure-mutation-latency`
4. `subbundles/04-sb04-workflows-template-loading`
5. `subbundles/05-sb05-ef-console-logging-option-and-final-validation`

## Dependency And Validation Map

- `plan/01-phase-plan.md` contains the dependency map, phase gates, and validation expectations.
- `reviews/01-execution-report.md` is the durable implementation log and must be updated as each subbundle closes.

## Validation Summary

- Bundle preparation status: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Host startup passed; no layout changes required screenshots`
