# Target Solution

## Domain And Persistence

- Every project-structure node record can carry an explicit duration field in seconds so future scheduling consumers have a stable source even when dates are missing.
- Many-to-many dependency links continue to use `ProjectObjectLinkKind.DependsOn`, but the service layer must add deletion or unlink support and dependency-centric read models so all node types participate uniformly.
- Dependency analysis should live in a reusable driver or service that can answer prerequisite readiness, upstream and downstream relationships, and graph-derived scheduling views without duplicating logic in the UI or MCP layer.

## UI And Interaction Model

- The project structure canvas toolbar gains a focused tool cluster for `select`, `dependency`, and `delete` using the requested icon names.
- Dependency mode starts from the currently selected node, shows a live curve preview, keeps standard left-drag movement available, and only creates the link when the user clicks a second node.
- Delete mode returns to the standard cursor, visually highlights hovered nodes or links, deletes the clicked target, and requests confirmation before deleting a node that has multiple dependency relationships or child consequences.
- Existing canvas link rendering remains responsible for curved links and arrowheads, but hit-testing and highlight state must be extended so links can be targeted for deletion.

## Dependency Intelligence And Export

- A dedicated dependency-analysis surface provides readiness answers for agents, prerequisite counts, downstream counts, and a normalized graph view for future consumers.
- Mermaid Gantt export becomes dependency-aware and uses explicit duration seconds when available, falling back to one hour for nodes without duration.
- Existing summary or export code should reuse the same dependency-analysis driver so readiness, schedule order, and Mermaid output stay consistent.

## Validation Boundaries

- Integration tests prove persistence, deletion, readiness, and duration behavior through the service layer.
- Page, component, and runtime tests cover tool-state transitions and deletion safeguards where practical.
- Playwright proof uses a fresh managed SQLite profile seeded with realistic project structure content, captures screenshots for dependency and delete modes, and verifies links stay attached while nodes move.
