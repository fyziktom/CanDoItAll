# 01 - Full-Width All Summary

## Status

- Status: `Completed`
- Closed: `Yes`
- Proof: `dotnet test`, `browser-01-summary-all.png`, `browser-proof.json`

## Objective

Make the fullscreen assignment dialog use the available width and add the first rail item `All` for summary-review mode.

## Covered Inputs

- IN-001
- IN-002

## Prerequisites

- Prepared bundle validation passes.
- Current staffing stage still renders `ProjectStructureProcessAssignmentDialog`.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureProcessAssignmentDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureProcessAssignmentDialog.razor.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureProcessAssignmentDialogTests.cs`

## Deliverables

- Full-width content rules for the assignment modal.
- First left-rail item named `All`.
- `All` mode selected by default.
- Summary overview remains the all-role card grid.

## Dependency Impact

- Critical foundation for role-specific mode, metadata badges, and browser screenshots.

## Validation Depth

- Component test for `All` rail and summary grid.
- Browser proof deferred to subbundle 04.

## Implementation Steps

1. Treat `selectedRoleId == null` as `All` mode.
2. Add the `All` role-list item before filtered roles.
3. Keep summary card rendering inside `All` mode.
4. Remove or loosen width limits that cause unused modal space.
5. Update component tests.

## Scope Exceptions

- Role-specific candidate ranking is owned by subbundle 02.
- Metadata badges are owned by subbundle 03.

## Do Not Do

- Do not change process runtime execution.
- Do not change manual picker behavior in this subbundle.
- Do not remove required-role gating.

## Acceptance Checklist

- `All` is the first rail row.
- `All` is active when staffing opens.
- Summary mode renders all roles.
- Modal content is not artificially capped by the previous copy width.

## Proof Required

- Targeted component test output.
- Later browser screenshot and DOM proof in subbundle 04.

## Browser Validation Logging

- Record route, viewport, summary mode screenshot, modal/body widths, horizontal overflow result, and pass/fail in `reviews/01-execution-report.md`.

## Progression Gate

- Pass only if `All` mode is stable and the summary grid still renders all roles.

## Suggested Agent Prompt

Implement subbundle 01 only. Add the `All` mode and full-width tuning while preserving the existing assignment grid and launch-plan state.
