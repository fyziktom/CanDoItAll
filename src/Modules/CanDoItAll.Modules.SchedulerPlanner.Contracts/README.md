# CanDoItAll.Modules.SchedulerPlanner.Contracts

## Purpose

Pure Scheduler target, plan/run, input-schema, history-query and observed mutation-fact contracts. Existing namespaces and enum values are preserved. The production module forwards the moved public types. EF entities, launch authority, legacy editor and Canvas workspace DTO stay in the module.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework: `net10.0`
- Authoritative dependencies: [CanDoItAll.Modules.SchedulerPlanner.Contracts.csproj](CanDoItAll.Modules.SchedulerPlanner.Contracts.csproj)

## Validation

```powershell
dotnet build src/Modules/CanDoItAll.Modules.SchedulerPlanner.Contracts/CanDoItAll.Modules.SchedulerPlanner.Contracts.csproj --configuration SchedulerUiProof /m:1
```

See the [Scheduler boundary and proof record](../../../docs/architecture/scheduler-ui-boundary.md).
