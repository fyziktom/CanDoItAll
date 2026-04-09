# Codex task — PRM-F10

Implement **Project, Workbench, and shell projections** inside the uploaded CanDoItAll solution.

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

- Project surfaces expose a clear entry point into processes.
- If Workbench projection is enabled, it shows references and summaries rather than acting as the canonical store.
- Process-related project object types and routes remain explicit and typed.
- Shared-project processes are navigated through project ownership rather than duplicated shadow copies.

## Recommended first files to touch

- `src/CanDoItAll.Modules.Projects/Pages/Components/ProjectModalHost.razor`
- `src/CanDoItAll.Modules.Projects/Pages/Components/ProjectsBoard.razor`
- `src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs`
- `src/CanDoItAll.Modules.Workbench/* (projection-only integration)`
- `tests/CanDoItAll.Tests.Components/ProjectProcessesNavigationTests.cs (new)`
