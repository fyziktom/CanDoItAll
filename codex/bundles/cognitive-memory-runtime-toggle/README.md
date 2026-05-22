# Cognitive Memory Runtime Toggle

This bundle coordinates the runtime switch that lets demos and ordinary agent work run with Cognitive Memory fully bypassed.

## Profile

- `initiative`

## Mission

Add a persisted, runtime-editable Cognitive Memory usage setting and gate every optional integration point that can inject or run memory work outside the Cognitive Memory management surface. When disabled, agent chat, workflow memory executors, and scheduled automation skip memory deterministically instead of failing because memory lacks project scope or supporting infrastructure.

## Outcome Contract

- Requested outcome: operators can switch "Use Cognitive Memory" between enabled and disabled at runtime, and disabled means optional Cognitive Memory integrations are no-ops.
- Hard constraints: no silent fallback that hides active memory errors while memory is enabled; no stringly-typed setting values; no broad unrelated refactor; settings must persist in PostgreSQL and SQLite.
- Evidence required before closure: prepared bundle validation, targeted unit/component tests, build proof or explicit blocker, changed-file hashes, source assertions for every gated integration point, and a clean development PostgreSQL reset/migration result.
- Known blockers or explicit scope exceptions: direct Cognitive Memory management APIs remain available so the operator can inspect status and re-enable the feature; the toggle does not unregister the module at DI startup because runtime switching is required.

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
- `inventories/` scoped code inventory
- `templates/` retained scaffold template copy

## Recommended Execution Order

1. `subbundles/01-global-runtime-setting-and-api-contract`
2. `subbundles/02-skip-cognitive-memory-integration-points`
3. `subbundles/03-validation-and-clean-development-database`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Ready`
- Execution status: `Complete`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Not captured; component coverage validates the inserted settings control`
