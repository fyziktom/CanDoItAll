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

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Modules.SchedulerPlanner.csproj](CanDoItAll.Modules.SchedulerPlanner.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

Scheduler Planner should coordinate existing process and workflow runtimes; it should not duplicate process launch logic or workflow execution semantics. `SchedulerPlannerDbContext` contains only plans and run history. Its pooled factory is bound to the immutable canonical database profile. It reuses the complete model mappings for `SchedulerPlanner_Plans` and `SchedulerPlanner_Runs`, including legacy Automation column names, run deduplication, and the plan/run cascade. The complete `AppDbContext` remains the PostgreSQL migration authority and the explicit profile-transfer maintenance model.

Scheduler trigger handling is explicit through `SchedulerPlannerRunDispatcher`. The owner context preserves current firing keys, terminal replay behavior, and target launch orchestration. Durable launch authority and recovery across an interrupted dispatch remain a separate required boundary; the context cutover does not change that protocol.

## Related Docs

- Repository overview: `README.md` at the repo root
- Process agent operator runbook: `docs/process-agent-operator-runbook.md`
