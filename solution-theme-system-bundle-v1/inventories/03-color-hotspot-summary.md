# Color Hotspot Summary

## Inventory Result

- Shared Tailwind files still encode palette values directly for buttons, tabs, treeview, workbench shell surfaces, cards, forms, typography, overlays, and page headers.
- Real routes still embed palette utilities in module pages and web layout files, especially around neutral text, borders, backgrounds, and status emphasis.

## Highest-Value Shared Hotspots

- `C:\repositories\CanDoItAll\Tailwind\controls\buttons.css`
- `C:\repositories\CanDoItAll\Tailwind\navigation\workbench-shell.css`
- `C:\repositories\CanDoItAll\Tailwind\navigation\tabs.css`
- `C:\repositories\CanDoItAll\Tailwind\navigation\treeview.css`
- `C:\repositories\CanDoItAll\Tailwind\surfaces\cards.css`
- `C:\repositories\CanDoItAll\Tailwind\forms\fields.css`

## Highest-Value Route Hotspots

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Resources\Pages\ResourcesPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Prompts\Pages\PromptGalleryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectModalHost.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectCalendarPage.razor`

## Migration Rule

- Theme-token adoption must start in shared Tailwind/BaseLib primitives first.
- Route-level cleanup comes after the primitives expose a stable semantic contract.
