# CanDoItAll.Modules.SchedulerPlanner

## Purpose

Product module for scheduling process definitions and workflow versions through scheduler-owned Quartz projection. It owns scheduler plans, run history, cron description, target launch orchestration, and the `/scheduler` Blazor page.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.SchedulerPlanner/CanDoItAll.Modules.SchedulerPlanner.csproj
```

## References

Project references:

- `../CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj`
- `../CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj`
- `../CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj`
- `../CanDoItAll.Components.CanvasLib/CanDoItAll.Components.CanvasLib.csproj`
- `../CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj`
- `../CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj`
- `../CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`
- `../CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj`

Framework references:

- None

Direct package references:

- `Microsoft.AspNetCore.Components.Web (10.0.5)`

## Architecture Notes

Scheduler Planner should coordinate existing process and workflow runtimes; it should not duplicate process launch logic or workflow execution semantics. Persistence is split between `SchedulerPlanner_Plans` and `SchedulerPlanner_Runs`, with PostgreSQL EF migrations owning runtime schema.

Scheduler trigger handling is explicit through `SchedulerPlannerRunDispatcher`. Keep dedupe keys and run-state transitions predictable so repeated scheduler fires do not launch duplicate work.

## Related Docs

- Repository overview: `README.md` at the repo root
- Process agent operator runbook: `docs/process-agent-operator-runbook.md`
