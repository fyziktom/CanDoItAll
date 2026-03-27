# Normalize file-type badges and selection panel color semantics

## Status

- `Completed`

## Objective

- Remove duplicate file subtype signals from the selection panel and render file-type badges with clear semantic colors and readable contrast.

## Covered Inputs

- `R004`
- `R007`
- `R008`
- Raw note `N005`
- Raw note `N007`
- Live finding: the Excel selection panel currently shows both `Type: excel` and an `Excel` badge while also repeating upload state.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureNodeDescriptor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureGraphAdapter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`

## Deliverables

- File-node selection panels no longer repeat subtype text when the badge already carries that meaning.
- File badges use type-specific semantic colors with readable text contrast.
- The selection panel and canvas-facing file visuals stay aligned.

## Implementation Steps

1. Trace file subtype, badge, and fact composition through the current model and descriptor helpers.
2. Remove duplicate subtype or upload signals from the selection panel where the badge or existing UI already expresses the same information.
3. Centralize semantic badge styling in the existing file visual-profile path where practical.
4. Update or add tests that cover file badge rendering and duplicate suppression.
5. Validate the changed file nodes in the real browser and capture screenshots.

## Scope Exceptions

- This phase should not revisit toolbox placement unless a prior change directly broke badge rendering.

## Do Not Do

- Do not add a second badge-color mapping path.
- Do not keep both subtype fact text and subtype badge for the same file meaning.
- Do not use low-contrast badge colors.

## Acceptance Checklist

- File-node selection panels do not repeat subtype meaning in both text and badge form.
- Badge colors clearly differentiate at least the file types covered by the feedback, including Excel and PDF.
- Badge text remains readable on the chosen background colors.
- The real browser view matches the implemented semantic styling.

## Proof Required

- Browser pass at `1600x1000`.
- Screenshot of a selected Excel node showing the new badge color and no duplicate subtype text.
- Screenshot of another file type when available, or a code/test assertion proving the alternate semantic mapping.
- DOM or assertion proof that removed subtype labels are absent.

## Browser Validation Logging

- Route: `http://127.0.0.1:5188/projects/{id}/structure`
- Viewports: `1600x1000`
- Required Playwright MCP actions:
- Select or create at least one Excel file node.
- Verify that duplicate subtype text is absent from the selection panel.
- Verify the badge styling for Excel and at least one other mapped file type by screenshot or assertion.
- Required screenshots:
- `C:\repositories\CanDoItAll\output\playwright\feedback8\subbundle-03-badges-desktop.png`
- `C:\repositories\CanDoItAll\output\playwright\feedback8\subbundle-03-badges-secondary-type.png` when a second type is shown in-browser

## Completion Notes

- Implemented and validated on the live route `http://127.0.0.1:5188/projects/f95ee2d4-166d-4ace-81ae-8b370730abd5/structure`.
- Excel badge proof: `C:\repositories\CanDoItAll\output\playwright\feedback8\subbundle-02-03-excel-selection-panel.png`
- PDF badge proof: `C:\repositories\CanDoItAll\output\playwright\feedback8\subbundle-03-pdf-selection-panel-badges.png`
- Playwright DOM checks confirmed the selected PDF node rendered `Status`, `FilePdf`, and `Uploaded` badge styles, while the selected Excel node rendered `Status`, `FileExcel`, and `Uploaded` without duplicate subtype facts.

## Suggested Agent Prompt

```text
Implement subbundle 03 only. Remove repeated file subtype signals from the selection panel, apply semantic file-type badge colors through the existing profile path, and prove the result in the real browser with screenshots and analytics logging before closing the subbundle.
```
