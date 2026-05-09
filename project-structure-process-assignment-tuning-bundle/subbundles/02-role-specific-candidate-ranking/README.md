# 02 - Role-Specific Candidate Ranking

## Status

- Status: `Completed`
- Closed: `Yes`
- Proof: `dotnet test`, `browser-02-role-candidates.png`, `browser-02b-role-plus-card.png`, `browser-05-agent-picker.png`, `browser-proof.json`

## Objective

Selecting a specific process role must show that role's assignment workflow: main candidate first, remaining candidates by score, and a final plus-card for the all-agent directory picker.

## Covered Inputs

- IN-003
- IN-004

## Prerequisites

- Subbundle 01 closure gate passes.
- `All` summary mode remains the default.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureProcessAssignmentDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureProcessAssignmentDialog.razor.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureProcessAssignmentDialogTests.cs`

## Deliverables

- Role-specific workspace panel.
- Candidate ordering helper.
- Select candidate buttons for non-selected candidates.
- Final plus-card with large icon that invokes the existing manual assignment callback.

## Dependency Impact

- Provides the candidate card surface that subbundle 03 will enhance with metadata badges.

## Validation Depth

- Component test with one selected candidate and lower-score alternatives.
- Component test or assertion for plus-card callback.

## Implementation Steps

1. Add active role resolver distinct from summary mode.
2. Render role-specific panel when `selectedRoleId` has a value.
3. Order candidates by selected/main first, then score descending.
4. Render a plus-card after all candidates.
5. Update tests for ordering and callback.

## Scope Exceptions

- Agent metadata badges are owned by subbundle 03.
- Real browser screenshots are owned by subbundle 04.

## Do Not Do

- Do not drop candidates returned by the launch plan.
- Do not filter candidates down to only recommended items.
- Do not replace `AgentSwitchDialog`.

## Acceptance Checklist

- Clicking a role leaves summary mode.
- The selected candidate is first.
- Non-selected candidates are sorted by score descending.
- The plus-card is last and opens the manual picker callback.

## Proof Required

- Targeted component test output.
- Browser role-drilldown and picker screenshots in subbundle 04.

## Browser Validation Logging

- Record selected role, candidate count, first card name/score, plus-card presence, picker screenshot, and pass/fail.

## Progression Gate

- Pass only if role mode is clearly separate from `All` summary mode and candidate ordering is deterministic.

## Suggested Agent Prompt

Implement subbundle 02 only. Build the role drilldown view and plus-card picker path on top of the already-closed `All` mode.
