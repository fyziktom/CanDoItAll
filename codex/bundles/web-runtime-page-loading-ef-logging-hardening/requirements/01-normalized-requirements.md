# Normalized Requirements

## `REQ-PROC-001` Processes Workspace Lazy Loading

The process workspace initial load must fetch only data needed for the initial visible workspace and selected definition. Expensive runtime pane, analytics, improvement, party, executor, workflow, and manager-agent option data must be loaded only when the opened tab or command requires it.

Acceptance:

- Initial workspace load no longer calls runtime/analytics/template-option APIs that are only needed for hidden tabs.
- Runtime and analytics tabs still load correct data when selected.
- Role/template dialogs still have the options they require before opening.
- Existing process workspace behavior remains covered by component tests.

## `REQ-PROJ-001` Project Structure Canvas Create Latency

After a project structure node is persisted successfully, the page must update the current canvas surface locally with the created node, hierarchy link, optional user links, and local move results instead of reloading the full assembled structure.

Acceptance:

- Newly created nodes are visible and selected immediately after persistence.
- Downward-stack movement and pending links remain reflected on the canvas.
- The create path avoids the full-surface reload call for the normal existing-surface case.
- Existing inline-update no-reload behavior remains intact.

## `REQ-WF-001` Workflows Page Template/Catalog Loading

The workflows page must stop running template seed/catalog work during ordinary page initialization. Component library and provider option data must be loaded only when a selected tab or command needs them.

Acceptance:

- Initial workflows page load does not invoke example catalog seeding or full component-library listing.
- Editor/templates/analytics tabs and starter workflow creation load component/provider options when required.
- Existing definition list and selected definition load behavior remains intact.

## `REQ-EF-001` EF Console Logging Option

Entity Framework console logging must be opt-in. A strongly typed database option must disable EF command/infrastructure console noise by default, with configuration available to turn it back on.

Acceptance:

- `DatabaseOptions` exposes an EF console logging option that defaults to false.
- Web host logging filters suppress EF command/infrastructure categories when the option is false.
- App settings make the default explicit.
- Unit tests prove the default and config binding behavior.
