# 23 Process Canvas Selection Inspector And Edit-Dialog Parity

## Status

- `Completed`

## Objective

- Bring the process canvas to project-structure workbench parity for selection, inspection, and edit actions by adding floating selection detail windows, single-click sync, and double-click edit/action flows.

## Covered Inputs

- `REQ-007`
- `REQ-014`
- `REQ-015`
- `REQ-016`
- `REQ-022`
- Canvas parity audit `CAV-06` through `CAV-10`
- User request for selection detail floating window and double-click edit behavior

## Prerequisites

- `22-process-canvas-context-menu-and-template-aware-create-flows`

## Exact Source References

- `C:\repositories\CanDoItAll\process-management-bundle\01-workbooks\02-process-modeling-canvas-and-runtime.xlsx`
- `C:\repositories\CanDoItAll\process-management-bundle\reviews\02-implementation-coverage-audit.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasSurfaceFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.SelectionPanel.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure`

## Deliverables

- A floating selection detail window for the process canvas.
- Single-click selection synchronization between canvas nodes and the selection detail window.
- Double-click edit/action flows for process-definition nodes and runtime nodes.
- Compact inspector content that keeps definition/runtime details inside canvas rhythm instead of scattering them across unrelated panels.
- Playwright-proved parity with the interaction standard already used by project structure.

## Dependency Impact

- This is the user-facing proof that the process canvas is no longer a preview surface.
- If this subbundle is weak, future runtime overlays and management UX will still feel inconsistent with the rest of CanDoItAll.

## Validation Depth

- `Critical UI parity`

## Implementation Steps

1. Add a floating selection inspector window for the process canvas using shared canvas window patterns.
2. Mirror the currently selected canvas node into the selection inspector on single click.
3. Add double-click or open-node flows that surface edit actions and related operations in a compact modal/dialog rhythm.
4. Reuse the extracted process forms and authoring actions instead of creating new one-off editors.
5. Validate both definition and runtime selection behavior, including large-screen density and overlay stability.

## Scope Exceptions

- Deeper management dashboards or non-canvas executive surfaces remain separate work and should not be hidden inside this parity subbundle.

## Do Not Do

- Do not leave selection details split between canvas and unrelated page sections once the floating inspector exists.
- Do not implement edit actions through duplicated modal markup that bypasses the extracted process forms.
- Do not claim parity if only definition mode works but runtime mode still lacks coherent selection behavior.

## Acceptance Checklist

- Selecting a node updates the floating inspector content immediately.
- Double-clicking a node opens edit or related actions using the new reusable editor surfaces.
- Definition and runtime modes both have coherent selection-detail behavior.
- The floating inspector is compact, readable, and stable on large screens.
- Browser proof shows no overlay clipping, overflow, or collision regressions.

## Proof Required

- Playwright walkthrough for node selection and double-click flows in both definition and runtime modes.
- Large-screen screenshots for floating inspector and edit/action dialogs.
- Regression proof that canvas selection and external list/detail state stay synchronized.

## Browser Validation Logging

- Route:
  `/processes`
- Route:
  `/projects/{id}/processes`
- Viewport:
  `1920x1080`
- Viewport:
  `1600x900`
- Evidence:
  screenshots and Playwright steps covering selection sync, edit dialogs, and runtime inspector parity

## Progression Gate

- Phase 06 may close only after both definition and runtime canvas parity flows are browser-validated and the generated repair bundle for phase 06 is ready.

## Suggested Agent Prompt

```text
Implement only the process-canvas selection and edit-parity slice. Add a floating selection inspector, keep it synchronized with canvas node selection, open compact edit/action flows on double click, reuse the extracted process editor forms, and close only after Playwright proves both definition and runtime parity on large screens.
```

