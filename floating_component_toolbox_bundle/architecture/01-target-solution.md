# Target Solution

## Shared Model

- Add generic toolbox models to OverlayLib, such as `OverlayToolboxSection`, `OverlayToolboxGroup`, `OverlayToolboxItem`, and optional item metadata.
- Keep the model presentation-oriented: labels, summary, icon/glyph, tone, disabled state, test id, item key/action id, secondary action metadata.
- Keep domain models in their owners and adapt them into the generic model at the boundary.

## Shared Component

- Add a reusable `OverlayComponentToolbox` body component in OverlayLib that renders:
- Header count and optional source/status chips.
- Search box bound to host-owned search text.
- Sections, groups, and items.
- Optional item secondary action for preview or details.
- Empty state.
- Stable data-testid hooks for Playwright.

## Host Integration

- Project structure keeps `ProjectStructureToolboxWindow` as a thin `CanvasFloatingWindow` wrapper and converts `ProjectStructureInspectorCreateGroup` to the generic model.
- Process canvas keeps `ProcessCanvasToolboxWindow` as a thin `CanvasFloatingWindow` wrapper and converts `ProcessCanvasToolboxGroup` to the generic model.
- Prompt factory keeps component-specific preview popover logic but moves the repeated toolbox section/group/item markup into the generic component.
- WebGL sandbox adds a new `OverlayWindow` toolbox instance and adapts process role templates into generic items.

## Validation Architecture

- Unit/component tests should exercise generic item rendering and event callbacks.
- Browser proof should use real routes and real add interactions.
- Screenshots should be stored under `C:\repositories\CanDoItAll\output\playwright-mcp\floating-component-toolbox\`.
