# API Control Plane

The current project, process, workflow, cognitive-memory, and agent automation path is the CanDoItAll web-hosted HTTP API. The old Processes and ProjectStructure MCP servers are suppressed; use the API and the repo-managed `candoitall-api-*` skills for those surfaces.

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

Use `/api/processes` for the current process runtime control plane. The source-grounded route set is intentionally smaller than older process-rewrite docs:

- `GET /api/processes/contract`
- `POST /api/processes/launch`
- `POST /api/processes/runs/{runId}/dispatch`
- `POST /api/processes/runs/{runId}/cancel`
- `POST /api/processes/runs/{runId}/steps/{stepInstanceId}/rework`
- `GET /api/processes/live`
- `GET /api/processes/runs/{runId}`
- `GET /api/processes/runs/{runId}/history`

Definition authoring, template import/export, artifact/assignment detail, escalations, direct messages, approvals, manager directives, and analytics are not currently exposed by `src/CanDoItAll.Web/Api/ProcessesApi.cs`. Do not document or call those as current HTTP routes until they are reintroduced with typed handlers and tests.

For source-level details, use [Processes, MAF, and providers implementation map](processes-maf-providers-implementation-map.md).

### Agents

Use `/api/agents` for AgentFramework catalog, provider, capability, chat, and execution operations:

- agents: list, bootstrap, editor read, save, delete, clone, convert to template, export, import
- providers: list, editor read, save, delete, test, test-chat, Ollama modelfile
- capabilities and memory
- chat sessions and chat workspace
- execution runs, pending approvals, artifacts, checkpoints, tool receipts, logs, metrics, approvals, runtime snapshots

Validate execution by reading run detail, artifacts, tool receipts, checkpoints, and metrics. A single status field is not enough for process-critical work.

Provider behavior is part of this surface. Current provider profiles include private-provider flags, per-model token prices, tags, native hosted tool support, local MCP support, image generation support, structured output support, and reasoning-effort policy for OpenAI-like Responses models. See [Provider capability and pricing](provider-capability-and-pricing.md).

### Workflows

Use `/api/workflows` for workflow settings, executor catalog, definitions, versions, components, test runs, runtime runs, external requests, artifacts, checkpoints, events, and analytics.

Runtime runs can be started through `/api/workflows/runs/start` or from a definition version. `WorkflowRunStartApiRequest` supports `workflowId`, `versionId`, `inputJson`, `requestedBackend`, `sourceProcessRunId`, and `sourceProcessAssignmentId`. The source-process fields are important when workflow activity is part of a process run.

### Cognitive Memory

Use `/api/cognitive-memory` for project-scoped source ingestion, consolidation, review, recall, probing, self-regulation, learning proposals, cross-project promotion, and distributed memory jobs. Start with [Cognitive Memory API](cognitive-memory/operations/api.md) for the current route list and [Cognitive Memory stage assessment](cognitive-memory/current-state/stage-assessment.md) before treating a behavior as beta-ready.

Important operating rule: Qdrant/RAG is a rebuildable projection. Durable memory facts, source evidence, claims, review decisions, traces, and proposals live in the active `AppDbContext` profile.

New integrations should prefer `/api/cognitive-memory/v1`. Legacy `/api/cognitive-memory` routes remain available for compatibility.

### Plugins And Projects

`/api/projects` and `/api/plugins` are documented here as control-plane route families, but they do not currently have dedicated repo-managed API skills. Use OpenAPI plus the owning source files until a dedicated skill is justified.

### Internal Agent Tools

The internal MAF/runtime-provider tool surface is narrower than the HTTP API. Current registered first-party runtime tool providers include project-structure tools from Workbench and image-generation tools from the AgentFramework module. A concrete direct process runtime tool provider is not present in the current source tree; process control currently goes through `/api/processes` and the project-structure bridge tools that can link and start processes. See [Agent runtime tool surface](agent-runtime-tool-surface.md) for current direct tools and HTTP-only operations.

## Development Workflow

1. Start the web app and confirm `/_dev/runtime`.
2. Check `GET /api/access/status`.
3. Inspect OpenAPI JSON for exact request and response shapes.
4. Use the relevant repo-managed skill:
   - [Project Structure API skill](../codex/skills/candoitall-api-project-structure/SKILL.md)
   - [Processes API skill](../codex/skills/candoitall-api-processes/SKILL.md)
   - [Agents API skill](../codex/skills/candoitall-api-agents/SKILL.md)
   - [Workflows API skill](../codex/skills/candoitall-api-workflows/SKILL.md)
   - [Cognitive Memory API skill](../codex/skills/candoitall-api-cognitive-memory/SKILL.md)
5. Make the smallest focused API call.
6. Read back the changed resource or run evidence route.

## Validation

For API behavior changes, add focused tests around the owning service and the API route family. For documentation-only changes, run:

```powershell
git diff --check
```

Use the stable test gate from [testing.md](testing.md) when source code changes affect runtime behavior.
