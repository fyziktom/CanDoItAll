# Target Solution

## Cleanup Direction

- Delete only the two MCP adapter projects and their tests. Keep shared MCP Core and other MCP servers.
- Keep project, process, and agent domain services as canonical logic. API endpoint handlers stay thin and delegate to existing services.
- Rehome project-structure development guidance into API skills. Skills should prefer Swagger/OpenAPI discovery and small focused endpoints over pulling whole object graphs.
- The project-structure HTTP route should no longer advertise MCP as the primary surface. Use `/api/project-structure` for the API-facing route and update tests/skills accordingly.
- Processes parity gaps are filled in `ProcessesApi` by calling existing template services.
- Settings UI should keep the API Access/JWT section and remove the Project Structure MCP setup/profile tab.

## Skill Direction

- `candoitall-api-project-structure`: use `/api/project-structure` and `/api/projects`; document lease, typed node, Mermaid/file asset, dependency, checklist, import, analytics, process-node execution, and filtering guidance.
- `candoitall-api-processes`: use `/api/processes`; document definitions, runs, filtered detail, step-scoped artifacts, direct messages, launch plans, HR matching, template import, template detail, and baseline scenarios.
- `candoitall-api-agents`: use `/api/agents`; document provider/capability/profile administration, chat sessions, execution runs, artifacts, checkpoints, receipts, approvals, logs, metrics, and runtime snapshots.

## Boundary Rules

- Do not create parallel services that duplicate UI/MCP behavior.
- Do not keep MCP install scripts as wrappers around removed projects.
- Do not remove old database tables in this cleanup unless current code requires it; dropping persisted user data is not needed for the requested cleanup.
