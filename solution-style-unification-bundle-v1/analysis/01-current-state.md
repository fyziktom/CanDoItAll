# Current State

## Styling Pipeline

- Tailwind v4 is compiled from `C:\repositories\CanDoItAll\Tailwind\input.css` into `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\wwwroot\css\output.css`.
- The current `input.css` is still a monolithic `@layer components` file. It mixes Radzen helpers, shared sheet classes, tag-editor styling, and metric cards in one place.
- `CanDoItAll.Web\wwwroot\app.css` remains small and global, but several pages and components still rely on page-scoped `.razor.css` files.

## Census Baseline

- Initial non-canvas census workbook: `C:\repositories\CanDoItAll\output\spreadsheet\style-census-initial.xlsx`
- Tailwind-like raw HTML element occurrences outside excluded canvas scope: `968`
- Distinct exact class patterns: `373`
- Distinct normalized patterns after numeric/arbitrary-value normalization: `300`
- Distinct family signatures for similarity grouping: `214`
- The highest-concentration tags are `div` (`446`), `p` (`189`), `label` (`108`), `span` (`97`), and `button` (`62`).

## Highest-Churn Files

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor` with `242` non-canvas Tailwind-like raw HTML occurrences
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\ProjectStructureAgentSettingsPanel.razor` with `86` occurrences
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Resources\Pages\ResourcesPage.razor` with `72` occurrences
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Prompts\Pages\PromptGalleryPage.razor` with `63` occurrences
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components\Components\AppShell.razor` with `59` occurrences
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor` with `56` occurrences
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.razor` with `56` occurrences

## Repeated Raw Utility Patterns

- Label pattern repeated `88` times: `mb-1 block text-sm font-medium text-slate-700`
- Primary dark action button repeated `21` times: `inline-flex items-center rounded-xl bg-slate-900 px-4 py-2 text-sm font-semibold text-white`
- Light secondary button variants appear with slightly different padding, borders, and background states across at least `35` occurrences.
- Common spacing shells are repeated heavily: `space-y-3` (`26`), `space-y-1` (`22`), `grid gap-4 md:grid-cols-2` (`21`), `space-y-4` (`19`), `flex flex-wrap items-center gap-2` (`18`).
- Section meta text also repeats with near-identical tracking and color values, especially on `p` and `span` tags.

## Custom CSS Hotspots Outside Excluded Scope

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor.css` with `189` selectors and `1305` lines
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\wwwroot\sandbox.css` with `55` selectors and `344` lines
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\Tabs.razor.css` with `44` selectors and `329` lines
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\ReconnectModal.razor.css` with `27` selectors and `157` lines
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\HelpPopover.razor.css` with `14` selectors and `98` lines
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\wwwroot\app.css` with `8` selectors and `49` lines

## BaseLib Readiness

- BaseLib already exposes reusable primitives for buttons, cards, forms, layout, navigation, typography, badges, lists, feedback, and dialogs.
- The component library is broad enough to absorb much of the repeated raw styling, but several app/module surfaces still bypass it and render raw HTML with repeated utility strings.
- The Tailwind output already contains some semantic shared classes such as `zy-sheet-*`, `zy-tag-textedit*`, and `zy-stat-card*`, but those classes are not yet organized into imported files and they do not cover the full repeated markup surface.

## Excluded Scope Captured For This Wave

- CanvasLib itself is excluded.
- Canvas-host and canvas-preview files discovered during repo inspection are explicitly listed in `inventories/01-scope-inventory.md` and the workbook sheet `ExcludedCanvasScope`.
