---
name: candoitall-api-project-structure
description: Use when creating, reading, editing, running, or reviewing CanDoItAll projects and project-structure nodes through the HTTP API instead of the removed ProjectStructure MCP server.
---

# CanDoItAll Project Structure API

Use this skill when a task needs project, hierarchy, project-structure, dependency, asset, lease, or project-run control through the CanDoItAll web API.

## Access

- Start the CanDoItAll web app and use Swagger/OpenAPI from the running host, usually `http://localhost:5032/swagger` or `https://localhost:7271/swagger`.
- Use `/api/access/status` to check whether JWT bearer authorization is active.
- If JWT is active, create a token from Settings -> API Access or `POST /api/access/tokens`, then send `Authorization: Bearer <token>`.
- Do not reinstall or use `candoitall_projectstructure`; that MCP server has been removed.

## Primary Routes

- Project records: `/api/projects`.
- Project hierarchy: `/api/projects/{projectId}/hierarchy`.
- Project structure read and focused mutations: `/api/project-structure/projects/{projectId}/structure/read`, `/nodes`, `/nodes/{nodeId}`, `/nodes/{nodeId}/type`, `/status`, `/progress`, `/markers`, `/priority`, `/move`, `/recompose`, `/reparent`, `/delete`.
- Dependency and link control: `/links`, `/links/unlink`, `/dependencies/link`, `/dependencies/unlink`, `/dependencies/query`.
- Assets: `/assets`, `/assets/{nodeId}`, `/assets/{nodeId}/content`, `/assets/{nodeId}/revisions`.
- Process nodes: `/nodes/{nodeId}/process-definition` and `/nodes/{nodeId}/process/start`.
- Coordination and review: `/leases/acquire`, `/leases/current`, `/leases/release`, `/knowledge/query`, `/analytics/query`.

## Operating Rules

- Prefer focused endpoints over fetching or sending entire graphs.
- Acquire a project lease before mutating shared project structure. Use repo-branch leases for branch-wide coordination.
- For typed project blocks, keep `objectType` as `ProjectBlock` and use lowercase `objectSubtype` values such as `feature`, `architecture`, `implementation`, `testing`, `delivery`, `research`, `risk`, `deployment`, `operations`, `repos`, or `dockers`.
- Mermaid diagrams are `File` asset nodes with `objectSubtype` `mermaid`; put Mermaid source in notes or asset content.
- Other generated files should also be `File` nodes with an appropriate subtype, not invented project block enum names.
- Write approval blockers into the graph with `/approvals/request` instead of leaving them only in chat.
- After mutations, query analytics and read back only the affected nodes or links.

## Validation

- Use Swagger to confirm the route shape before writing client code.
- For node mutations, read back the specific node id and relevant links/dependencies.
- For assets, verify both metadata and `/content` when content matters.
