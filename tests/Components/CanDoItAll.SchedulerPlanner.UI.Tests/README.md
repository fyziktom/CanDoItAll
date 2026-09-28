# CanDoItAll.SchedulerPlanner.UI.Tests

## Purpose

Lightweight Scheduler renderer, presentation lifetime, input, receipt and calendar ownership
tests. The real sandbox owner port supplies bounded deterministic storage and controllable
waits. No PostgreSQL, Quartz scheduler, production application or live Agent starts here.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework: `net10.0`
- Authoritative dependencies: [CanDoItAll.SchedulerPlanner.UI.Tests.csproj](CanDoItAll.SchedulerPlanner.UI.Tests.csproj)

## Validation

Build-backed discovery and execution:

```powershell
dotnet test tests/Components/CanDoItAll.SchedulerPlanner.UI.Tests --configuration SchedulerUiProof --list-tests
dotnet test tests/Components/CanDoItAll.SchedulerPlanner.UI.Tests --configuration SchedulerUiProof --no-build
```

Registered in Components and Stable test solutions and each CI component selection.
Real PostgreSQL, production route, native Canvas and published asset proof remain separate;
see the [Scheduler boundary record](../../../docs/architecture/scheduler-ui-boundary.md).
