# Detailed findings

## Finding 1 — the current `CanvasWorkbench` is the right shape to mirror

The existing canvas workbench already behaves like a host component over a JS runtime:

- the Blazor component holds the host element and typed surface,
- the JS runtime creates and updates the actual scene,
- tests call semantic helpers such as scene snapshot and drag simulation.

That pattern should be reused instead of reinvented.

## Finding 2 — the current automation surface is the best blueprint for WebGL proof

The current canvas runtime already exposes methods such as:

- `create` / `update` / `dispose`
- `fitView` / `focusNode` / `getState`
- `getDiagnostics` / `getSceneSnapshot` / `exportImageData`
- `simulateDrag` / `finishInteraction`

The WebGL concept should deliberately mirror that style under a new namespace such as `window.CanDoItAll.webglWorkbench`.

## Finding 3 — process semantics already exist and should not be re-invented

The current Processes module already knows:

- how to build stable role/step/branch IDs,
- how to categorize connections,
- how to project templates into current editor models,
- how to express current canvas coordinates.

The concept should reuse those semantics and add limited Z-depth, not replace them.

## Finding 4 — a dedicated sandbox project is better than extending the current shared Components sandbox

The existing `CanDoItAll.Components.Sandbox` project demonstrates good catalog conventions, but the user explicitly asked for a **new sandbox project**. That is appropriate because the WebGL concept needs:

- its own route space,
- its own startup/project references,
- its own proof matrix,
- its own representative templates and review notes.

## Finding 5 — the first concept must be sandbox-only and non-persistent

The user asked for a concept branch. The safest way to respect that is:

- do not replace `ProcessWorkspace`,
- do not write back to process persistence,
- keep edits in a sandbox state holder,
- use screenshots and scene snapshots to judge value before any production pilot.
