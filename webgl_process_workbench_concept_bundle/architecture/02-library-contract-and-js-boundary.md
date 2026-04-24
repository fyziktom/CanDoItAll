# Library contract and JS boundary

## Proposed generic contract family

Suggested initial types:

- `WebGlWorkbenchSurface`
- `WebGlWorkbenchNode`
- `WebGlWorkbenchEdge`
- `WebGlWorkbenchAnchor`
- `WebGlWorkbenchCameraState`
- `WebGlWorkbenchUiState`
- `WebGlWorkbenchDiagnostics`
- `WebGlSelectionChangedEvent`
- `WebGlNodeMovedEvent`
- `WebGlConnectionChangeRequest`
- `WebGlAutomationSnapshot`

## Boundary rule

### C# owns

- typed data contracts,
- host lifecycle,
- event callback dispatch,
- inspector chrome and sandbox state holder,
- reset/reload decisions.

### JavaScript owns

- renderer lifecycle,
- frame loop,
- camera math,
- hit-testing,
- drag preview and snapping,
- edge preview,
- screenshot export,
- semantic scene snapshots.

## DOM mirror rule

The library must maintain a DOM mirror layer containing stable node/port/edge anchors so that:

- labels remain crisp,
- accessibility is possible,
- Playwright/MCP can inspect screen-space semantics,
- screenshot review can be linked back to stable IDs.

## Asset rule

Use repository-local assets with deterministic loading. Do not introduce a CDN dependency into the concept branch.
