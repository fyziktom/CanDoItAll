# Process And Workflow Form Layout Tuning

This bundle is a coordination and execution package for `process-workflow-form-layout-tuning-v1`.

## Profile

- `feedback`

## Mission

- Replace the long, hard-to-scan Processes step setup forms and Workflows editor forms with space-efficient tabbed layouts that use existing shared components and keep all existing behavior intact.

## Outcome Contract

- Requested outcome: Processes page Steps tab setup forms and Workflows page Editor forms are reorganized into clear tabbed sections, with separate layout proposals captured for the main affected forms.
- Hard constraints: keep behavior and persistence unchanged; use existing shared components such as `Tabs`, `TabsItem`, `Grid`, `Stack`, `FormField`, `FormSection`, `SurfaceCard`, and `PanelCard`; do not add special styling or page-specific visual redesign; do not introduce stringly-typed command or state contracts beyond UI tab labels.
- Evidence required before closure: image proposal artifacts, prepared and completed bundle validators, targeted builds, source assertions, anti-stub audit, and real browser proof for `/processes` Steps and `/agents/workflows` Editor at desktop and narrow widths.
- Known blockers or explicit scope exceptions: the CanDoItAll components MCP transport closed during discovery, so shared-component choices are grounded in repo-local component APIs and usages instead of live MCP output.

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
- `evidence/` generated planning proposals and later browser artifacts when useful

## Recommended Execution Order

1. `subbundles/01-01-layout-inventory-and-proposals`
2. `subbundles/02-02-process-step-form-tabs`
3. `subbundles/03-03-workflow-editor-form-tabs`
4. `subbundles/04-04-validation-and-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, UI proof requirements, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed prepared-stage validator on 2026-05-31`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Passed`
