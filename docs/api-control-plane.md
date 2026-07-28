# API Control Plane

Last source review: 2026-07-28.

The web-hosted HTTP API is the supported external automation boundary for CanDoItAll. Request and response schemas come from the running OpenAPI document; this page records the durable route families and operating rules.

## Start And Discover

From the repository root:

```powershell
dotnet run --project src/App/CanDoItAll.Web/CanDoItAll.Web.csproj
```

The default development profile listens on `http://localhost:5032`.

| Endpoint | Purpose |
| --- | --- |
| `GET /_dev/runtime` | Local runtime readiness and diagnostics. |
| `GET /api/access/status` | API enablement and authorization status. This endpoint is anonymous. |
| `GET /openapi/v1.json` | OpenAPI document when `Api:OpenApiEnabled` is enabled. |
| `GET /swagger/v1/swagger.json` | Swagger-compatible alias for the same document. |

OpenAPI endpoints require authorization when API authorization is enabled.

## Access Configuration

Defaults are defined in [`appsettings.json`](../src/App/CanDoItAll.Web/appsettings.json).

| Setting | Default | Meaning |
| --- | --- | --- |
| `Api:Enabled` | `true` | Maps the main `/api` route families. |
| `Api:OpenApiEnabled` | `true` | Maps the OpenAPI endpoints. |
| `Api:Authorization:Enabled` | `false` | Requires bearer authorization for the API groups when enabled. |
| `Api:Authorization:Issuer` | `CanDoItAll.Api` | JWT issuer. |
| `Api:Authorization:Audience` | `CanDoItAll.Api` | JWT audience. |
| `Api:Authorization:SigningKey` | empty | HS256 key; at least 32 UTF-8 bytes when authorization is enabled. |
| `Api:Authorization:DefaultTokenLifetimeMinutes` | `480` | Default issued-token lifetime. |
| `Api:Authorization:MaxTokenLifetimeMinutes` | `1440` | Maximum issued-token lifetime. |

The Project Structure API is mapped separately from `Api:Enabled`, but it applies the same authorization switch. The development default of open access is not suitable for a remotely reachable deployment. Keep signing keys and bearer tokens out of tracked files and logs.

## Current Route Families

The canonical family registration is in [`ApiEndpointRouteBuilderExtensions.cs`](../src/App/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs); Project Structure is registered from [`Program.cs`](../src/App/CanDoItAll.Web/Program.cs).

| Base path | Responsibility | Source |
| --- | --- | --- |
| `/api/access` | API status and token issue. | [`ApiEndpointRouteBuilderExtensions.cs`](../src/App/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs) |
| `/api/projects` | Project records, access items, hierarchy, and subproject relationships. | [`ProjectsApi.cs`](../src/App/CanDoItAll.Web/Api/ProjectsApi.cs) |
| `/api/project-structure` | Structure reads and mutations, tasks, process/workflow node operations, assets, imports, leases, knowledge, and analytics. | [`ProjectStructureAgentApi.cs`](../src/App/CanDoItAll.Web/ProjectStructureAgentApi.cs) |
| `/api/agents` | Agent, provider, capability, memory, chat, execution, approval, artifact, receipt, checkpoint, log, metric, and runtime-snapshot operations. | [`AgentsApi.cs`](../src/App/CanDoItAll.Web/Api/AgentsApi.cs) |
| `/api/agent-recruiting` | Candidate interviews, attempts, human reviews, interview history, and readiness. | [`AgentRecruitingApi.cs`](../src/App/CanDoItAll.Web/Api/AgentRecruitingApi.cs) |
| `/api/prompt-gallery` | Prompt Gallery search, artifacts, versions, review, and application. | [`PromptGalleryApi.cs`](../src/App/CanDoItAll.Web/Api/PromptGalleryApi.cs) |
| `/api/workflows` | Workflow settings, definitions, versions, runs, external requests, evidence, and analytics. | [`WorkflowsApi.cs`](../src/App/CanDoItAll.Web/Api/WorkflowsApi.cs) |
| `/api/processes` | Process launch, dispatch, operator actions, live projections, durable run records, graphs, and analytics. | [`ProcessesApi.cs`](../src/App/CanDoItAll.Web/Api/ProcessesApi.cs) |
| `/api/cognitive-memory` and `/api/cognitive-memory/v1` | Retired compatibility surfaces. | [`CognitiveMemoryApi.cs`](../src/App/CanDoItAll.Web/Api/CognitiveMemoryApi.cs) |
| `/api/plugins` | Plugin catalog, configuration, and runtime operations. | [`PluginsApi.cs`](../src/App/CanDoItAll.Web/Api/PluginsApi.cs) |
| `/api/crm-hr` | CRM, workforce, recruiting, capacity, and relationship operations. | [`CrmHrApi.cs`](../src/App/CanDoItAll.Web/Api/CrmHrApi.cs) |

Use OpenAPI for exact methods and schemas. Do not copy a complete generated endpoint inventory into maintained documentation.

## Process Contract

`GET /api/processes/contract` returns the current process route contract. The source-backed routes are:

| Method | Route | Use |
| --- | --- | --- |
| `GET` | `/api/processes/contract` | Discover the route contract. |
| `POST` | `/api/processes/launch/check` | Validate launch readiness without creating a run. |
| `POST` | `/api/processes/launch` | Create and optionally queue a durable run. |
| `POST` | `/api/processes/runs/{runId}/dispatch` | Execute ready work. |
| `POST` | `/api/processes/runs/{runId}/cancel` | Request cancellation. |
| `POST` | `/api/processes/runs/{runId}/steps/{stepInstanceId}/rework` | Request focused step rework. |
| `GET` | `/api/processes/live` | Read live run projections. |
| `GET` | `/api/processes/runs` | Search durable run records. |
| `GET` | `/api/processes/runs/analytics` | Aggregate durable run records. |
| `GET` | `/api/processes/runs/{runId}` | Read the live/detail projection. |
| `GET` | `/api/processes/runs/{runId}/summary` | Read durable facts and the bounded narrative summary. |
| `GET` | `/api/processes/runs/{runId}/graph` | Read a paged run graph. |
| `GET` | `/api/processes/runs/{runId}/history` | Read timeline history. |

`launch/check` is non-mutating. `launch` persists the run when readiness permits; `execute: false` prevents immediate dispatch queueing but does not turn the launch into a dry run. See the [operator runbook](process-agent-operator-runbook.md) for triage and configuration.

## Retired Cognitive Memory API

The base host no longer registers the legacy in-process Cognitive Memory module or Qdrant/RAG integration.

- `GET /api/cognitive-memory/contract` and `GET /api/cognitive-memory/v1/contract` return the `retired-v1` contract.
- Every other request under either base path returns `410 Gone` with migration guidance.
- Generic memory is configured through provider profiles and operated from the `/memory` UI and registered memory runtime tools.
- Use the native remote memory provider only when a separate native Cognitive Memory service is deployed.

See [Memory providers](cognitive-memory/README.md) for the current boundary.

## HTTP APIs Versus Runtime Tools

An HTTP route is not automatically available as an in-agent tool. Runtime tools are attached through MAF built-ins, capability descriptors, MCP/A2A integrations, provider-native tools, or one of the registered `IAgentRuntimeToolProvider` implementations. The current first-party providers cover Memory, Project Structure, Image Generation, Workflow, Prompt Gallery, Prompts Curator, Workflow Curator, Capability Curator, HR, and Scheduler.

Attachment remains subject to execution purpose, agent permissions, assigned capabilities, project/process scope, and invocation policy. See [Agent runtime tool surface](agent-runtime-tool-surface.md).

## Operator Skills

Reusable `candoitall-api-*` skills are maintained in the canonical [CanDoItAll.SharedInfo skill source](https://github.com/fyziktom/CanDoItAll.SharedInfo/tree/main/codex/skills). No product-repository source copy is maintained.

For every mutation:

1. Inspect OpenAPI for the exact request type.
2. Make the smallest focused call.
3. Read back the affected resource, run record, artifact, or receipt.
4. Treat a transport success without durable readback as incomplete evidence.

## Validation

For documentation-only changes:

```powershell
git diff --check
```

For API behavior changes, add focused route and application-service tests, then use the stable repository gate in [Testing](testing.md).
