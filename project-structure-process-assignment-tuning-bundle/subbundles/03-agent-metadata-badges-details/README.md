# 03 - Agent Metadata Badges And Details

## Status

- Status: `Completed`
- Closed: `Yes`
- Proof: `dotnet test`, `browser-03-badge-tooltip.png`, `browser-04-agent-details.png`, `browser-proof.json`

## Objective

Agent cards in summary and role-specific views must expose `model`, `tools`, `skills`, and `details` badges. Tooltip badges use the existing tooltip service, and `details` opens a readonly details dialog.

## Covered Inputs

- IN-005

## Prerequisites

- Subbundle 02 closure gate passes.
- Candidate rendering has reusable markup for summary and role modes.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.Processes.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.OverlayStates.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureProcessAssignmentDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureProcessAssignmentDialog.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\TooltipTarget.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureProcessAssignmentDialogTests.cs`

## Deliverables

- Candidate state metadata fields for provider/model/tools/skills/details.
- Parent mapping from Agent Framework catalog into candidate UI state.
- Badge rendering in summary cards and role candidate cards.
- Readonly details dialog for assignment-time agent information.
- Z-index rule for nested details dialog above the fullscreen overlay.

## Dependency Impact

- Changes launch-plan UI state mapping and component tests; browser proof depends on badge test ids and dialog layering.

## Validation Depth

- Component test for badges and details button.
- Build validation for catalog mapping and new component.

## Implementation Steps

1. Extend candidate UI state with optional metadata.
2. Refresh agent/provider metadata before mapping launch-plan roles.
3. Add badge helper markup using `TooltipTarget`.
4. Add readonly details dialog component.
5. Add CSS for badges and details layout.
6. Update tests.

## Scope Exceptions

- Editing agent settings is out of scope for the readonly details dialog.

## Do Not Do

- Do not make the details dialog editable.
- Do not block modal opening if provider metadata cannot load.
- Do not remove existing AgentSwitchDialog search/tag/favorite behavior.

## Acceptance Checklist

- Summary cards show `model`, `tools`, `skills`, and `details` badges.
- Role candidate cards show the same badges.
- Model tooltip includes provider/model information.
- Tools and skills tooltips list names or a clear empty state.
- Details dialog opens and is readonly.

## Proof Required

- Targeted component test output.
- Browser screenshots for tooltip and details dialog in subbundle 04.

## Browser Validation Logging

- Record badge test ids, tooltip text proof, details dialog screenshot, dialog z-index/layering result, and pass/fail.

## Progression Gate

- Pass only if metadata is real when catalog data is available and graceful when it is not.

## Suggested Agent Prompt

Implement subbundle 03 only. Enrich candidates from the Agent Framework catalog and add tooltip badges plus readonly details without changing process execution.
