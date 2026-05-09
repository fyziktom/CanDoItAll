# 01 - Fullscreen Assignment Layout

## Status

- Status: `Completed`
- Closed: `2026-05-09`
- Proof: `reviews/browser-02-assignment-modal.png`, `reviews/browser-04-assignment-modal-narrow.png`, targeted component tests, and `dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore`.

## Objective

Replace the current stacked process staffing dialog with the full-screen assignment modal shell and card layout shown in the supplied design.

## Covered Inputs

- IN-001
- IN-002
- IN-003

## Prerequisites

- Prepared bundle validation passes.
- `ProjectStructureCanvasDialogs` still owns rendering of `ProcessStartDialog`.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureCanvasDialogs.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureCanvasDialogs.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureOverlayDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.OverlayStates.cs`

## Deliverables

- Full-screen process assignment modal for staffing stage.
- Header with title, copy, progress, and design-matching actions.
- Left role rail with search input, filter affordance, role rows, recommended/assigned states, and HR help action.
- Main role-card grid with resolved and unresolved states.
- Bottom selected-agent detail panel.

## Dependency Impact

- Critical foundation for subbundle 02 and 03. Manual agent picker actions and browser proof depend on stable layout and test ids from this subbundle.

## Validation Depth

- Component or build validation for the modal markup.
- Large-screen browser screenshot before moving to subbundle 02 when feasible.

## Implementation Steps

1. Add any needed staffing-stage UI state helpers to `ProjectStructureCanvasDialogs`.
2. Add full-screen mode support to `ProjectStructureOverlayDialog` if CSS-only override is insufficient.
3. Replace staffing-stage body with assignment shell, role rail, role cards, and selected-agent detail panel.
4. Add scoped CSS for responsive layout and visual states.
5. Add or update component tests for expected test ids and labels.

## Scope Exceptions

- Manual agent picker behavior is owned by subbundle 02.

## Do Not Do

- Do not change process runtime execution.
- Do not remove existing required-role gate behavior.
- Do not redesign non-staffing process start confirmation.

## Acceptance Checklist

- Full-screen modal is used for staffing stage.
- Header actions show `Cancel`, `Save and close`, and `Review and start`.
- Role count and assigned progress are visible.
- Role rail and main cards render from actual `startDialog.Roles`.
- Empty role cards show `No agent assigned` and `Assign agent`.
- A selected/resolved role shows selected agent detail.

## Proof Required

- Test/build output.
- Screenshot or DOM proof that modal uses full-screen dimensions.

## Browser Validation Logging

- Record route, viewport, open modal action, DOM assertions, screenshot path, and pass/fail in `reviews/01-execution-report.md`.

## Progression Gate

- Pass only if the modal is structurally full-screen and browser/component proof shows no obvious overlap in the primary desktop layout.

## Suggested Agent Prompt

Implement subbundle 01 only. Keep manual picker behavior stubbed to existing candidate selection actions until subbundle 02. Update the execution report before closing.
