# Target Solution

## End State

- Project-structure node actions are driven by resolvers that understand typed metadata instead of only managed assets.
- Runtime nodes include Script, Environment, and command-backed Docker infrastructure nodes, and each valid runtime plan carries `DisplayCommand`, `StartupScript`, and `WorkingDirectory`.
- Local open nodes include managed files, workspace artifacts, local folders, local repository folders, deployment folders, and local-drive file metadata when the path guard permits them.
- Repository and link nodes surface GitHub/GitLab recognition through metadata, display, aliases, and catalog guidance.
- Agent tool catalog guidance gives agents enough precise schema hints to create runtime scripts, folders, links, repositories, and file nodes without inventing node types.

## Boundaries

- Keep process launching centralized in `ProjectStructureRuntimeLauncher`.
- Keep Explorer opening centralized in `ProjectStructureLocalFileOpener`.
- Keep actionCapabilities centralized in `ProjectStructureNodeActionCapabilityResolver`.
- Keep node creation schema and aliases in the shared project-structure catalog and request converters.
- Do not bypass workspace safety guards or add direct execution through Explorer.

## Validation Surfaces

- Component and unit tests for resolver behavior and catalog content.
- Blazor route validation through Playwright MCP for visible create dialogs and quick-action actions.
- Bundle execution report as the source of proof rows and raw-note closure.
