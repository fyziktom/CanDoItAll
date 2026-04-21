# Target Solution

## Runtime Decomposition

- Keep `01-webgl-workbench.js` as the stable entry module that exposes `window.CanDoItAll.webglWorkbench`.
- Move implementation details into smaller ES modules under the same workbench folder with clear ownership such as:
- runtime bootstrap and shared constants
- camera and viewport utilities
- scene graph and renderables
- label and overlay projection helpers
- in-scene HUD or chrome rendering
- input routing and tool-mode logic
- command/event bridging to .NET
- Prefer a small number of cohesive classes or controllers over another flat pile of utility functions. A likely shape is `WebGlWorkbenchRuntime`, `WebGlSceneController`, `WebGlHudController`, and `WebGlInteractionController`.

## WebGL Chrome Model

- Render the requested top toolbar and right-click menu in the stage itself, using a dedicated HUD layer that is part of the WebGL runtime rather than ordinary page HTML controls.
- Back the HUD with explicit runtime state for:
- active tool mode
- node info mode
- grid visibility
- anchor visibility or edge-label visibility
- diagnostics toggle
- Keep transient menu-open state local to the runtime, but persist stable settings in `WebGlWorkbenchUiState`.

## Authoring Contract

- Preserve existing selection, drag, and connection contracts where possible.
- Add a deletion command contract for the sandbox host when the runtime needs the host/session to mutate data.
- Allow connect and reconnect flows to stay model-aware by continuing to route supported connection mutations through `ProcessWebGlSceneAdapter`.
- Any fallback or limitation for deletion or anchor disambiguation must be documented explicitly in the bundle and final closure notes.

## Sandbox Boundary

- The sandbox page should keep high-level page framing, template metadata, and command-log display.
- The stage-local authoring controls should move into the WebGL surface so the proof demonstrates the requested direction.
- The sandbox session remains the source of truth for the current template, layout mode, spacing, selected node, command log, and any resettable sandbox-only mutations.

## Testing And Proof Strategy

- Keep or update the current .NET regression coverage for interop, UI-state parsing, scene adaptation, and sandbox session behavior.
- Refresh Playwright coverage to target the live stage-local toolbar, settings menu, context menu, and authoring actions.
- Use Playwright MCP manual proof on the live route to capture screenshots of the toolbar/menu open state and at least one real authoring flow after the automated suite is green.
