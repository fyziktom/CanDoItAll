# Project Structure MCP Transition Note

## Current Status

`CanDoItAll.Mcp.ProjectStructure` is not an active MCP server in the current repository shape. Project, hierarchy, asset, dependency, lease, process-node, and analytics work now goes through the web-hosted HTTP API.

Do not reinstall or call `candoitall_projectstructure` in current sessions. It may return later, but current documentation should route agents and developers through the HTTP API.

## Current Replacement

Use the web API and the repo-managed project-structure API skill:

- API overview: [API control plane](api-control-plane.md)
- Skill guidance: [codex/skills/candoitall-api-project-structure/SKILL.md](../codex/skills/candoitall-api-project-structure/SKILL.md)
- Source routes: [ProjectStructureAgentApi.cs](../src/CanDoItAll.Web/ProjectStructureAgentApi.cs)

Key route families:

- project records and hierarchy: `/api/project-structure/projects`
- structure read and node mutation: `/api/project-structure/projects/{projectId}/structure/read`, `/nodes`, `/nodes/{nodeId}`
- status, progress, markers, priority, movement, recomposition, reparenting, and delete commands: `/api/project-structure/projects/{projectId}/nodes/...`
- process nodes: `/api/project-structure/projects/{projectId}/nodes/{nodeId}/process-definition` and `/process/start`
- dependencies and links: `/dependencies/query`, `/dependencies/link`, `/dependencies/unlink`, `/links`, `/links/unlink`
- assets and revisions: `/assets`, `/assets/{nodeId}`, `/assets/{nodeId}/content`, `/assets/{nodeId}/revisions`
- coordination and review: `/leases/acquire`, `/leases/renew`, `/leases/release`, `/leases/current`, `/knowledge/query`, `/analytics/query`

## Migration Guidance

1. Start `src/CanDoItAll.Web`.
2. Check API status with `GET /api/access/status`.
3. If API authorization is enabled, use a Settings-generated bearer token or an already-authorized token.
4. Acquire a project lease before mutating shared structure.
5. Prefer focused endpoints and read back only the changed node, link, dependency, or asset.

## Removed Setup Commands

Old setup commands such as `Install-CanDoItAllProjectStructureMcp.ps1` and `candoitall_projectstructure` should not be used for current work. The full MCP reinstall script now removes stale `candoitall_projectstructure` config sections from local Codex config.
