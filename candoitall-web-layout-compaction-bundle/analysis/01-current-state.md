# Current State Analysis

## Shell And Surface Width

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components\Components\AppShell.razor`
  - The shell renders a permanent large sidebar plus an optional `20rem` right rail.
- `C:\repositories\CanDoItAll\Tailwind\navigation\workbench-shell.css`
  - `.cda-shell-frame` caps the entire application at `max-w-[1840px]` with outer padding.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Layout\PageScaffold.razor`
  - Standard pages default to `max-w-[1500px]`, which compounds the shell cap and creates avoidable unused space on large desktops.

### Impact

- The app already pays for a wide shell, but the body content still constrains itself aggressively.
- On routes that also have a top bar, pinned tab strip, page header, summaries, and list/detail shells, the remaining workspace feels artificially narrow relative to the available viewport.

## Repeated Vertical Stack Pattern

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\PageHeader.razor`
  - Full descriptions are visible by default unless the caller opts into `Compact`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Cards\SummaryTiles.razor`
  - Summary tiles form a full-width multi-card band with helper copy, which is useful but costly in vertical space.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Lists\ListPanelHeader.razor`
  - List pages commonly stack title, description, count, optional actions, then filters.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\FilterBar.razor`
  - The filter bar is generic and visually clean, but it does not provide a dedicated large-screen search-plus-filters pattern; pages decide their own arrangement ad hoc.

### Impact

- Multiple routes spend the first screen on orientation copy instead of actionable controls.
- The repeated header plus summaries plus tabs or list-header pattern means each route burns space differently, with no single density rule.

## Projects Route

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor`
  - The route adds a page header before rendering the board.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectsBoard.razor`
  - The board hardcodes `style="height: calc(100vh - 15rem);"` and spends its command bar on:
    - a full descriptive paragraph
    - status pills
    - a separate action row
    - a search control
    - a second wrapped row for three filters plus reset
- Baseline browser observation from `/projects`
  - The large-screen first viewport still shows stacked board controls instead of letting search, filters, and reset live on one toolbar row.
  - The startup database modal also appears immediately in the current workspace, so modal layout quality matters on first paint.

## Operational List/Detail Routes

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Resources\Pages\ResourcesPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Prompts\Pages\PromptGalleryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Validation\Pages\ValidationCenterPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.TestLab\Pages\TestLabPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Activity\Pages\ActivityPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Pages\AutomationPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor`

### Shared Pattern

- Most routes use:
  - `PageHeader`
  - summary tiles or tabs
  - `ListDetailShell`
  - `ListPanelHeader`
  - `FilterBar`
- The structure is consistent, but density is not optimized:
  - headers carry long descriptions
  - some routes add summary tiles before tabs or before list/detail content
  - list filters often sit below list title and description instead of collapsing into a tighter toolbar band

### Settings Baseline

- Baseline browser observation from `/settings`
  - The route shows large header copy, summary tiles, and tab controls before the primary form surface.
  - This confirms the projects-page complaint is part of a broader density pattern, not a single-route exception.

## Modal And Overlay Fragmentation

- Shared dialog:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Modals\Dialog.razor`
  - Uses generous padding and a max-height shell; suitable but not yet tuned for compact operational modals.
- Projects modals:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectModalHost.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectHierarchyModal.razor`
- Shell database modal:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.razor`
- Prompt factory dialogs:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\Components\PromptFactoryDialogs.razor`
  - These are custom backdrops and modal wrappers, not the shared BaseLib dialog.
- Workbench overlays:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureOverlayDialog.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureCanvasDialogs.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureSupportDialogs.razor`

### Impact

- There is no single modal density strategy.
- Shared dialog improvements alone will not fix prompt factory or project structure overlays.
- Open-state proof is required because clipping and layering risks differ across these systems.

## Shared Component Flexibility

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\TextBox.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\DropDown.razor`
  - Both already default to `w-full`, which is directionally correct.
  - Several pages still bypass them in favor of raw `InputText`, `InputSelect`, or custom field wrappers.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\HelpPopover.razor`
  - A reusable small help affordance already exists and can support the requested `?` pattern.

## Tailwind Build State

- `C:\repositories\CanDoItAll\Tailwind\input.css`
  - Imports theme, layout, surface, control, and navigation modules and points `@source` at `../src/CanDoItAll.Components.BaseLib`.
- Tailwind watch status during preparation
  - Not running at the start of the investigation.
  - Started successfully with output logged under:
    - `C:\repositories\CanDoItAll\output\tailwind\watch.stdout.log`
    - `C:\repositories\CanDoItAll\output\tailwind\watch.stderr.log`

