# PRM-F01 — Process module foundation and shell integration

## Objective

Introduce a new canonical CanDoItAll module for process management, register it in composition, expose shell and project routes, and keep the module isolated from Workbench canonical state.

## Priority and wave

- Priority: **Critical**
- Planned wave: **Wave 1**
- Depends on: **None**

## Why this feature exists

This feature is part of the first process-management bundle because the user explicitly wants process definitions, actor responsibility, handoffs, and interactive modeling to land **before** the intelligence lake and before deep runtime coupling to the AgentFramework overlay.

## In scope

- A top-level /processes route exists and renders without breaking current shell navigation.
- A project-scoped route exists for process work and can be opened from project UX.
- The module is registered through the same composition pattern as existing CanDoItAll modules.
- SQLite and PostgreSQL migrations compile with the new module present.

## Non-goals

- Do not add process semantics into Workbench nodes or metadata.
- Do not introduce a separate process database in the first wave.
- Do not create AgentFramework runtime dependencies.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj (new)`
- `src/CanDoItAll.Modules.Processes/ProcessesModuleServiceCollectionExtensions.cs (new)`
- `src/CanDoItAll.Web/Program.cs`
- `src/CanDoItAll.Web/Composition/ShellNavigation.cs`
- `src/CanDoItAll.Composition/CanDoItAll.Composition.csproj`
- `src/CanDoItAll.Composition/ModuleAssemblies.cs`
- `src/CanDoItAll.Migrations.Sqlite/*`
- `src/CanDoItAll.Migrations.PostgreSql/*`
- `tests/CanDoItAll.Tests.*`
