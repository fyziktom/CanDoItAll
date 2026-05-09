# 02 - Manual Agent Picker Reuse

## Status

- Status: `Completed`
- Closed: `2026-05-09`
- Proof: `reviews/browser-03-agent-picker.png`, `reviews/browser-agent-picker-proof.json`, and the existing `AgentChatModalTests` filter/favorite coverage.

## Objective

Connect assignment actions in the full-screen modal to the existing chat agent switcher so manual agent selection includes search, tag filtering, favorite toggles, and favorites-first behavior.

## Covered Inputs

- IN-004

## Prerequisites

- Subbundle 01 completed and stable.
- `AgentSwitchDialog` and `AgentSelectionCard` tests still pass.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.Processes.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureCanvasDialogs.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\AgentSwitchDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\AgentSelectionCard.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Launch\ProcessesService.Launch.Planning.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\AgentChatModalTests.cs`

## Deliverables

- `Assign agent` and `Change agent` actions open `AgentSwitchDialog`.
- The picker uses the current workspace agent catalog and favorite toggle callback.
- Selecting an agent updates the launch-plan role selection.
- If the selected agent is not already a candidate, a safe persisted manual candidate is added and selected.

## Dependency Impact

- Critical foundation for final closure. Without this subbundle, the user's manual-specific-agent requirement is unresolved.

## Validation Depth

- Component test for visible assignment actions/test ids.
- Existing agent switcher tests for filtering/favorites.
- Service/integration test if manual candidate creation is introduced.

## Implementation Steps

1. Add callback parameters from `ProjectStructureCanvasDialogs` to `ProjectStructurePage.Processes.cs` for manual role assignment.
2. Open `AgentSwitchDialog` with `AgentSwitchDialog.Agents`, `SelectedAgentId`, and `FavoriteToggled`.
3. Toggle favorites through `IAgentFrameworkWorkspaceService.GetAgentEditorAsync` and `SaveAgentAsync`.
4. Select existing candidate by technical agent id where possible.
5. Add backend candidate creation/selection only if needed and test it.

## Scope Exceptions

- Creating a new technical agent is not required. The picker chooses existing agents.

## Do Not Do

- Do not fork or duplicate `AgentSwitchDialog`.
- Do not remove favorite filtering behavior from the existing chat switcher.
- Do not bypass launch-plan candidate validation.

## Acceptance Checklist

- Clicking `Assign agent` opens the switcher modal.
- Search, tag filters, favorite toggles, and favorites-first behavior are available through the reused component.
- Selecting an agent updates the process role and reloads the staffing modal.
- Required role gaps update correctly after selection.

## Proof Required

- Targeted tests.
- Browser proof of the picker open from the process assignment modal.

## Browser Validation Logging

- Record assignment action, switcher modal route/window, viewport, DOM assertions for filters/favorite controls, screenshot path, and pass/fail.

## Progression Gate

- Pass only if a specific agent can be manually selected and persisted for a process role.

## Suggested Agent Prompt

Implement subbundle 02 only. Reuse `AgentSwitchDialog` directly and avoid duplicating agent filtering UI. Update tests and execution report before closing.
