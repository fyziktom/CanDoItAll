# Agent Template Teams

This bundle coordinates the migration of default agents from C# seed literals and embedded instruction assets into editable `Templates/Agents` files grouped by teams.

## Profile

- `initiative`

## Mission

- Make the default agent catalog file-backed, team-oriented, editable, and validated through seed regression tests plus browser proof.

## Outcome Contract

- Requested outcome: default agents live under `Templates/Agents`, each in its own team member folder with `settings.json`, `skills.json`, and `instructions.md`.
- Hard constraints: default agent instructions and team definitions must not remain hardcoded in source seed code; generic agent text must be easy to tune in simple files.
- Evidence required before closure: prepared/completed bundle validation, targeted .NET build/tests, source audit for removed hardcoded agent assets, and Playwright/browser validation that seeded agents still surface.
- Known blockers or explicit scope exceptions: existing runtime agent construction for tests and dynamic user-created agents is outside the default-template migration.

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

1. `subbundles/01-template-pack-and-loader-foundation`
2. `subbundles/02-seed-migration-and-team-splitting`
3. `subbundles/03-validation-and-browser-proof`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed`
- Browser validation analytics: `Completed`
