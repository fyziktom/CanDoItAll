# API Control Plane

The current project, process, and agent automation path is the CanDoItAll web-hosted HTTP API. The old Processes and ProjectStructure MCP servers are suppressed; use the API and the repo-managed `candoitall-api-*` skills for those surfaces.

## Host And Discovery

Start the web host:

```powershell
dotnet run --project src/CanDoItAll.Web
```

Development readiness is exposed at `/_dev/runtime`. API status is exposed at `GET /api/access/status` when the API is enabled.

OpenAPI JSON is mapped by the web host through `MapOpenApi()` and the Swagger-compatible JSON route `/swagger/{documentName}/swagger.json` when `Api:OpenApiEnabled` is true.

## Access Settings

API settings live under the `Api` configuration section:

| Setting | Default | Purpose |
| --- | --- | --- |
| `Api:Enabled` | `true` | Enables the `/api` route group. |
| `Api:OpenApiEnabled` | `true` | Enables OpenAPI JSON endpoints. |
| `Api:Authorization:Enabled` | `false` | Requires bearer authorization for API route groups when true. |
| `Api:Authorization:Issuer` | `CanDoItAll.Api` | JWT issuer. |
| `Api:Authorization:Audience` | `CanDoItAll.Api` | JWT audience. |
| `Api:Authorization:SigningKey` | empty | HS256 signing key; must be at least 32 UTF-8 bytes when authorization is enabled. |
| `Api:Authorization:DefaultTokenLifetimeMinutes` | `480` | Default issued token lifetime. |
| `Api:Authorization:MaxTokenLifetimeMinutes` | `1440` | Maximum issued token lifetime. |

`GET /api/access/status` is anonymous. If bearer authorization is enabled, create tokens through the Settings UI or an already-authorized administrative path. Do not put signing keys or bearer tokens in tracked files.

## Route Families

All routes below are source-grounded in `src/CanDoItAll.Web/Api` and `src/CanDoItAll.Web/ProjectStructureAgentApi.cs`.

### Projects

Use `/api/projects` for project records and hierarchy:

- list, create, read, update, and delete project records
- list access items and hierarchy links
- read project hierarchy
- attach, detach, and reconnect subprojects

### Project Structure

Use `/api/project-structure` for agent-oriented structure operations:

- node catalog and project list
- structure read
- node create, update, type, metadata, status, progress, markers, priority, move, recompose, reparent, command, and delete
- process-definition and process-start operations from nodes
- approvals, checklists, dependencies, links, assets, revisions, imports, knowledge queries, leases, and analytics

Acquire a lease before mutating shared project structure. Prefer focused reads after mutations.

### Processes

Use `/api/processes` for process definitions, templates, runtime control, and operator workflows:

- definitions: list, editor read, save, publish, delete, export, and import
- templates: list, detail, envelope, Mermaid, baseline scenarios, and import
- runs: list, detail, start, stop, steps, artifacts, assignments, analytics
- operator actions: transitions, rerun-agent, assignment resolution, escalations, approvals, rework, manager directives, and direct messages
- launch planning: launch plans, HR matching, approval submission and decisions, provisioning, execution, and candidate selections

For governed agent-run processes, use PostgreSQL when `Processes:Runtime:RequirePostgreSqlForAgentAutomation` is enabled.

### Agents

Use `/api/agents` for AgentFramework catalog, provider, capability, chat, and execution operations:

- agents: list, bootstrap, editor read, save, delete, clone, convert to template, export, import
- providers: list, editor read, save, delete, test, test-chat, Ollama modelfile
- capabilities and memory
- chat sessions and chat workspace
- execution runs, pending approvals, artifacts, checkpoints, tool receipts, logs, metrics, approvals, runtime snapshots

Validate execution by reading run detail, artifacts, tool receipts, checkpoints, and metrics. A single status field is not enough for process-critical work.

## Development Workflow

1. Start the web app and confirm `/_dev/runtime`.
2. Check `GET /api/access/status`.
3. Inspect OpenAPI JSON for exact request and response shapes.
4. Use the relevant repo-managed skill:
   - [Project Structure API skill](../codex/skills/candoitall-api-project-structure/SKILL.md)
   - [Processes API skill](../codex/skills/candoitall-api-processes/SKILL.md)
   - [Agents API skill](../codex/skills/candoitall-api-agents/SKILL.md)
5. Make the smallest focused API call.
6. Read back the changed resource or run evidence route.

## Validation

For API behavior changes, add focused tests around the owning service and the API route family. For documentation-only changes, run:

```powershell
git diff --check
```

Use the stable test gate from [testing.md](testing.md) when source code changes affect runtime behavior.
