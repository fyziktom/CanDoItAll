# Current State

## ProjectStructure MCP Surface

The `CanDoItAll.Mcp.ProjectStructure` server is a thin adapter over the web-hosted project-structure API. Its useful surface is not the server implementation itself, but the operational rules embedded in tool descriptions:

- Use project listing, project creation/update, hierarchy, subproject reconnect, structure read, checklist, dependency, node create/update/move/recompose/reparent, approval request, asset read/revision, import, knowledge query, analytics query, and lease endpoints.
- Use leases before mutating shared project or repo-branch state so concurrent agents do not collide.
- For typed project block variants, keep `objectType` as `ProjectBlock` and set lowercase `objectSubtype` values such as `feature`, `architecture`, `implementation`, `testing`, `delivery`, `research`, `risk`, `deployment`, `operations`, `repos`, or `dockers`.
- For Mermaid diagrams, create a `File` asset node with `objectSubtype` `mermaid` and put Mermaid source in notes.
- Other generated files should be `File` nodes with appropriate file subtypes, not invented enum values.
- Use approval-request nodes to write blocked work into the project graph instead of leaving it only in chat.
- Use analytics after changes to inspect what agents actually did.

Current API coverage has the ProjectStructure MCP operations and additional focused commands, but the route is still exposed as `/api/project-structure-mcp` and still carries MCP-era agent-token policy hooks.

## Processes MCP Surface

The `CanDoItAll.Mcp.Processes` server is a thin adapter over process services. Useful guidance and coverage:

- Process definitions: list, editor get, save, publish, delete, export, import.
- Runtime: list runs, get run detail, analytics, start run, transition steps, rerun agent steps, resolve assignments, record artifacts.
- Templates: list folder templates, load detailed template with sidecar metadata and compatibility notes, export Mermaid, import template, and list baseline runtime scenarios.
- Registry: party options and executor options.

The new `/api/processes` surface covers most runtime and template behavior and improves filtering. Missing coverage found from MCP parity:

- Baseline scenario listing from the process template pack.
- A detail endpoint that returns the same sidecar and compatibility payload as `processes_template_get`.

## Existing Cleanup Targets

- Solution entries include `src/CanDoItAll.Mcp.ProjectStructure`, `src/CanDoItAll.Mcp.Processes`, and both MCP test projects.
- Integration tests still reference the two MCP projects for stdio/MCP behavior.
- `tools\Reinstall-CanDoItAllMcps.ps1` publishes and configures both MCPs.
- Separate install scripts exist for both MCPs.
- `.vscode\mcp.json` and `C:\Users\lucys\.codex\config.toml` contain both MCP server entries.
- Settings UI contains a Project Structure MCP tab and setup/profile panel.
- Repo-managed skill `candoitall-processes-mcp` still instructs agents to install and use the Processes MCP.
