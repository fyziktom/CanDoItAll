# 02 Fix Blocks Explorer Search Scroll And Browser Proof

## Status

- `Ready`

## Objective

Prove that blocks explorer search results remain scrollable and readable inside the window and capture the browser screenshots that close the feedback.

## Covered Inputs

- `N004`
- `R004`
- `R005`
- `R006`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`
- `C:\repositories\CanDoItAll\canvas-feedback-bundle-5\inputs\feedback5-media\image1.png`
- `C:\repositories\CanDoItAll\canvas-feedback-bundle-5\inputs\feedback5-media\image2.png`

## Deliverables

- scroll-safe toolbox search results inside the visible explorer window
- browser assertions that visible results retain readable labels and icons
- screenshot artifacts that prove the shipped explorer state

## Implementation Steps

1. Recheck the toolbox body and sections overflow rules after the shared header returns.
2. Extend browser validation so it proves:
   - the explorer still fits within the floating window
   - wheel or scroll input moves the visible result set
   - visible result labels stay readable after scrolling
3. Capture screenshot artifacts for the default explorer and for the filtered or scrolled state.
4. Update the execution report with the exact browser test command, results, and artifact paths.

## Scope Exceptions

- none

## Do Not Do

- do not close this subbundle with DOM-only assertions and no screenshots
- do not rely on hidden overflow that only works in component tests
- do not remove search expansion behavior just to make scrolling easier

## Acceptance Checklist

- search results move inside the explorer when the user scrolls
- visible results keep readable text and icons
- screenshot proof exists for the relevant search states
- bundle execution notes include the artifact paths and commands that produced them

## Proof Required

- targeted Playwright pass
- screenshot artifacts for the validated browser states
- updated execution report with exact commands and screenshot paths

## Suggested Agent Prompt

```text
Implement subbundle 02 only.

Validate the blocks explorer in the browser after the shared window chrome returns. The goal is not just passing DOM checks; prove that search results scroll inside the visible window and that the visible labels remain readable in captured screenshots.
```
