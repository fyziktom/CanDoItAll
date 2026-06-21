# Remove Unused Validation Activity Automation Modules

This bundle is a coordination and execution package for `remove-unused-validation-activity-automation`.

## Profile

- `initiative`

## Mission

- Remove the unused Validation, Activity, and Automation module projects from the app, tests, navigation, composition, and connected workbench surfaces while preserving the scheduler/workflow/process paths that replace the old automation behavior.

## Outcome Contract

- Requested outcome: the old `CanDoItAll.Modules.Validation`, `CanDoItAll.Modules.Activity`, and `CanDoItAll.Modules.Automation` modules are no longer compiled, registered, navigable, or covered by obsolete tests.
- Hard constraints: map references before deleting, remove related tests, keep changes surgical, stop the running `5032` instance before edits, rebuild and restart `5032` for browser validation.
- Evidence required before closure: reference workbook, clean targeted reference audit, successful build/test commands, and Browser validation against the restarted local web host.
- Known blockers or explicit scope exceptions: historical EF migration designer files retain old string metadata as migration history; the current model snapshot and new removal migration reflect the deleted modules. Unrelated generic terms such as workflow validation or cognitive-memory automation endpoints are not removal targets by name alone.

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

1. `subbundles/01-01-reference-inventory-and-removal-boundaries`
2. `subbundles/02-02-module-dependency-extraction`
3. `subbundles/03-03-project-module-and-test-removal`
4. `subbundles/04-04-build-browser-and-bundle-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Passed`
- Browser validation analytics: `Passed`
