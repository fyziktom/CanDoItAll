# Normalized Requirements

## Functional Requirements

- `RQ-001` Every project-structure node type, including notes, can participate in dependency relationships as a prerequisite or dependent.
- `RQ-002` A node can depend on multiple other nodes, and a node can be the prerequisite for multiple downstream nodes.
- `RQ-003` The canvas top toolbar exposes `select`, `dependency`, and `delete` tools using the requested icons.
- `RQ-004` Dependency mode starts from the selected node, shows a visible pending connection or curve preview, and only commits the dependency when the user clicks a second node.
- `RQ-005` Standard left-click dragging remains available while dependency mode is active unless the click targets another node to complete a dependency.
- `RQ-006` Delete mode can delete dependency links directly and visually highlights the hovered deletable target before click.
- `RQ-007` Deleting a multiply-connected node requires confirmation before removal.
- `RQ-008` Dependency links render with a directional arrow and stay visually attached while connected nodes move.
- `RQ-009` The workbench or service layer exposes dependency-aware information suitable for readiness checks, prerequisite inspection, and downstream graph consumers.
- `RQ-010` The system can produce Mermaid Gantt output from the dependency graph.
- `RQ-011` Nodes expose an explicit duration value in seconds.
- `RQ-012` Mermaid or export scheduling uses the explicit duration when present and defaults to one hour when duration is absent.
- `RQ-013` Automated validation uses a fresh SQLite profile and not the legacy database.
- `RQ-014` Playwright MCP proof includes screenshots and written screenshot-review notes for the new dependency authoring and delete UX.
- `RQ-015` Fresh-SQLite validation data should be realistic enough to exercise different node kinds, dependency chains, and progress or status surfaces.

## Non-Functional Requirements

- `NFR-001` Dependency direction must stay consistent across persistence, checklist or readiness logic, UI rendering, and Mermaid export.
- `NFR-002` New graph and duration fields must not break existing hierarchy-based project structure behavior.
- `NFR-003` Browser-visible behavior must be proven on a real canvas session, not inferred only from unit or component tests.
