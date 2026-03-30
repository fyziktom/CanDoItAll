# 01 Foundation And Toolbox

## Status

- Status: `Completed`
- Legacy task coverage: `T00-T05`

## Objective

Stabilize overlay ownership, selection flows, inline note behavior, context actions, and toolbox interactions before the renderer migration.

## Covered Inputs

- `R04`
- `R06`

## Prerequisites

- Prepared execution bundle exists and the shared workbench consumers are available locally.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\CanvasWorkbench.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`

## Deliverables

- Stable ProjectStructure overlay and context flows.
- Stable toolbox and floating-window interaction behavior.

## Dependency Impact

- Unblocks renderer adoption because selection, overlay, and create-action flows stop fighting the stage runtime.

## Validation Depth

- Browser regression validation on `/projects/{id}/structure`.
- Artifact capture and overlay interaction scenarios.

## Implementation Steps

- Repair selection synchronization and multi-select behavior.
- Repair toolbox open, create, and context action flows.
- Prove the browser-visible states with screenshots and Playwright assertions.

## Do Not Do

- Do not widen scope into renderer migration before these interaction gates are green.

## Acceptance Checklist

- Overlay/browser regressions are green.
- Context actions and inline note flows are green.
- Toolbox/browser artifacts are current.

## Proof Required

- `AppSmokeTests.Structure_canvas_supports_inline_note_creation_editing_and_context_create_dialogs`
- `AppSmokeTests.Project_structure_feedback6_context_menu_is_validated_in_browser`
- `AppSmokeTests.Project_structure_feedback_7_is_validated_in_browser`

## Browser Validation Logging

- Route: `/projects/{id}/structure`
- Viewports: `1900x1200`, `1600x1100`
- Evidence: `output/playwright/structure-*.png`, `output/playwright/bundle-p0-02-*.png`, `artifacts/screenshots/i04`, `artifacts/screenshots/i08`, `artifacts/screenshots/i17`, `artifacts/screenshots/i19`, `artifacts/screenshots/i23`

## Progression Gate

- Passed because ProjectStructure interaction regressions were repaired before renderer adoption continued.

## Suggested Agent Prompt

Verify overlay ownership, toolbox behavior, inline-note flows, and ProjectStructure browser artifacts before allowing renderer changes to proceed.
