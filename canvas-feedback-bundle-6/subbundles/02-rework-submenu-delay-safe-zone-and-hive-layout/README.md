# 02 Rework Submenu Delay Safe Zone And Hive Layout

## Status

- `Ready`

## Objective

Rebuild nested submenu opening and placement so delayed layers feel intentional, stay below the toolbar, and arrange their hexes in a hive-style stagger rather than a simple ring.

## Covered Inputs

- `N004`
- `N005`
- `N006`
- `R004`
- `R005`
- `R006`
- `R007`
- `R008`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\canvas-workbench.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`
- `C:\repositories\CanDoItAll\canvas-feedback-bundle-6\inputs\feedback6-media\image1.png`
- `C:\repositories\CanDoItAll\canvas-feedback-bundle-6\inputs\feedback6-media\image2.png`
- `C:\repositories\CanDoItAll\canvas-feedback-bundle-6\inputs\feedback6-media\image3.png`

## Deliverables

- nested submenu opening delay with visible loading-circle progress
- nested layer placement that respects the toolbar safe zone and visible host bounds
- hive-style staggered submenu offsets proven in browser screenshots

## Implementation Steps

1. Add a submenu hover scheduler that tracks pending child-layer opens and cancels cleanly on pointer leave or layer closure.
2. Render a visible loading-ring state on the hovered parent action until the child layer opens.
3. Replace or extend compact-ring offsets with a hive-style staggered layout suitable for the submenu sizes from subbundle 01.
4. Clamp nested submenu origins against a toolbar-safe host region instead of only the raw host rectangle.
5. Extend browser proof for delay timing, toolbar clearance, and final screenshot composition.

## Scope Exceptions

- none

## Do Not Do

- do not copy the game screenshot’s exact art style, colors, or outlines
- do not fake the loading delay with a static icon and immediate submenu open
- do not accept a layout that passes only because the submenu is moved off-screen or clipped

## Acceptance Checklist

- hovering a parent action shows a loading-circle state before the submenu opens
- leaving early cancels the open and removes the indicator
- second-layer submenu items stay visible below the toolbar
- submenu staggering visibly reads as a hive composition in the screenshots

## Proof Required

- targeted browser pass with timing and geometry assertions
- screenshot artifacts for the delayed-open state and the final nested submenu composition
- execution report updated with exact commands, screenshot paths, and note-by-note closure

## Suggested Agent Prompt

```text
Implement subbundle 02 only.

Rework nested submenu behavior in the shared canvas menu so child layers open after an observable ~500ms loading-circle delay, stay below the toolbar, and use a hive-style staggered hex composition. Prove this in the browser with screenshots and explicit geometry checks.
```
