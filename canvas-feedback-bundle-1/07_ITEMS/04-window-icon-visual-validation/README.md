# Item 04: Window icon visual validation

## Covered notes

- `N007`

## Objective

Finish the runtime part of the floating-window icon work so the controls are not merely mapped in code, but visibly render as black icons in the browser.

## Execution checklist

- Load the Font Awesome stylesheet in the app shell so shared icon classes paint in the browser.
- Keep floating-window action controls explicitly black in CSS.
- Capture screenshot evidence from the project structure page after the window renders.

## Implemented in

- `src/CanDoItAll.Web/Components/App.razor`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/canvas-workbench.css`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`

## Validation

- `Project_structure_feedback_fixes_are_validated_in_browser`
- `C:\repositories\CanDoItAll\output\playwright\feedback1\01-window-icon-actions.png`

## Status

`Done and validated`
