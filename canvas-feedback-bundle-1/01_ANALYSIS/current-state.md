# Current-state analysis

## Verified findings

1. The first pass solved the long create-form and icon-only chrome requirements, but the browser still showed empty or weak action icons until the Font Awesome stylesheet was loaded globally and the action color was forced to black.
2. The toolbox still rendered duplicate chrome: the outer `CanvasFloatingWindow` header plus the inner dark toolbox surface. The user was right that the white wrapper copy was redundant.
3. Headerless floating windows still used the shared `auto minmax(0, 1fr)` grid. That left body-only windows under-constrained until the shared floating-window layout learned a headerless mode.
4. The toolbox needed explicit accordion state in page code, not passive markup. Matching groups also needed to open during search.
5. Search-result icons were still unreliable because several toolbox/file tokens were missing from the shared icon catalog and the browser lacked the stylesheet needed to paint Font Awesome classes.
6. File nodes still used the generic `sky` palette because the graph adapter ignored file subtypes.
7. The PDF preview backdrop was still layered below the maximized workbench shell.
8. Raw `scrollTop` and `scrollHeight` assertions were brittle in the transformed workbench surface. The browser validation had to prove visible content movement, not only DOM scroll metrics.

## Implementation constraint

The correct change set was still the smallest shared fix plus page-local overrides. The user feedback did not justify a new toolbox component or a different preview system. It justified finishing the shared floating-window behavior, tightening the toolbox runtime surface, and adding browser evidence where the first pass was too optimistic.

## Affected code paths

- `src/CanDoItAll.Components.BaseLib/Components/Identity/FontAwesomeIconCatalog.cs`
- `src/CanDoItAll.Components.CanvasLib/Components/CanvasFloatingWindow.razor`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/canvas-workbench.css`
- `src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureGraphAdapter.cs`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ToolWindows.cs`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor.css`
- `src/CanDoItAll.Web/Components/App.razor`
- `tests/CanDoItAll.Tests.Components/CanvasFloatingWindowTests.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructureGraphAdapterTests.cs`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`
