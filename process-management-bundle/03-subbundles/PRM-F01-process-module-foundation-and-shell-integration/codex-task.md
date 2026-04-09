# Codex task — PRM-F01

Implement **Process module foundation and shell integration** inside the uploaded CanDoItAll solution.

## Constraints

- Treat `CanDoItAll.Modules.Processes` as the canonical owner for process-management behavior.
- Do not create a new durable agent registry; use CRM-HR bindings when actors are involved.
- Do not add direct compile-time dependency on the uploaded AgentFramework repo in the first process-management implementation.
- Keep all code comments in English.
- Preserve buildability for the current solution layout.

## Required outputs

- Code changes for this feature
- Matching tests
- Migration updates if persistence changes
- A short implementation note describing what changed and how it was verified

## Done definition

This task is done when:

- A top-level /processes route exists and renders without breaking current shell navigation.
- A project-scoped route exists for process work and can be opened from project UX.
- The module is registered through the same composition pattern as existing CanDoItAll modules.
- SQLite and PostgreSQL migrations compile with the new module present.

## Recommended first files to touch

- `src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj (new)`
- `src/CanDoItAll.Modules.Processes/ProcessesModuleServiceCollectionExtensions.cs (new)`
- `src/CanDoItAll.Web/Program.cs`
- `src/CanDoItAll.Web/Composition/ShellNavigation.cs`
- `src/CanDoItAll.Composition/CanDoItAll.Composition.csproj`
- `src/CanDoItAll.Composition/ModuleAssemblies.cs`
- `src/CanDoItAll.Migrations.Sqlite/*`
- `src/CanDoItAll.Migrations.PostgreSql/*`
- `tests/CanDoItAll.Tests.*`
