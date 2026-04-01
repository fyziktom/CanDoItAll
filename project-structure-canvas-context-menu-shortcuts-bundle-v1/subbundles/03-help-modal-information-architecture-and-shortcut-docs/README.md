# help-modal-information-architecture-and-shortcut-docs

## Status

- `Completed`

## Objective

- Turn the help modal into a browsable documentation surface that explains the new menu-shortcut model alongside the existing global canvas guidance.

## Covered Inputs

- `N001` Simplify menu orientation from the keyboard.
- `N008` Add a better-structured help modal with browsable docs pages.
- `N009` Ensure the docs reflect the rendered shortcut behavior.

## Prerequisites

- `02-runtime-keyboard-navigation-and-menu-affordances` complete with browser proof.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Workbench\CanvasWorkbench.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Graph\Interaction\KeyboardShortcutRouter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css\workbench\panels\03-help-settings-and-preview.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanvasWorkbenchTests.cs`

## Deliverables

- Help modal page navigation such as tabs, pills, or another lightweight page switcher.
- Dedicated menu-shortcuts help content that explains the right-click flow and key assignments.
- Preserved or improved documentation for existing global and selection shortcuts.
- Focused tests for help-modal structure and key documentation.

## Dependency Impact

- `04-browser-proof-and-closure` depends on the help surface being real before it can capture closure screenshots and raw-note closure.
- Weak proof here would leave discoverability incomplete even if runtime behavior works.

## Validation Depth

- `UI, component-test, and browser-proof`

## Implementation Steps

1. Replace the flat help layout with a browsable multi-page structure.
2. Add a menu-shortcuts page that explains top-layer and nested-layer usage.
3. Preserve the existing global shortcut guidance in a dedicated page.
4. Style the help surface so page navigation remains readable on large and narrower widths.
5. Add or update component tests for help-page structure and content.
6. Capture browser screenshots of the open help modal at both target viewports.

## Scope Exceptions

- Do not redesign unrelated settings or preview panels.
- Do not change runtime shortcut behavior in this phase unless documentation reveals a direct bug that must be repaired immediately.

## Do Not Do

- Do not ship a new help page that drifts from the actual runtime shortcut contract.
- Do not collapse all help content back into a single long scroll section.

## Acceptance Checklist

- Help modal has clear page-level navigation.
- One page explains the right-click menu keyboard model and representative mappings.
- One page preserves global or selection shortcut documentation.
- Component tests cover the browsable structure and key content.
- Browser screenshots confirm the open help layout remains readable at both target widths.

## Proof Required

- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter CanvasWorkbenchTests`
- Browser screenshots of the open help modal at `1600x1000` and `1280x800`
- Execution-report updates documenting page names, screenshots, and any responsive findings

## Browser Validation Logging

- Route: `/projects/{projectId}/structure`
- Viewports: `1600x1000` first, then `1280x800`
- Playwright actions: open help overlay, switch pages, verify menu-shortcuts content, verify global-shortcuts content
- Screenshot targets:
  - `evidence/help-modal-shortcuts-desktop.png`
  - `evidence/help-modal-shortcuts-narrow.png`
- Review questions:
  - Can a keyboard-first user understand how to open and advance nested menus?
  - Are the page navigation controls obvious and stable at both widths?
  - Does the help content still retain the existing global shortcut guidance?

## Progression Gate

- Closure may continue only after component proof passes and browser screenshots confirm the help surface is browsable and aligned with the shipped shortcut behavior.

## Suggested Agent Prompt

```text
Implement only subbundle 03 for the canvas context-menu shortcuts bundle.
Convert the help modal into a browsable documentation surface, add a dedicated menu-shortcuts page, preserve the existing global shortcut guidance, and prove the result with component tests plus browser screenshots at both required viewports.
Keep the documentation aligned with the actual runtime shortcut behavior from subbundle 02.
```
