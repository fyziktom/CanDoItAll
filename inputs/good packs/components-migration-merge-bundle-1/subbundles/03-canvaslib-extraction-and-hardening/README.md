# 03 CanvasLib Extraction And Hardening

## Objective

Create `CanDoItAll.Components.CanvasLib` from the current CanDoItAll canvas source while keeping CanDoItAll as the source of truth for canvas behavior and preserving current Zyphonote consumers.

## Exact Source References

Primary canvas sources:

- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\Canvas`
- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\Components`
- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\wwwroot\js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\wwwroot\canvas-workbench.css`

Current CanDoItAll consumers:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectCalendarPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\App.razor`

Current Zyphonote consumers:

- `C:\repositories\Zyphonote\src\App.Blazor\Pages\AccountEvents.razor`
- `C:\repositories\Zyphonote\src\App.Blazor\Pages\AccountLearningBuilder.razor`
- `C:\repositories\Zyphonote\src\App.Blazor\Pages\AccountLearningPackage.razor`
- `C:\repositories\Zyphonote\src\App.Blazor\Pages\AccountPlaylists.razor`
- `C:\repositories\Zyphonote\src\App.Blazor\Pages\PlaylistReview.razor`
- `C:\repositories\Zyphonote\src\App.Blazor\Services\PlanningCalendarExportService.cs`
- `C:\repositories\Zyphonote\src\App.Blazor\Services\PlanningWorkspaceService.cs`
- `C:\repositories\Zyphonote\src\App.Server\Components\App.razor`

Sandbox-only exclusions:

- see `..\..\inventories\02-componentkit-and-app-component-classification.md`

## Implementation Steps

1. Create `CanvasLib` and move the full `Canvas` model/contract tree into it.
2. Move canvas runtime Razor components listed in the classification inventory.
3. Move `wwwroot\js` canvas assets and `canvas-workbench.css`.
4. Exclude preview/tuning components from `CanvasLib`; move them later to `Sandbox`.
5. Keep public canvas contract shapes stable for the first extraction wave.
6. Replace direct `CanDoItAll.ComponentKit` asset references with `CanvasLib` asset references after the library compiles.
7. Update current CanDoItAll consumers first.
8. Add or update component tests in `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components`.
9. Only after tests are stable, adapt Zyphonote consumer references.

## Hard Rules

- do not replace CanDoItAll canvas contracts with Zyphonote page-local code
- do not change canvas contract semantics and app adoption in the same commit if it can be avoided
- do not move preview/demo wrappers into runtime CanvasLib
- do not let `CanvasLib` depend on heavy CanDoItAll modules

## Acceptance Checklist

- `CanvasLib` compiles independently
- CanDoItAll canvas tests still pass
- existing public contract types used by Zyphonote still exist or are bridged safely
- new static asset paths are correct
- no sandbox/demo-only component remains in the runtime canvas library

## Proof Required

- file move map for canvas contracts/components/js/css
- updated asset include diff
- test results for canvas-related component tests
- one short compatibility note describing any adapter or temporary bridge kept for Zyphonote

## Suggested Agent Prompt

```text
Implement subbundle 03 only.

Extract CanDoItAll’s current canvas source into CanDoItAll.Components.CanvasLib. Preserve current contract behavior for both CanDoItAll and Zyphonote consumers. Move runtime JS and CSS with the library. Do not move preview or tuning components into the runtime canvas library, and do not begin broader app-specific component rewiring in this phase.
```
