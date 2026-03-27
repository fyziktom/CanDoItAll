# 03-polish-settings-icon-and-toolbar-safe-offset

## Status

- `Ready`

## Objective

- Replace the `cfg` toolbar label with proper settings iconography and ensure the settings overlay never renders behind the toolbar.

## Covered Inputs

- `N004` use a settings icon instead of the `cfg` button.
- `N005` the configuration modal top is hidden behind the toolbar and must render lower.
- `R008`
- `R009`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\CanvasWorkbench.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\canvas-workbench.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`

## Deliverables

- Toolbar settings button iconography that matches the existing project style better than literal text.
- Toolbar-safe settings overlay positioning.
- Browser screenshots proving wide and narrower layout behavior.

## Implementation Steps

1. Replace the literal `cfg` toolbar marker with a settings icon using the project’s existing icon approach.
2. Adjust the settings overlay positioning logic and CSS so the card is centered within the usable stage area below the toolbar.
3. Confirm the overlay remains fully visible at maximized width and after a narrower-width follow-up pass.
4. Record the screenshots and proof in the execution report.

## Scope Exceptions

- Do not redesign unrelated toolbar actions or the rest of the canvas chrome.
- Do not widen this phase into a broader modal-theme refresh.

## Do Not Do

- Do not add a new icon library or unrelated dependency for one button.
- Do not solve the toolbar overlap with page-specific hacks if the issue belongs to shared canvas layout.
- Do not leave the settings overlay behavior proven only on one viewport width.

## Acceptance Checklist

- The toolbar no longer displays literal `cfg`.
- The toolbar displays recognizable settings iconography.
- Opening settings keeps the card fully below the toolbar.
- The settings card remains visibly usable at narrower width as well.

## Proof Required

- Run a maximized browser pass and save a screenshot of the opened settings overlay.
- Run a narrower-width follow-up pass and confirm the overlay still clears the toolbar.
- Add focused automated proof if the placement logic can be asserted reliably. Otherwise the browser screenshots are mandatory.

## Suggested Agent Prompt

```text
Implement feedback7 subbundle 03 only.

Keep the fix in shared CanvasLib toolbar and settings layout code. Replace the `cfg` label with settings iconography and make the settings overlay stay below the toolbar on both wide and narrower layouts.
```
