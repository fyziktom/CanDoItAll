# Process Canvas Layout Composition

This bundle coordinates the process canvas layout-composition repair for complex process definitions.

## Profile

- `initiative`

## Mission

Improve automatic process-canvas node positions so authored processes read as a clear left-to-right flow: the main/default path stays on a stable spine, branches fan into readable lanes, role and executor nodes sit near the steps they affect, and connection lines have enough space to be followed by a human reviewer.

## Outcome Contract

- Requested outcome: The `Recomposition` canvas action produces a clearer process map for complicated authoring canvases without changing process semantics, saved data shape, manual drag behavior, branch normalization, or runtime execution behavior.
- Hard constraints: Keep the change scoped to automatic layout/composition. Do not replace CanvasLib, rewrite the workbench, add a new graph library, or silently hide cyclic process graphs.
- Evidence required before closure: targeted component tests for layout rules, build or test command output, and browser-visible proof on `/processes` or an equivalent process canvas route when the local app can be launched.
- Known blockers or explicit scope exceptions: Generated images may be used only as visual planning aids. They do not count as acceptance proof. Runtime WebGL layout can consume improved canvas coordinates, but this bundle does not redesign the 3D layout modes.

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

1. `subbundles/01-01-layout-analysis-and-contract`
2. `subbundles/02-02-definition-recomposition-tuning`
3. `subbundles/03-03-validation-and-browser-proof`
4. `subbundles/04-04-role-instance-composition-and-default-template-repair`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `Passed through 04 closure`
- Final closure gate: `Passed`
- Browser validation analytics: `Captured for 03 and 04`
