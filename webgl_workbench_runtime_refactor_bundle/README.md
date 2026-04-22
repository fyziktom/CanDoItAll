# WebGL Workbench Runtime Refactor Bundle

This bundle is a coordination and execution package for `webgl_workbench_runtime_refactor_bundle`.

## Profile

- `initiative`

## Mission

- Refactor the WebGlLib workbench runtime into smaller logical classes and helper modules, then add the missing in-scene authoring chrome the user asked for: a WebGL-drawn top toolbar, a WebGL-drawn right-click context menu, explicit tool modes for selection, delete, connect, and reconnect, plus settings for node-info density and other useful scene options. The completed work must stay provable on the real sandbox route with Playwright MCP actions, screenshots, and updated automated regression coverage.

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

1. `subbundles/01-runtime-foundation-refactor-and-api-shaping`
2. `subbundles/02-in-scene-toolbar-and-settings-chrome`
3. `subbundles/03-3d-connection-reconnection-and-delete-tools`
4. `subbundles/04-sandbox-integration-regression-proof-and-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.

## Validation Summary

- Bundle preparation status: `Prepared and validated on 2026-04-21 from repo-backed analysis`
- Execution status: `Implemented, regression-repaired, and extended with follow-up runtime split, camera-view controls, new 3D layout algorithms, model-based role/step/branch visuals, closer role anchors, start/end flow markers, zoom-conditional anchor labels, explicit anchor-to-anchor authoring, an icon-first single-row maximize/dock toolbar, and a shared overlay-window component library adopted in WebGl first and then swapped back into CanvasLib`
- Subbundle gate review: `Completed with residual Playwright fixture-host instability noted in reviews/01-execution-report.md`
- Final closure gate: `Implemented with documented residuals`
- Browser validation analytics: `Manual Playwright MCP proof complete, including the 2026-04-21 repair pass that restored node/edge visibility and reduced chrome-render overhead plus the 2026-04-22 follow-up proof for host/in-scene camera switching, the new critical-path-spine, fan-out-corridor, and radial-burst layouts, the GLB-driven role/step/branch node visuals with start/end flow markers, the zoom-conditional anchor-label plus explicit target-input connection flow, the icon-first single-row maximize/dock toolbar on the dedicated sandbox host, and the shared overlay-window extraction validated across the WebGL sandbox plus the CanDoItAll project-structure and process canvases; focused automated proof passed for the toolbar/chrome smoke and targeted component coverage passed for the shared overlay wrapper`
