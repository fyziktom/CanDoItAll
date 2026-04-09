# 04 Process Module Shell And Storage Foundation

## Status

- `Completed`

## Objective

- Create the `CanDoItAll.Modules.Processes` shell, module registration, storage baseline, and migration pattern so later authoring and runtime work has a canonical home.

## Covered Inputs

- `REQ-001`
- `REQ-005`
- Raw note `N01`
- Legacy features `PRM-F01` and `PRM-F15`

## Prerequisites

- `03-post-implementation-bundle-phase00-generation`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Composition\ShellNavigation.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\CanDoItAll.Composition.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\ModuleAssemblies.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Persistence\AppDbContext.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.Sqlite`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.PostgreSql`
- `C:\repositories\CanDoItAll\process-management-bundle\03-subbundles\PRM-F01-process-module-foundation-and-shell-integration\README.md`

## Deliverables

- New process-module project shell registered through existing composition patterns.
- Workspace-level and project-level navigation entry points for processes.
- Shared database integration and migration baseline for SQLite and PostgreSQL.
- Storage and retention guardrails that keep the module on the main app database first.

## Dependency Impact

- Every later process entity and route depends on this shell and migration baseline.
- Weak proof here invalidates all later persistence, navigation, and module-registration work.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Create the module shell and register it in composition and web startup.
2. Add initial navigation and empty route surfaces.
3. Add the first persistence and migration seams using the shared database pattern.
4. Confirm storage naming and retention guardrails for later runtime and journal tables.

## Scope Exceptions

- Do not attempt the full process domain in this subbundle.

## Do Not Do

- Do not introduce a separate process database.
- Do not place canonical process truth in Workbench metadata.
- Do not add AgentFramework project references.

## Acceptance Checklist

- A process module project exists and is wired into the solution.
- Routes and navigation compile without breaking current shell behavior.
- SQLite and PostgreSQL migration projects are ready for later process tables.
- Storage direction remains compatible with future journal and evidence growth.

## Proof Required

- `dotnet build` for the solution or targeted projects.
- Migration build proof for both database providers.
- Route smoke or component proof for the new shell entries.

## Browser Validation Logging

- Route:
  `/processes`
- Route:
  `/projects/{id}/processes`
- Viewport:
  `1920x1080`
- Evidence:
  basic navigation smoke even if the pages are still skeletal

## Progression Gate

- Later authoring work may start only after the module shell, composition wiring, and migration baseline are stable and verified.

## Suggested Agent Prompt

```text
Implement only the process-module shell and storage foundation. Add the new module through existing CanDoItAll composition patterns, keep storage on the shared AppDbContext path, and do not add any AgentFramework dependency.
```
