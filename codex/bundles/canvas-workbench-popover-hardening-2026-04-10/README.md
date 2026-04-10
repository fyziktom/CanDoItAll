# Canvas Workbench Popover Hardening

This bundle coordinates the repair and hardening of the shared `CanvasWorkbench` hover and popover path used by workbench canvases. The mission is to remove the `showPopover` crash, make annotation hover state resilient across clicks and rerenders, audit nearby canvas-JS anti-patterns in the same mechanism, and prove the behavior on shared-canvas and real workbench surfaces without changing the C# surface contract.

## Profile

- `feedback`

## Mission

- Repair the shared workbench-canvas annotation hover and popover path so annotation hover never throws, stays consistent after node clicks and surface refreshes, remains safe when popover chrome is missing or disconnected, and preserves the existing annotation action, overlay, and node-interaction behavior across all shared `CanvasWorkbench` consumers.

## Bundle Layout

- `inputs/` raw request, stack trace, and normalized input
- `analysis/` repo-backed findings, risks, and reopen triggers
- `requirements/` normalized requirements for crash repair and hardening
- `architecture/` target runtime contract and boundary decisions
- `plan/` ordered subbundles, dependency map, and gates
- `traceability/` raw-note and requirement ownership mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` execution-ready phases
- `reviews/` preparation self-review and execution evidence

## Recommended Execution Order

1. `subbundles/01-hover-and-popover-state-invariants`
2. `subbundles/02-canvas-runtime-hardening-across-node-interactions`
3. `subbundles/03-browser-proof-and-closure`

## Dependency And Validation Map

- The operational dependency map, critical foundations, and progression gates live in `plan/01-phase-plan.md`.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Recorded`
