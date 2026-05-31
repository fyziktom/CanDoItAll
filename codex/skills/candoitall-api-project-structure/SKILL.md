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
- Project structure read and focused mutations: `/api/project-structure/projects/{projectId}/structure/read`, `/nodes`, `/nodes/{nodeId}`, `/nodes/{nodeId}/type`, `/nodes/{nodeId}/metadata`, `/nodes/statuses`, `/nodes/{nodeId}/status`, `/nodes/progress`, `/nodes/{nodeId}/progress`, `/nodes/markers`, `/nodes/{nodeId}/markers`, `/nodes/priorities`, `/nodes/{nodeId}/priority`, `/nodes/move`, `/nodes/recompose`, `/nodes/{nodeId}/reparent`, `/nodes/reparent`, `/nodes/move-to-new-subproject`, `/nodes/{nodeId}/move-descendants-to-project`, `/nodes/{nodeId}/command`, `/nodes/{nodeId}/delete`.
- Dependency and link control: `/links`, `/links/unlink`, `/dependencies/link`, `/dependencies/unlink`, `/dependencies/query`.
- Assets: `/assets`, `/assets/{nodeId}`, `/assets/{nodeId}/content`, `/assets/{nodeId}/revisions`.
- Process nodes: `/nodes/{nodeId}/process-definition` and `/nodes/{nodeId}/process/start`.
- Workflow nodes: `/nodes/{nodeId}/workflow-add-options`, `/nodes/{nodeId}/workflow-definition`, `/nodes/{nodeId}/workflow/start`, `/nodes/{nodeId}/workflow/status`.
- Coordination and review: `/approvals/request`, `/checklists/query`, `/leases/acquire`, `/leases/renew`, `/leases/current`, `/leases/release`, `/knowledge/query`, `/analytics/query`.

## Direct Tool Boundary

The internal MAF project-structure tool surface currently exposes 28 direct tools. The HTTP API exposes 51 project-structure routes. Node type-only changes, metadata/status/progress/marker/priority bulk and single update routes, node command execution, node process/workflow operations, node delete, generic link/unlink routes, asset content download, and lease renew are HTTP-only unless a typed runtime tool is deliberately added with policy and approval coverage.

## Operating Rules

- Prefer focused endpoints over fetching or sending entire graphs.
- Acquire a project lease before mutating shared project structure. Use repo-branch leases for branch-wide coordination.
- For typed project blocks, keep `objectType` as `ProjectBlock` and use lowercase `objectSubtype` values such as `feature`, `architecture`, `implementation`, `testing`, `delivery`, `research`, `risk`, `deployment`, `operations`, `repos`, or `dockers`.
- Mermaid diagrams are `File` asset nodes with `objectSubtype` `mermaid`; put Mermaid source in notes or asset content.
- Other generated files should also be `File` nodes with an appropriate subtype, not invented project block enum names.
- Write approval blockers into the graph with `/approvals/request` instead of leaving them only in chat.
- After mutations, query analytics and read back only the affected nodes or links.
- Use `/nodes/{nodeId}/workflow/status` after starting node-linked workflows. Do not infer workflow completion from process or project node state alone.
- Use `/assets/{nodeId}/content` when the actual file bytes matter; metadata alone is not content proof.

## Validation

- Use Swagger to confirm the route shape before writing client code.
- For node mutations, read back the specific node id and relevant links/dependencies.
- For assets, verify both metadata and `/content` when content matters.

## Source Route Appendix

<!-- api-docs-skills-parity:routes:start -->

Project Structure API route appendix. Generated from Minimal API registrations; rerun `.codex/tmp/api-docs-skills-gap-map/update-skill-route-appendices.mjs` when routes change.

| Method | Route |
| --- | --- |
| `POST` | `/api/project-structure/analytics/query` |
| `POST` | `/api/project-structure/imports` |
| `POST` | `/api/project-structure/knowledge/query` |
| `POST` | `/api/project-structure/leases/acquire` |
| `GET` | `/api/project-structure/leases/current` |
| `POST` | `/api/project-structure/leases/release` |
| `POST` | `/api/project-structure/leases/renew` |
| `GET` | `/api/project-structure/node-catalog` |
| `GET` | `/api/project-structure/projects` |
| `POST` | `/api/project-structure/projects` |
| `POST` | `/api/project-structure/projects/{parentProjectId:guid}/subprojects` |
| `PUT` | `/api/project-structure/projects/{projectId:guid}` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/approvals/request` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/assets` |
| `GET` | `/api/project-structure/projects/{projectId:guid}/assets/{nodeId}` |
| `GET` | `/api/project-structure/projects/{projectId:guid}/assets/{nodeId}/content` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/assets/{nodeId}/revisions` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/dependencies/link` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/dependencies/query` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/dependencies/unlink` |
| `GET` | `/api/project-structure/projects/{projectId:guid}/hierarchy` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/checklists/query` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/links` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/links/unlink` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes` |
| `PUT` | `/api/project-structure/projects/{projectId:guid}/nodes/{nodeId}` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/{nodeId}/command` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/{nodeId}/delete` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/{nodeId}/markers` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/{nodeId}/metadata` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/{nodeId}/move-descendants-to-project` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/{nodeId}/priority` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/{nodeId}/process-definition` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/{nodeId}/process/start` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/{nodeId}/progress` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/{nodeId}/reparent` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/{nodeId}/status` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/{nodeId}/type` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/{nodeId}/workflow-add-options` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/{nodeId}/workflow-definition` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/{nodeId}/workflow/start` |
| `GET` | `/api/project-structure/projects/{projectId:guid}/nodes/{nodeId}/workflow/status` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/markers` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/move` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/move-to-new-subproject` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/priorities` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/progress` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/recompose` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/reparent` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/nodes/statuses` |
| `POST` | `/api/project-structure/projects/{projectId:guid}/structure/read` |

<!-- api-docs-skills-parity:routes:end -->
