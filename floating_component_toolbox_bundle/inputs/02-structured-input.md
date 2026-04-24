# Structured Input

## Objectives

- Provide a generic floating component toolbox that can render different catalogs for different workbenches.
- Keep project structure, process canvas, prompt factory, and WebGL behavior working as before.
- Add WebGL toolbox support for adding a role and prove the new role appears in the 3D scene.
- Validate project structure toolbox creation by adding a real block and proving it appears on the canvas.

## Hard Constraints

- Preserve existing creation logic and persistence semantics in project structure and process canvases.
- Do not replace canvas-specific domain models with a lowest-common-denominator data type that loses required metadata.
- Use Playwright MCP and screenshots for browser-visible behavior.
- Keep existing floating window minimize, hide, drag, and toolbar restore behavior intact.

## Assumptions

- The reusable toolbox can live in `CanDoItAll.Components.OverlayLib` because it must work over both CanvasLib and WebGL.
- Host modules should adapt their domain-specific catalogs into generic view models, then keep their existing action handlers.
- WebGL role addition can be sandbox-local and in-memory, matching the current WebGL sandbox edit model.

## Validation Expectations

- Build the changed shared/component projects.
- Run targeted component tests when practical.
- Use Playwright MCP on WebGL sandbox and CanDoItAll Web project/process routes.
- Capture screenshots showing open toolbox states and post-add canvas/3D results.
