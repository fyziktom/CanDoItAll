# 05 Recomposition Menu And Layout Modes

## Status

- `Completed`

## Objective

- Repair the recomposition toolbar menu so it behaves like a detached popup, then add selectable graph-layout modes that make the main path, branch paths, and feedback paths easier to compare on complex process canvases.

## Covered Inputs

- `N011`
- `N012`
- `N013`
- `REQ-009`
- `REQ-010`
- `REQ-011`

## Prerequisites

- `01-layout-analysis-and-contract` completed.
- `02-definition-recomposition-tuning` completed.
- `03-validation-and-browser-proof` completed.
- `04-role-instance-composition-and-default-template-repair` completed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\ProcessCanvasRecompositionService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessCanvasToolbarActions.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.Recomposition.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.StepsPresenter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceStepsTab.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessCanvasRecompositionServiceTests.cs`
- `C:\repositories\CanDoItAll\Templates\Processes\manifest.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\software-delivery\definition.json`

## Deliverables

- Detached recomposition popup menu with click-toggle behavior.
- Selectable `Balanced flow`, `Main spine`, `Branch fan-out`, and `Feedback lanes` layout actions.
- Recomposition profiles with different step spacing, branch-lane spacing, feedback-lane handling, and collision spacing.
- Default process template coordinates regenerated from the current feedback-lane layout profile.
- Targeted tests plus browser screenshots and crossing-count comparison.

## Dependency Impact

- Process semantics, runtime execution, persistence shape, and manual canvas movement remain unchanged.
- The new modes are UI/application-service entry points over the existing recomposition service, not a new graph engine.

## Validation Depth

- Critical UI follow-up.

## Implementation Steps

1. Replace toolbar-stretching recomposition menu rendering with a detached popup panel.
2. Use click-toggle behavior so the detached menu remains usable across the gap below the toolbar.
3. Add mode-specific recomposition profiles for main-spine, branch fan-out, and feedback-lane layouts.
4. Wire the new modes through the workspace component and steps tab callbacks.
5. Refresh default process template coordinates from the current recomposition service.
6. Add targeted tests proving the modes produce different, intentional geometry.
7. Capture browser screenshots and comparative crossing-count analytics.

## Do Not Do

- Do not change process runtime semantics or persisted process contracts.
- Do not introduce a new graph rendering or layout library.
- Do not hide links to make the crossing count look better.
- Do not treat approximate browser crossing counts as exact CanvasLib router math.

## Acceptance Checklist

- Recomposition menu opens below the toolbar without increasing toolbar height or clipping.
- At least three new recomposition algorithms are available from the menu.
- A complex default process can be recomposed by each mode from the live UI.
- Browser proof captures screenshots and crossing-count analytics for the modes.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter ProcessCanvasRecompositionServiceTests`
- `dotnet build src\CanDoItAll.Modules.Processes\CanDoItAll.Modules.Processes.csproj`
- Browser proof on `/processes` at `1920x1080`, maximized canvas, menu open-state screenshot, and mode screenshots.

## Browser Validation Logging

- Add a `05-recomposition-menu-and-layout-modes` row to `reviews/01-execution-report.md` with route, viewport, popup metrics, mode screenshots, crossing-count metrics, and result.

## Progression Gate

- Passed. The menu was repaired, layout modes were implemented and wired through the UI, default process template coordinates were refreshed, targeted tests/module build passed, and browser proof captured mode screenshots plus comparative crossing metrics.

## Completion Proof

- Targeted recomposition tests passed with `8` tests.
- Module build passed with `0` warnings and `0` errors.
- Browser route: `http://127.0.0.1:5094/processes`.
- Browser viewport: `1920x1080`.
- Popup proof: `C:\repositories\CanDoItAll\process-canvas-recompose-menu-proof.png`; measured popup top gap was `6.60px` below the toolbar and body horizontal overflow was `false`.
- Mode screenshots:
  - `C:\repositories\CanDoItAll\process-canvas-balanced-flow-proof.png`
  - `C:\repositories\CanDoItAll\process-canvas-main-spine-proof.png`
  - `C:\repositories\CanDoItAll\process-canvas-branch-fanout-proof.png`
  - `C:\repositories\CanDoItAll\process-canvas-feedback-lanes-proof.png`
- Browser crossing analytics on `Multi-team software delivery and release governance`:
  - `Balanced flow`: `130` approximate flow crossings, `971` approximate all-link crossings.
  - `Main spine`: `88` approximate flow crossings, `570` approximate all-link crossings.
  - `Branch fan-out`: `91` approximate flow crossings, `566` approximate all-link crossings.
  - `Feedback lanes`: `104` approximate flow crossings, `613` approximate all-link crossings, with the first-pass path concentrated on the main lane and repair/escalation paths below it.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Fix the recomposition popup menu so it is a detached menu, expose multiple layout algorithms from it, and prove the modes in a large browser viewport with screenshots and crossing-count analytics.
```
