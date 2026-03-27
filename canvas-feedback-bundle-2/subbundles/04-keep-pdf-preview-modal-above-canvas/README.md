# 04 Keep PDF Preview Modal Above Canvas

## Objective

Ensure preview-style dialogs, especially PDF preview, render inside the canvas overlay shell so they cannot appear behind the canvas.

## Covered Inputs

- `N004`
- `R004`
- `R005`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`

## Deliverables

- preview dialogs rendered through the `CanvasWorkbench` overlay slot
- backdrop styling scoped to the canvas stage instead of the whole viewport
- PDF preview still opens without navigation and keeps its existing actions

## Implementation Steps

1. Move the preview-style dialog markup into the canvas overlay content block.
2. Reuse the existing dialog markup and handlers rather than cloning a second preview implementation.
3. Update the backdrop styling so the overlay belongs to the canvas shell.
4. Re-run the existing preview behavior test and extend it only if necessary.

## Do Not Do

- do not replace the preview dialog with browser navigation
- do not special-case PDF if the same placement fix can safely cover summary and Mermaid dialogs too

## Acceptance Checklist

- opening PDF preview renders the dialog inside the canvas shell
- preview dialog still shows close and new-tab actions
- existing component coverage for preview behavior still passes

## Proof Required

- focused component test pass
- execution report updated with the command and outcome

## Suggested Agent Prompt

```text
Implement subbundle 04 only.

Move the project structure preview dialogs into the CanvasWorkbench overlay slot so PDF preview stays above the canvas. Reuse the existing dialog markup and keep the existing preview behavior intact.
```
