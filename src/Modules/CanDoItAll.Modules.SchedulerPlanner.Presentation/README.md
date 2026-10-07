# CanDoItAll.Modules.SchedulerPlanner.Presentation

## Purpose

Shared production/scenario presentation policy. SchedulerWorkspace owns view and draft lifetimes and independent reads; SchedulerInputSession owns schema/JSON/options; SchedulerMutations owns immutable submission, target admission and fact reconciliation. Owner ports are in-process and contain no service locator.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework: `net10.0`
- Authoritative dependencies: [CanDoItAll.Modules.SchedulerPlanner.Presentation.csproj](CanDoItAll.Modules.SchedulerPlanner.Presentation.csproj)

## Validation

```powershell
dotnet build src/Modules/CanDoItAll.Modules.SchedulerPlanner.Presentation/CanDoItAll.Modules.SchedulerPlanner.Presentation.csproj --configuration SchedulerUiProof /m:1
```

See the [Scheduler boundary and proof record](../../../docs/architecture/scheduler-ui-boundary.md).
