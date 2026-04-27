# Normalized Requirements

| Id | Requirement | Source notes | Acceptance signal |
| --- | --- | --- | --- |
| `R001` | Add an all-projects database transfer handler that copies core project data and project-structure/workbench data from one existing database profile to another. | `N001`, `N003`, `N004` | `Projects` appears as a selectable transfer item and integration tests prove records are copied. |
| `R002` | Preserve all project identities and relationships needed for hierarchy, structure canvas, calendar, layout, node bindings, references, lifecycle history, and view state. | `N001` | A copied project can load through project board and workbench services with the same nodes, links, and hierarchy counts. |
| `R003` | Implement all-projects zip export from the active or selected profile into a local `.zip` package. | `N001`, `N002` | A package file is created with a manifest and project table payloads. |
| `R004` | Implement all-projects zip import into a target profile/current profile using the same project-copy inventory and ordered clearing/restoration rules. | `N001`, `N002` | Import into an empty profile recreates projects and workbench records. |
| `R005` | Expose the database-to-database transfer through the existing UI transfer workflows used for new database creation and data-source transfer. | `N003`, `N004` | Browser proof shows `Projects` in the transfer checklist. |
| `R006` | Expose project zip export/import through a user-accessible UI flow. | `N002` | Browser proof shows project zip export/import controls and host proof confirms package creation/import. |
| `R007` | Keep existing processes, agents, provider, and ProjectStructure MCP transfer behavior working. | `N004` | Existing transfer handler tests/builds continue passing and the transfer preview still lists existing handlers. |

## Scope Exceptions

- ProjectStructure leases and operation analytics are not imported/exported as project content unless implementation discovers they are required for user-facing project behavior. They are runtime/diagnostic state rather than portable project data.
- Whole database snapshots remain the system-level database clone/import tool. This bundle adds project-scoped zip import/export, not a replacement for snapshots.
