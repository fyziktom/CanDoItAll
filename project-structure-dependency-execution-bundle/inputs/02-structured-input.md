# Structured Input

## Core Objective

- Add explicit execution dependencies to project-structure nodes, make them visible and editable in the canvas, and surface dependency-aware scheduling/export data so later Gantt and MCP workflows can trust it.

## Hard Constraints

- Support all node types, including simple notes.
- A node may depend on one or many nodes, and may block one or many downstream nodes.
- Dependency authoring must be available from a canvas top-toolbar toolset with `select`, `dependency`, and `delete` tools.
- Left click must remain available for moving nodes while dependency mode is active; dependency creation only commits when the user clicks a second node.
- Delete mode must be able to remove both nodes and dependency connections, with clear hover highlight and confirmation when deleting a node with multiple linked dependents or prerequisites.
- Connection curves must show direction with arrows and remain attached while nodes move.
- Dependency intelligence must be consumable for future Gantt generation and MCP agent readiness checks.
- Dependency-to-Gantt export should support Mermaid and default unknown durations to one hour.
- Store node duration in seconds rather than milliseconds or ticks.
- Validation must use a fresh SQLite database, not a legacy DB, and must include Playwright MCP plus screenshots.

## Source Artifacts

- `inputs/00-original-request.md`
- `inputs/01-source-artifacts.md`
- Repo files listed in `inventories/01-scope-inventory.md`

## Input Coverage Signals

- `N001` Dependency semantics apply to every node type, including notes.
- `N002` Many-to-many dependency graph support is required.
- `N003` Toolbar must expose `select`, `dependency`, and `delete` modes with the requested icons.
- `N004` Dependency mode must preserve drag and move behavior and only complete when a second node is clicked.
- `N005` Delete mode must highlight hovered targets and remove links as well as nodes.
- `N006` Deleting a multiply-connected node must require confirmation.
- `N007` Canvas links must render direction arrows and stay attached during node moves.
- `N008` Dependency intelligence service or driver is required for readiness checks and future graph consumers.
- `N009` Mermaid Gantt conversion is required, with one-hour default duration when timing is absent.
- `N010` Node contracts should expose duration in seconds.
- `N011` Validation must use a fresh SQLite profile with richer mock data, not the legacy database.
- `N012` Playwright MCP validation must include screenshots and written review findings.
- `N013` Prepared bundle data should be reusable as realistic project-structure mock content where practical.
- `N014` Execution may record progress information back into nodes while bundle phases run.

## Dependency And Sequencing Signals

- Persistence and contract changes must land before toolbar UX, dependency intelligence, or export logic can close.
- Toolbar UX must target the same dependency model and deletion semantics used by service and MCP surfaces.
- Fresh-DB seed and test work depends on stable contracts plus working UI tools and export surfaces.
- Browser proof is the final gate because it validates the authoring workflow against the new SQLite-backed data path.

## Validation Expectations

- Targeted integration tests must prove many-to-many `DependsOn` persistence, deletion, and readiness data for all node types.
- Component and runtime tests must prove canvas tool state, hover and delete behavior, and dependency preview and commit flows where feasible.
- Dependency intelligence and Mermaid export must be covered with deterministic tests, including the one-hour default duration path.
- Fresh SQLite end-to-end validation must create or import realistic project structure data and prove dependency creation, deletion, movement persistence, and visible arrows in the browser.

## UI Validation Strategy

- Run a maximized desktop pass first on the project structure canvas using a fresh SQLite profile seeded specifically for dependency scenarios.
- Capture at least one screenshot during dependency mode and one during delete or highlight mode, then review whether the cursor or tool state, arrow direction, hover highlight, and link persistence are visually obvious.
- Run a narrower-width follow-up if toolbar wrapping or canvas overlay behavior changes at tablet width.

## Browser Validation Analytics

- Record route, viewport, Playwright MCP actions, screenshot paths, and result per phase in `reviews/01-execution-report.md`.
- Browser-visible phases must log both the interaction steps and the screenshot findings, not only the image paths.

## Working Assumptions

- Existing `ProjectObjectLinkKind.DependsOn` is the canonical persistence and link type and should be strengthened rather than replaced.
- Existing `StartUtc` and `EndUtc` can continue to exist alongside a new explicit duration-seconds field.
- A dedicated dependency-analysis service can be introduced inside the workbench module without needing to redesign the whole project structure API surface.
- Existing Playwright infrastructure already supports managed fresh SQLite profiles and can be reused instead of standing up a legacy database.

## Primary Risks

- Canvas runtime hit-testing may not currently support link hover and delete, which could require careful JS runtime changes.
- Adding duration to shared contracts may ripple through migrations, summaries, MCP contracts, and tests.
- Browser proof can be misleading if it uses stale or legacy DB data rather than the requested fresh SQLite path.
- Dependency semantics already exist in some checklist code; changing link direction or readiness interpretation incorrectly could break downstream logic.
