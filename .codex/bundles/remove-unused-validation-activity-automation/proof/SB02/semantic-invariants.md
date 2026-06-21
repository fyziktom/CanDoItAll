# SB02 Semantic Invariant Contract

## Invariant ID

- Invariant ID: `RM-002`

## Source raw note

- Calendar scheduler with workflows and processes replaces the old Automation module for automation tasks.

## Expected behavior

- SchedulerPlanner compiles and schedules through scheduler-owned types without importing `CanDoItAll.Modules.Automation`.

## Disallowed shallow implementation

- Keeping a hidden project reference or wrapping removed Automation contracts behind a silent fallback.

## Failing-first test

- failing-first: N/A - process/non-production removal audit; no new production behavior fixture was introduced.

## Passing test

- `proof/SB02/transcripts/schedulerplanner-automation-audit.txt`
- `proof/SB04/transcripts/build-solution.txt`
- `proof/SB04/transcripts/test-components-targeted.txt`

## Changed source files

- `src/CanDoItAll.Modules.SchedulerPlanner/CanDoItAll.Modules.SchedulerPlanner.csproj`
- `src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerModels.cs`
- `src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs`
- `src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerScheduling.cs`
- `src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerModuleServiceCollectionExtensions.cs`
- `src/CanDoItAll.Modules.SchedulerPlanner/Pages/SchedulerPlannerPage.razor`

## Production assertions

- SchedulerPlanner has no old Automation namespace or project dependency.
- The scheduler route renders in Browser proof without Blazor error UI.

## Red-team negative case

- The audit fails if old Automation contracts or SchedulerPlanner Automation payload types reappear.

## Downstream dependency check

- SB03 could delete the Automation project only after this audit passed.
