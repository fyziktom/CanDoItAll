# Requirement Traceability

| Raw note | Normalized requirements | Impacted surface | Planned proof | Owning subbundle | Exception status |
| --- | --- | --- | --- | --- | --- |
| `N001` missing toolbox window actions | `R001`, `R005` | `ProjectStructurePage.razor`, `ProjectStructurePage.razor.css`, `CanvasFloatingWindow.razor` | component test plus browser screenshot of visible header actions | `subbundles/01-upgrade-blocks-explorer-window-chrome-and-accordion/README.md` | `None` |
| `N002` clicking sections should open items like an accordion | `R003`, `R005` | `ProjectStructurePage.ToolWindows.cs`, `ProjectStructurePage.razor` | component test for open group state plus browser proof of visible section body | `subbundles/01-upgrade-blocks-explorer-window-chrome-and-accordion/README.md` | `None` |
| `N003` use the shared in-canvas window with drag and dark mode | `R001`, `R002`, `R006` | `ProjectStructurePage.razor`, `ProjectStructurePage.razor.css`, shared floating-window contract | browser drag validation and screenshot of dark explorer inside shared shell | `subbundles/01-upgrade-blocks-explorer-window-chrome-and-accordion/README.md` | `None` |
| `N004` search results must scroll and stay readable, validated with screenshot | `R004`, `R005`, `R006` | `ProjectStructurePage.razor.css`, `AppSmokeTests.cs` | browser search-scroll assertions plus screenshots | `subbundles/02-fix-blocks-explorer-search-scroll-and-browser-proof/README.md` | `None` |
