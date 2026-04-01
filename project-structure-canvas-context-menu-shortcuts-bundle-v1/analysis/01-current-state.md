# Current State

## Action Catalog And Menu Shape

- The shared workbench action model in `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchChrome.cs` has labels, icons, menu labels, tones, input metadata, and children, but no shortcut or accelerator field.
- The project-structure canvas already exposes the exact menu families the request references through `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCanvasCatalog.cs` and `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCanvasCatalog.RichDefinitions.cs`.
- The create catalog already includes more siblings than the architect listed, including additional block kinds, runtime items, assurance items, meetings, people, work variants, and infrastructure variants. That makes collision-safe extension a first-class requirement, not a nice-to-have.
- The node context menu also includes non-create actions such as `Open`, `Copy id`, `Summary`, `Connect`, `Reconnect`, `Progress`, `Marker`, `Priority`, `Validate`, `Test`, and `Delete` through `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureActionCatalogAdapter.cs`.

## Runtime Interaction State

- The keyboard handler in `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\05-viewport-and-events.js` currently supports global canvas shortcuts such as zoom, help, diagnostics, minimap, clipboard, `Tab`, `Enter`, and `Escape`, but it does not route printable keys into the open context menu.
- The menu renderer in `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\04-context-menu-and-composer.js` renders orbit layers, panel layers, submenu loading, and pointer-driven submenu opening, but it does not annotate labels with shortcut hints or build a layer-local key map.
- Shared menu helper functions such as `resolveMenuLabel`, `fitContextMenuLabel`, `createMenuActionIcon`, and `resolveMenuActionAriaLabel` live in `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\03-interaction-and-state.js`.

## Help Modal State

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Workbench\CanvasWorkbench.razor` currently renders the help overlay as one card with a two-column grid of short paragraphs.
- The existing help content mentions global interactions and a compressed line of keyboard shortcuts, but it does not document menu-layer navigation, nested submenu keys, or browsable documentation pages.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css\workbench\panels\03-help-settings-and-preview.css` styles the help overlay as a single scrollable card and has no page navigation or information-architecture affordances today.

## Maintainability Signals

- `03-interaction-and-state.js` is already large at roughly `51,813` bytes.
- `04-context-menu-and-composer.js` is similarly large at roughly `53,199` bytes.
- Adding shortcut-specific rendering, collision handling, and key-routing directly into the current monoliths without extracting a clear helper boundary would increase the maintenance risk the architect already called out.

## Existing Proof Seams

- Component tests already exist around the workbench shell, the project-structure action catalog adapter, and the project-structure catalog definitions:
  - `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanvasWorkbenchTests.cs`
  - `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureActionCatalogAdapterTests.cs`
  - `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureCanvasCatalogTests.cs`
- Playwright coverage already exists for the project-structure canvas route and can be extended rather than recreated:
  - `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureCanvasFeedbackBundle.cs`
