# Structured Input

## Raw Notes

| Id | Exact wording | Literal signal |
| --- | --- | --- |
| `N001` | `add system for export all projects and import all projects` | Must support all project records, not a single project only. |
| `N002` | `must work as zip import/export` | Must create and restore a zip package for all projects. |
| `N003` | `also transfer between existing dbs just via UI` | Must expose a UI flow that transfers all projects from one existing database profile to another. |
| `N004` | `Same transfer can work for transfer of processes, agents, etc. similar as we have it now when creating new database.` | Must fit the existing database-transfer pattern used by the create-empty/new-database workflow and not create an unrelated transfer surface. |

## Normalized Intent

Add an all-projects import/export capability with two transport modes:

- database-to-database transfer through the existing `IDatabaseTransferHandler` infrastructure and existing UI transfer dialogs
- zip package export/import for all project records from the current profile into the current profile or a selected profile

## Hard Constraints

- Preserve the user's absolute wording `all projects`.
- Reuse existing database profile transfer UI behavior where possible.
- Include project board data, project hierarchy, workbench graph records, node bindings, node references, view state, lifecycle history, and projection layout overrides.
- Do not silently copy volatile agent leases or analytics as project content unless explicitly justified.
- Do not break existing process, agent, provider, or ProjectStructure MCP transfer handlers.

## Validation Expectations

- Unit or integration proof that all project tables are copied between database profiles.
- Package proof that a project zip can be exported and imported back into an empty target database.
- Browser proof that the UI exposes the new `Projects` transfer item and project zip controls.
