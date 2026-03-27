# Cross-Repo Dependency Map

## Current CanDoItAll Project References

Key files:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\CanDoItAll.ComponentKit.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.*\*.csproj`

Observed state:

- `CanDoItAll.Web` references both `CanDoItAll.ComponentKit` and `CanDoItAll.Components`.
- multiple CanDoItAll modules reference both `CanDoItAll.ComponentKit` and `CanDoItAll.Components`.
- `CanDoItAll.ComponentKit` itself references `CanDoItAll.Components`.

Implication:

- current boundaries are inverted and overlapping
- the extraction must break this cycle by turning `BaseLib` and `CanvasLib` into clear lower layers

## Current Zyphonote Project References

Key files:

- `C:\repositories\Zyphonote\src\App.Blazor\Zyphonote.App.csproj`
- `C:\repositories\Zyphonote\src\App.Web\Zyphonote.App.Web.csproj`
- `C:\repositories\Zyphonote\src\App.Components\Zyphonote.App.Components.csproj`

Observed state:

- `App.Blazor` references `CanDoItAll.ComponentKit`
- `App.Blazor` references `App.Components`
- `App.Web` references `App.Blazor`
- `App.AI.TranscriptionLab` and `App.PdmxTool` also reference `App.Components`

Implication:

- Zyphonote already depends on CanDoItAll canvas, but it still owns its own wrapper/component layer
- the new split must preserve this dependency direction while moving the wrapper layer to `BaseLib`

## Current Runtime Asset Includes

### CanDoItAll

`C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\App.razor`

- includes `_content/CanDoItAll.Components/css/output.css`
- includes `_content/CanDoItAll.ComponentKit/canvas-workbench.css`
- includes many `_content/CanDoItAll.ComponentKit/js/*` files

### Zyphonote

`C:\repositories\Zyphonote\src\App.Server\Components\App.razor`

- includes `_content/CanDoItAll.ComponentKit/canvas-workbench.css`
- includes `_content/Zyphonote.App/zyphonote-compat.css`
- includes `_content/Zyphonote.App.Components/css/output.css`
- includes app CSS and brand CSS
- includes `_content/CanDoItAll.ComponentKit/js/*`
- includes `planningCalendarInterop.js`
- includes Font Awesome CDN

Implication:

- asset rewiring is part of the migration, not cleanup after it
- apps must eventually reference the new library asset paths directly

## Current Cross-Repo Canvas Consumers In Zyphonote

Key pages:

- `C:\repositories\Zyphonote\src\App.Blazor\Pages\AccountEvents.razor`
- `C:\repositories\Zyphonote\src\App.Blazor\Pages\AccountLearningBuilder.razor`
- `C:\repositories\Zyphonote\src\App.Blazor\Pages\AccountLearningPackage.razor`
- `C:\repositories\Zyphonote\src\App.Blazor\Pages\AccountPlaylists.razor`
- `C:\repositories\Zyphonote\src\App.Blazor\Pages\PlaylistReview.razor`

Key services:

- `C:\repositories\Zyphonote\src\App.Blazor\Services\PlanningCalendarExportService.cs`
- `C:\repositories\Zyphonote\src\App.Blazor\Services\PlanningWorkspaceService.cs`

Implication:

- `CanvasLib` extraction must preserve these contracts and public shapes
- contract-breaking cleanup must be staged behind app-local adapters, not done inline

## Existing Test Surfaces To Reuse

### CanDoItAll

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright`

Current CanDoItAll component tests already cover many canvas runtime pieces and some shell components. Reuse this project first.

### Zyphonote

- `C:\repositories\Zyphonote\tests\App.Web.PlaywrightTests`
- `C:\repositories\Zyphonote\tests\App.PdmxTool.PlaywrightTests`

These are the right places for regression validation during Zyphonote adoption.
