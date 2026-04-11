# Canvas Workbench Popover Hardening And JS Organization

This bundle coordinates the repair and hardening of the shared `CanvasWorkbench` hover and popover path used by workbench canvases, then extends that work with a focused JS-organization pass on the largest verified workbench-runtime hotspots. The mission is to remove the `showPopover` crash, make annotation hover state resilient across clicks and rerenders, audit nearby canvas-JS anti-patterns in the same mechanism, split the highest-risk long runtime files into ordered feature slices, and prove the behavior on real workbench surfaces without changing the C# surface contract.

## Profile

- `feedback`

## Mission

- Repair the shared workbench-canvas annotation hover and popover path so annotation hover never throws, stays consistent after node clicks and surface refreshes, remains safe when popover chrome is missing or disconnected, and preserves the existing annotation action, overlay, and node-interaction behavior across all shared `CanvasWorkbench` consumers.
- Extend the same bundle with a behavior-preserving JS-organization pass that inventories long CanvasLib JS hotspots, selects the highest-value workbench runtime seams, and splits them into smaller ordered files with shared helpers where duplication or fragile cross-file coupling is proven.
- Reopen the bundle for lifecycle-safe workbench interop after a real app failure in the Processes Run tab, then re-prove the reachable CanDoItAll app canvas routes and record non-canvas blockers honestly.

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
4. `subbundles/04-js-hotspot-inventory-and-boundaries`
5. `subbundles/05-canvas-renderer-scene-split`
6. `subbundles/06-runtime-entry-splitting-and-regression-proof`
7. `subbundles/07-workbench-interop-lifecycle-hardening`
8. `subbundles/08-cross-canvas-app-proof-and-blockers`

## Dependency And Validation Map

- The operational dependency map, critical foundations, and progression gates live in `plan/01-phase-plan.md`.

## Validation Summary

- Bundle preparation status: `Prepared and extended`
- Bundle readiness gate: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Recorded`
