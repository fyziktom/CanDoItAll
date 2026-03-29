# P2-01 Scene Patch Protocol And Plain-JS Modularization

## Status
- Lifecycle status: `Ready`

## Objective
- Make the JS layer maintainable without TypeScript or a new bundler while preserving the public workbench API.

## Covered Inputs
- Audit recommendation to modularize the giant JS interop file after retained-renderer work is stable.
- Feature preservation items `F33` and `F34`.

## Prerequisites
- `P1-01` completed with trusted retained-renderer proof.
- `P1-02` completed with trusted culling proof.
- `P1-03` completed with trusted drag-loop proof.

## Exact Source References
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvas-floating-window.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\CanvasWorkbench.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`

## Deliverables
- Clear internal ownership boundaries inside the JS layer.
- Stable public `window.CanDoItAll.canvasWorkbench` API.
- Easier-to-audit patch, viewport, diagnostics, and overlay logic.

## Dependency Impact
- Critical foundation for optional consolidation and true-canvas spike work.
- Shared-canvas change, so ProjectStructure and PromptFactory proof must both remain trustworthy.

## Validation Depth
- Build and runtime validation for public API stability.
- Browser proof for shared-canvas consumers after refactor.
- Code review standard focused on maintainability and explicit ownership.

## Implementation Steps
- Identify internal seams in the current JS file.
- Extract or reorganize only the modules needed to clarify ownership.
- Keep entry points stable or migrate them with tests in the same task.

## Do Not Do
- Do not introduce TypeScript or a new build pipeline.
- Do not mix in new renderer behavior unrelated to modularization.

## Acceptance Checklist
- Public API stays stable or is migrated with tests in the same task.
- Hot-path JS is easier to reason about and code ownership is explicit.

## Proof Required
- Targeted shared-canvas browser runs.
- Screenshot and smoke evidence for ProjectStructure and PromptFactory.
- Focused code review of public API stability.

## Browser Validation Logging
- Route: ProjectStructure structure route and `/prompt-factory`.
- Viewport: large-screen first.
- Record shared-surface actions, screenshots, and result in `reviews/01-execution-report.md`.

## Progression Gate
- Do not start `P3-01` or `P3-02` until the modularized JS surface is stable and shared-canvas proof remains green.

## Suggested Agent Prompt
- Reorganize the hot-path JS into clearer internal ownership boundaries while keeping the public workbench API stable and proving that ProjectStructure and PromptFactory still work.
