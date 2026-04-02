# Project Structure Canvas Signals Toolbox Bundle

This bundle is the execution contract for the April 1, 2026 project-structure canvas follow-up that enlarges marker glyphs in the right-click menu and introduces a floating node-signals toolbox inspired by XMind-style marker palettes. It also upgrades the node signal model so markers become additive instead of single-value-only.

## Profile

- `feedback`

## Mission

- Increase second-layer marker glyph legibility without changing the marker badge size.
- Add a floating canvas toolbox for markers, progress, priority, and closely related quick node signals, opened from the top toolbar.
- Make markers additive so one node can carry multiple markers at the same time while keeping existing single-marker compatibility intact.
- Prove the new toolbox and node rendering in a real browser session on the project-structure canvas.

## Bundle Layout

- `inputs/` raw request and artifact notes
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and compatibility decisions
- `plan/` phase order, dependencies, and gates
- `traceability/` raw-note coverage matrix
- `shared-prompts/` implementation and QA prompts
- `subbundles/` execution-ready workstreams
- `reviews/` self-review and execution evidence

## Recommended Execution Order

1. `subbundles/01-01-multi-marker-data-contract-and-rendering`
2. `subbundles/02-02-signals-toolbox-window-and-menu-polish`
3. `subbundles/03-03-browser-proof-and-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Captured`
