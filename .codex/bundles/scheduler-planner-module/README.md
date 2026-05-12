# Scheduler Planner Module

This bundle prepares an initiative to add a Scheduler/Planner module for automatic workflow and process runs. It is preparation-only: no production implementation has been performed in this bundle.

## Profile

- `initiative`

## Mission

Deliver a first-class Scheduler/Planner feature that lets operators define CRON-based schedules for workflows and processes, inspect active planned runs, create new schedules with human-readable CRON descriptions, and search historical scheduled run outcomes. The feature must reuse the existing Quartz-backed Automation runtime for triggering, close the current Quartz database-recovery gap, and keep workflow/process launch logic behind typed application-service adapters.

## Outcome Contract

- Requested outcome: an execution-ready bundle for implementing the Scheduler/Planner module.
- Hard constraints: use existing Quartz triggering; configure database-backed Quartz recovery; store and display CRON descriptions; provide an own tabbed page; use existing Blazor/BaseLib/Radzen-style component patterns; do not implement code during bundle preparation.
- Evidence required before closure: prepared-bundle validator pass, subbundle readiness review, source inventory, UI layout proposal artifact, and explicit proof requirements per subbundle.
- Known blockers or explicit scope exceptions: implementation must confirm the active runtime database provider details before choosing the exact Quartz ADO.NET delegate and migration path.

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

1. `subbundles/01-scheduler-domain-and-persistence`
2. `subbundles/02-quartz-db-recovery-and-fire-dispatch`
3. `subbundles/03-process-and-workflow-run-adapters`
4. `subbundles/04-scheduler-planner-ui`
5. `subbundles/05-validation-and-closure`

## Dependency And Validation Map

- The dependency map, critical gates, and rollback-aware validation plan live in `plan/01-phase-plan.md`.
- If this bundle is resumed after compaction or by a different agent, start with this README, then `requirements/01-normalized-requirements.md`, then the active subbundle README, then `reviews/01-execution-report.md`.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Not started`
- Subbundle gate review: `Completed for preparation`
- Final closure gate: `Not started`
- Browser validation analytics: `Not started`
