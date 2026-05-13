# DB EF Query Repair

This bundle coordinates a focused EF Core query review and repair pass for the current CanDoItAll database work.

## Profile

- `feedback`

## Mission

Find and repair concrete EF Core query trouble in existing database-backed services without redesigning persistence or changing user-facing behavior.

## Outcome Contract

- Requested outcome: DB-backed read paths avoid obvious EF query traps such as materializing too much data before ordering, filtering, or paging; read-only queries use no-tracking where safe.
- Hard constraints: keep the existing switchable `AppDbContext` profile architecture, module boundaries, public service contracts, provider support, and persistence semantics unchanged.
- Evidence required before closure: prepared-stage bundle validation, targeted code inspection, targeted test execution for touched modules, solution build or scoped build proof, and completed raw-note closure.
- Known blockers or explicit scope exceptions: schema/index design and broad query-plan benchmarking are out of scope unless a code-level EF misuse is found while repairing this bundle.

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

1. `subbundles/01-ef-query-hotspots-and-repair`

## Dependency And Validation Map

- Dependency map, critical-subbundle notes, and phase gates live in `plan/01-phase-plan.md`.
- Durable execution state, proof commands, gate rows, and closure rows live in `reviews/01-execution-report.md`.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `All closure gates passed`
- Final closure gate: `Passed`
- Browser validation analytics: `N/A for non-UI DB repair`
