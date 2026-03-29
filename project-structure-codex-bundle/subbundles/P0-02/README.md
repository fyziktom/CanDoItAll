# P0-02 Commit-Only Floating-Window Persistence

## Status
- Lifecycle status: `Ready`

## Objective
- Keep floating-window drag and resize local in JS and persist only committed geometry.

## Covered Inputs
- Audit hotspot about excessive window-geometry persistence chatter.
- Feature preservation items `F02`, `F06`, `F30`, and `F33`.

## Prerequisites
- `P0-01` completed with trusted shared-canvas browser proof.

## Exact Source References
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvas-floating-window.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\CanvasFloatingWindow.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanvasFloatingWindowTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\PromptLibraryVerificationTests.cs`

## Deliverables
- Separation between live geometry updates and committed geometry publication.
- One persisted geometry write per completed drag, resize, restore, normalize, or hide action.
- Shared-window behavior preserved for ProjectStructure and PromptFactory.

## Dependency Impact
- Depends on overlay ownership because drag handles and window chrome need reliable input isolation first.
- Shared-canvas foundation for later persistence and browser-gate work.

## Validation Depth
- Component tests for floating-window states and accessibility labels.
- Browser proof for drag, minimize, restore, and persisted placement.
- Shared PromptFactory regression rerun.

## Implementation Steps
- Inspect the JS bridge for geometry publication frequency.
- Reduce persistence calls to commit points only.
- Verify page-level callers do not reintroduce repeated save churn.

## Do Not Do
- Do not widen into canvas viewport persistence owned by `P0-03`.
- Do not accept a drag-only visual fix without persistence-path proof.

## Acceptance Checklist
- Zero `SaveViewStateAsync` calls while actively dragging or resizing a floating window.
- Exactly one persisted state update after drag or resize commit.
- PromptFactory floating toolbox still drags and restores correctly.

## Proof Required
- Targeted `CanvasFloatingWindow` tests.
- Playwright drag and restore scenario on ProjectStructure.
- PromptFactory Playwright rerun.
- Screenshot of expanded and minimized window states after the change.

## Browser Validation Logging
- Route: ProjectStructure workbench and `/prompt-factory`.
- Viewport: large-screen for drag geometry review.
- Log screenshot paths and persistence observations in `reviews/01-execution-report.md`.

## Progression Gate
- Do not start downstream persistence work until browser proof and persistence evidence both show commit-only behavior.

## Suggested Agent Prompt
- Confirm current floating-window persistence churn, then implement commit-only publication in the shared window bridge without regressing PromptFactory or ProjectStructure window behavior.
