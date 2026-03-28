# Project Structure Node Recomposition Initiative

This bundle is a coordination and execution package for `project-structure-node-recomposition-bundle-1`.

## Profile

- `initiative`

## Mission

- Add a manual toolbar-triggered subtree recomposition command to the project structure canvas so the currently selected node stays anchored, first-layer descendants are placed into balanced clockwise clock-face slots, deeper descendants stay inside their parent branch sector or “bubble”, existing connections remain untouched, positions persist across reloads, group spacing stays readable instead of over-packed, and the final layout is collision-free against both recomposed nodes and untouched canvas nodes.

## Bundle Layout

- `inputs/` raw request, screenshot note, and normalized task framing
- `inputs/03-follow-up-feedback.md` reopens the bundle with clock-face layering and branch-bubble separation requirements
- `analysis/` current implementation audit, algorithm comparison, assumptions, and risks
- `requirements/` normalized, testable requirements and scope boundaries
- `architecture/` target design, algorithm choice, persistence boundary, and rejected alternatives
- `inventories/` impacted production and test surfaces
- `plan/` execution order, dependency map, and subbundle gates
- `traceability/` raw-note and requirement coverage
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` self-review and execution proof
- `templates/` scaffold-local authoring material kept for future reuse

## Recommended Execution Order

1. `C:\repositories\CanDoItAll\project-structure-node-recomposition-bundle-1\subbundles\01-subtree-radial-layout-engine-and-persistence-foundation\README.md`
2. `C:\repositories\CanDoItAll\project-structure-node-recomposition-bundle-1\subbundles\02-toolbar-triggered-selected-subtree-recomposition-workflow\README.md`
3. `C:\repositories\CanDoItAll\project-structure-node-recomposition-bundle-1\subbundles\03-tests-browser-proof-and-closure-audit\README.md`

## Dependency And Validation Map

- Keep the dependency graph, critical-subbundle notes, and phase gates current in `C:\repositories\CanDoItAll\project-structure-node-recomposition-bundle-1\plan\01-phase-plan.md`.
- Do not start the toolbar workflow until the recomposition engine and persistence seam are proven against hierarchy data and collision cases.
- Do not close the bundle until browser proof shows the selected-subtree command compacts the layout around the selected node without node overlap and without changing link topology.

## Validation Summary

- Bundle preparation status: `Completed`
- Bundle readiness gate: `Completed`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed`
- Browser validation analytics: `Completed with follow-up proof`
