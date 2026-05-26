# Structured Input

## Raw Notes

| ID | Source | Exact note | Initial disposition |
| --- | --- | --- | --- |
| N001 | User | check roles definitions, process step definitions in UI and add missing options. | Normalize as UI and typed-template vocabulary parity; owned by SB01. |
| N002 | User | clear processes history, runs, etc in development db. reload new updated processes templates. | Normalize as process-only development data reset plus template reload; owned by SB02. |
| N003 | User | we need to keep our other settings of agents, plugins, memory and projects with their project structure and related files. | Hard preservation constraint for SB02; no whole-database drop, no project or managed-file deletion. |

## Initial Scope Decision

- Use `feedback` profile because the request is concrete and has two operational notes.
- Treat SB01 as a critical foundation because the process reload in SB02 depends on template projection preserving all vocabulary instead of silently falling back.
- Treat SB02 as destructive operational work with a strict process-table-only boundary.
