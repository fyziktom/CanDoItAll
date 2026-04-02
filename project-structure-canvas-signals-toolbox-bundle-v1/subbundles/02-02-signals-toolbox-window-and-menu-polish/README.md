# Subbundle 02-02: Signals Toolbox Window And Menu Polish

## Status

- `Completed`

## Objective

- Add the floating node-signals toolbox and enlarge the marker submenu glyphs without changing their badge size.

## Covered Inputs

- `N001`
- `N002`
- `N003`
- `N004`
- `N005`

## Prerequisites

- `01-01-multi-marker-data-contract-and-rendering` is complete and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.ToolWindows.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureToolbarActions.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructureSelectionPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Core\CanvasFloatingWindow.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\03-interaction-and-state.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css\workbench\scene\04-scene-and-nodes.css`

## Deliverables

- Toolbar toggle for the signals toolbox.
- Floating overlay window with grouped signal sections and clear/reset helpers.
- Selection-aware apply behavior for markers, progress, and priority.
- Enlarged marker glyphs in the second-layer menu badge with unchanged badge geometry.

## Dependency Impact

- Final browser proof depends on this phase because it owns the new overlay composition and the requested menu visual fix.

## Validation Depth

- Real browser proof with desktop and narrower-width overlay checks.

## Implementation Steps

1. Add a new floating window state and toolbar toggle.
2. Build the grouped signals toolbox component.
3. Route marker, progress, and priority actions through the current selection.
4. Enlarge the marker submenu glyph while keeping badge width and height unchanged.
5. Tune overlay spacing, scrolling, and layering.

## Scope Exceptions

- The first shipped version does not need to replicate every XMind marker family. It needs a coherent grouped palette for the existing typed marker and signal vocabulary.

## Do Not Do

- Do not copy the XMind visual style literally.
- Do not create a toolbox that appears usable without any selected node context.

## Acceptance Checklist

- The top toolbar can show and hide the signals toolbox.
- The toolbox makes the selected node or selection context obvious.
- Clicking a marker or priority or progress control applies it immediately.
- The marker submenu glyph is visibly larger while the badge circle stays the same size.

## Proof Required

- Browser screenshots for the open toolbox and open marker submenu.
- Browser assertions for unchanged badge size and working selection-aware signal application.

## Browser Validation Logging

- Route: `http://127.0.0.1:5500/projects/2eac2cae-5138-437d-ac57-1a1b142ebccb/structure`
- Viewports: `1568x742` and `1100x900`
- Toolbar action: clicked `Signals`
- Marker submenu proof: opened context menu over the selected node and pressed `m`
- Badge sizing proof: `marker:question` badge stayed `37.84375x37.84375`; glyph font size measured `35.776px`
- Toolbox action proof: applied `Question`, `Risk`, `60%`, and `Priority 2`, then restored to original state
- Screenshots: `C:\repositories\CanDoItAll\output\playwright-mcp\marker-submenu-glyph-proof.png`; `C:\repositories\CanDoItAll\output\playwright-mcp\signals-window-desktop-proof.png`; `C:\repositories\CanDoItAll\output\playwright-mcp\signals-window-narrow-proof.png`
- Outcome: `Passed`

## Progression Gate

- Do not close this phase unless the open toolbox state and marker submenu state are both browser-verified.
- Gate result: `Passed`

## Suggested Agent Prompt

- Reuse the existing floating-window pattern to add a grouped node-signals toolbox, wire it to the current selection, then polish the context-menu marker glyph sizing without changing badge geometry.
