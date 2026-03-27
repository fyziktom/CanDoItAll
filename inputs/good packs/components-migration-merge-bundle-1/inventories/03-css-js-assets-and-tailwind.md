# CSS, JS, Assets, And Tailwind Inventory

## Current Tailwind Pipelines

### CanDoItAll

- source: `C:\repositories\CanDoItAll\Tailwind\input.css`
- package: `C:\repositories\CanDoItAll\Tailwind\package.json`
- output: `C:\repositories\CanDoItAll\src\CanDoItAll.Components\wwwroot\css\output.css`

### Zyphonote

- source: `C:\repositories\Zyphonote\Tailwind\input.css`
- package: `C:\repositories\Zyphonote\Tailwind\package.json`
- output: `C:\repositories\Zyphonote\src\App.Components\wwwroot\css\output.css`

## Current Custom CSS Sources In CanDoItAll

- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\Components\HelpPopover.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\wwwroot\canvas-workbench.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\ReconnectModal.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\wwwroot\app.css`

## Current Custom CSS Sources In Zyphonote Relevant To This Migration

Shared-wrapper level:

- `C:\repositories\Zyphonote\src\App.Components\Radzen\Blazor\Tabs.razor.css`

Candidate shared component level:

- `C:\repositories\Zyphonote\src\App.Blazor\Components\FactTable.razor.css`
- `C:\repositories\Zyphonote\src\App.Blazor\Components\ListItem.razor.css`
- `C:\repositories\Zyphonote\src\App.Blazor\Components\MarketplaceListingsGrid.razor.css`
- `C:\repositories\Zyphonote\src\App.Blazor\Components\NotationEditor.razor.css`
- `C:\repositories\Zyphonote\src\App.Blazor\Components\PlaylistOverviewCardsList.razor.css`
- `C:\repositories\Zyphonote\src\App.Blazor\Components\RepositoryGraphCanvas.razor.css`
- `C:\repositories\Zyphonote\src\App.Blazor\Components\SheetCard.razor.css`
- `C:\repositories\Zyphonote\src\App.Blazor\Components\Toolbar.razor.css`
- `C:\repositories\Zyphonote\src\App.Blazor\Components\ToolbarFields.razor.css`
- `C:\repositories\Zyphonote\src\App.Blazor\Components\ToolbarRow.razor.css`

App-global migration sources:

- `C:\repositories\Zyphonote\src\App.Blazor\wwwroot\app.css`
- `C:\repositories\Zyphonote\src\App.Blazor\wwwroot\brand.css`
- `C:\repositories\Zyphonote\src\App.Blazor\wwwroot\zyphonote-compat.css`
- `C:\repositories\Zyphonote\src\App.Server\wwwroot\css\server-shell.css`

## Current Shared Canvas JS Owned By CanDoItAll

Source root: `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\wwwroot\js`

- accessibility mirror, animation timeline, calendar bridges, floating window
- canvas primitives and workbench interop
- clipboard, connector, container, context menu, create action, diagnostics
- drag/drop, inspector, grid, group, hit-test, hover, keyboard, layout
- marquee, minimap, selection, snap guides, text measure, tooltip, transform handles, viewport

This entire directory is a `CanvasLib` concern.

## Zyphonote JS Files Relevant To Component Ownership

Source root: `C:\repositories\Zyphonote\src\App.Blazor\wwwroot`

- `planningCalendarInterop.js`
- `repositoryGraphCanvas.js`
- `scoreCreationWizard.js`
- `notationEditorPlayground.js`
- `harmonicAssistantCanvas.js`
- `harmonicMovementTrainerCanvas.js`
- `invisibleDrumsCanvas.js`
- `sightReadingCanvas.js`
- plus app utilities like `indexedDbInterop.js` and `fileDownloadInterop.js`

Only the integrations that are true shared canvas extensions should ever move to `CanvasLib`. Music/editor/product JS remains Zyphonote-specific.

## Icon And External Asset Findings

- `Zyphonote.App.Server` currently references Font Awesome from CDN in:
  - `C:\repositories\Zyphonote\src\App.Server\Components\App.razor`
- both wrapper libraries use `FontAwesomeIconCatalog.cs` as a material-token-to-Font-Awesome mapping layer
- the final shared library must own local icon assets instead of relying on CDN

## Tailwind Ownership Decision

- CanDoItAll owns the shared Tailwind sources for `Common`, `BaseLib`, and `CanvasLib`.
- Zyphonote may keep a Tailwind pipeline, but only for `Zyphonote.Components` and Zyphonote app-specific surfaces.
- Shared library CSS generation must not happen inside the Zyphonote repo once the migration lands.

## CSS Migration Rules

- Prefer inline utilities or `@layer components` sources over large free-form stylesheet copies.
- Convert isolated CSS to Tailwind only when it stays readable and stable.
- Keep isolated CSS for cases like complex tabs or canvas overlays when utility conversion would reduce clarity.
- Mine `zyphonote-compat.css` by component. Never import it into `BaseLib`.
- Keep `canvas-workbench.css` in `CanvasLib` first, then progressively move stable tokens into typed theme packs and smaller component CSS files.

## Recommended Shared Tailwind Layout

- `C:\repositories\CanDoItAll\Tailwind\input.shared.css`
  - outputs shared `BaseLib` CSS
- `C:\repositories\CanDoItAll\Tailwind\input.canvas.css`
  - outputs `CanvasLib` CSS if the canvas CSS is split later
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\...`
  - app-specific Tailwind input/output only for CanDoItAll app
- `C:\repositories\Zyphonote\Tailwind\input.app.css`
  - app-specific Zyphonote output only
