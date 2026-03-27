# Current State

## Verified Owners

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureActionCatalogAdapter.cs`
  - defines progress, marker, and priority submenu actions
  - currently keeps submenu menu labels outside the icon and marks all three preset families as `compact-ring`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js`
  - renders progress badges, menu action icons, action metrics, compact-ring offsets, submenu opening, and layer clamping
  - currently opens nested menus immediately on hover and clamps against the host rectangle rather than a toolbar-safe region
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\canvas-workbench.css`
  - controls progress ring size, menu hex sizing, label visibility, and radial menu appearance
  - current progress and marker presets are too small for readable in-icon text and can overlap when scaled
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`
  - already exercises progress, marker, and priority submenus in the browser
  - current proof checks basic size relationships, but does not validate toolbar-safe placement, hover delay, or hive-style staggering

## Current Gaps Against Feedback

- Progress submenu items show the text label under the hex instead of inside the progress icon.
- Compact-ring offsets place submenu items on a simple circle rather than a tighter staggered hive pattern.
- Marker and progress preset metrics are too small for the requested diameter increase.
- Nested submenu placement can still push items into the toolbar band because host clamping does not reserve that safe zone.
- Hovering a parent action opens the submenu immediately with no visible wait-state indicator.
