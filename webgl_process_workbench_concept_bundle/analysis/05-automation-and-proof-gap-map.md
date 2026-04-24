# Automation and proof gap map

## What the current repository already proves

The current canvas runtime already demonstrates a good browser-proof pattern:

- semantic helper methods live on a global runtime namespace,
- tests read host state instead of parsing pixels,
- screenshots complement semantic assertions instead of replacing them.

## What WebGL adds that must be addressed explicitly

- geometry is not directly inspectable in the DOM,
- raw pointer automation is brittle for 3D hit-testing,
- labels may be harder to read if they are rendered as textures,
- camera movement can make screenshots non-repeatable.

## Required concept answer

The WebGL concept must add:

- `host.__webglWorkbenchState` or equivalent debug state,
- `getSceneSnapshot` with visible nodes, edges, camera, and projected anchors,
- `exportImageData`,
- `simulateDrag`,
- `simulateConnection` or equivalent semantic connection helper,
- `finishInteraction`,
- DOM mirror anchors with stable `data-*` IDs.

## Proof consequence

If the concept does not provide the semantic bridge above, screenshots alone are insufficient and the automation phase must fail.
