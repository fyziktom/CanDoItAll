# Project Structure Page Shell Components

## Status

- `Ready`

## Objective

- Split the ProjectStructure page shell and oversized ProjectStructure page-owned components into smaller typed components after node helpers are stable.

## Covered Inputs

- `N001`
- `N002`
- `R007`

## Prerequisites

- `01-project-structure-node-helpers` completed with passing tests.
- Components MCP retried, or local BaseLib/CanvasLib usage fallback recorded.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureCanvasDialogs.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureProcessAssignmentDialog.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`

## Deliverables

- Extract visible ProjectStructure shell regions with typed parameters and callbacks.
- Reduce page markup while preserving `CanvasWorkbench`, tool windows, support dialogs, and node detail behavior.
- Split oversized dialog components only where the split has a clear section boundary.

## Dependency Impact

- Later route cleanup and final browser proof depend on this phase for the most complex workbench route.

## Validation Depth

- Critical UI foundation.

## Implementation Steps

1. Re-read the ProjectStructure page after subbundle `01`.
2. Choose one ProjectStructure shell region at a time.
3. Extract typed components without moving page-owned orchestration state unnecessarily.
4. Preserve `EventCallback` flows and existing test ids.
5. Run targeted tests and browser proof.

## Scope Exceptions

- Do not redesign the ProjectStructure UI.
- Do not move service orchestration into child components unless the child already owns that behavior.

## Do Not Do

- Do not combine this with PromptFactory or CRM/HR cleanup.
- Do not change route parameters or project selection behavior.
- Do not add raw structural wrappers where existing shared components fit.

## Acceptance Checklist

- Page/component file sizes shrink in the workbook checklist.
- Canvas selection, node open, create action, context action, and dialog flows still work.
- Floating windows and dialogs render above the canvas without clipping.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter ProjectStructurePage`
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter ProjectStructurePartyPicker`
- Playwright proof on `/projects/{ProjectId:guid}/structure`.
- Large-screen and narrow screenshots for changed UI regions.

## Browser Validation Logging

- Route: `/projects/{ProjectId:guid}/structure`.
- Viewports: `1600x900` and `390x844` when layout changes.
- Required actions: navigate, select a node, open quick actions or relevant dialogs, toggle tool windows, capture screenshots.
- Review questions: content readable, no clipping, no lateral overflow, callbacks still trigger expected state.

## Progression Gate

- Browser proof and targeted tests pass before `09` or `10` can rely on ProjectStructure behavior.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Split ProjectStructure shell/components after confirming subbundle 01 proof, preserve canvas and dialog behavior, run the required tests and Playwright proof, update report rows, and stop on weak browser evidence.
```
