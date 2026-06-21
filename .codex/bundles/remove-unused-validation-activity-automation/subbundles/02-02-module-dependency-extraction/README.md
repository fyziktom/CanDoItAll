# 02-module-dependency-extraction

## Status

- `Completed`

## Objective

- Remove SchedulerPlanner's compile-time dependency on `CanDoItAll.Modules.Automation` while keeping the scheduler page and plan persistence usable.

## Success Criteria

- SchedulerPlanner project has no project reference to the old Automation module.
- SchedulerPlanner source has no `using CanDoItAll.Modules.Automation`.
- Enabled scheduler plans can still be represented with strongly typed scheduler-owned concepts.

## Covered Inputs

- Raw note that calendar-scheduler with workflows and processes covers automation tasks.
- R005.

## Prerequisites

- SB01 reference inventory is available.
- SchedulerPlanner current Automation dependencies have been inspected.

## Exact Source References

- `repo://src/CanDoItAll.Modules.SchedulerPlanner/CanDoItAll.Modules.SchedulerPlanner.csproj`
- `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerModels.cs`
- `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs`
- `repo://src/CanDoItAll.Modules.SchedulerPlanner/Pages/SchedulerPlannerPage.razor`

## Deliverables

- SchedulerPlanner-owned misfire/schedule dispatch model.
- Removed Automation project reference from SchedulerPlanner.
- Updated SchedulerPlanner tests or build proof.

## Dependency Impact

- SB03 cannot delete the Automation module safely until this subbundle removes SchedulerPlanner's dependency.
- SB04 must verify the scheduler route still renders after restart.

## Validation Depth

- Critical behavior extraction: source audit plus build/test proof that scheduler code no longer imports the removed Automation module.

## Implementation Steps

1. Replace Automation enums/contracts in SchedulerPlanner models with scheduler-owned types.
2. Replace Automation trigger registration/handler code with SchedulerPlanner-owned scheduling or dispatch behavior.
3. Update SchedulerPlanner UI text and tests for the new boundary.
4. Run a direct source audit for `CanDoItAll.Modules.Automation` under SchedulerPlanner.

## Scope Exceptions

- This subbundle does not delete the Automation module project; SB03 owns deletion.

## Do Not Do

- Do not remove SchedulerPlanner.
- Do not introduce silent fallback dispatch that hides scheduler errors.

## Acceptance Checklist

- SchedulerPlanner compiles without the old Automation project.
- Strongly typed SchedulerPlanner-owned scheduler concepts replace old Automation types.
- No direct Automation namespace import remains in SchedulerPlanner source.

## Proof Required

- `bundle://proof/SB02/transcripts/schedulerplanner-automation-audit.txt`
- Build or targeted test transcript proving SchedulerPlanner compiles.

## Browser Validation Logging

- Route: `/scheduler`.
- Viewport: desktop in SB04 after all removals.
- Evidence: Browser navigation and screenshot recorded by SB04.

## Progression Gate

- SB03 may delete Automation only after SchedulerPlanner has no direct Automation namespace or project dependency.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Remove SchedulerPlanner's dependency on the old Automation module with the smallest strongly typed change, preserve scheduler rendering and plan persistence, capture source-audit and build/test proof, and stop if scheduler behavior cannot be preserved.
```
