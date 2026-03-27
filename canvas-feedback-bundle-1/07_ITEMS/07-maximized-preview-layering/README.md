# Item 07: Maximized preview layering

## Covered notes

- `N014`

## Objective

Ensure that a PDF preview opened from the structure canvas stays above the maximized workbench shell instead of rendering behind it.

## Execution checklist

- Raise the preview backdrop above the maximized shell.
- Double-click a PDF node while the canvas is maximized.
- Confirm the dialog owns the visual center point and capture a screenshot.

## Implemented in

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor.css`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`

## Validation

- `Project_structure_feedback_fixes_are_validated_in_browser`
- `C:\repositories\CanDoItAll\output\playwright\feedback1\03-maximized-pdf-preview.png`

## Status

`Done and validated`
