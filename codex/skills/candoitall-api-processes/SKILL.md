---
name: candoitall-api-processes
description: Use when launching, dispatching, cancelling, reworking, observing, or reviewing CanDoItAll process runs through the current HTTP API instead of the removed Processes MCP server.
---

# CanDoItAll Processes API

Use this skill when a task needs process runtime control through the CanDoItAll web API.

Processes are the durable orchestration layer, but the current HTTP surface is intentionally narrow. Source of truth: `src/App/CanDoItAll.Web/Api/ProcessesApi.cs`.

## Access

- Start the CanDoItAll web app and inspect Swagger/OpenAPI at `/swagger` or `/swagger/v1/swagger.json`.
- Check `GET /api/access/status` before assuming bearer tokens are required.
- If JWT is active, send `Authorization: Bearer <token>`.
- Do not reinstall or use `candoitall_processes`; that MCP server has been removed.
- Do not document older process authoring, artifact, assignment, escalation, direct-message, manager-directive, approval, analytics, or template routes as current HTTP commands unless `ProcessesApi.cs` reintroduces them.

## Current Commands

The current source-backed process API exposes these commands:

- `GET /api/processes/contract`
- `POST /api/processes/launch/check`
- `POST /api/processes/launch`
- `POST /api/processes/runs/{runId}/dispatch`
- `POST /api/processes/runs/{runId}/cancel`
- `POST /api/processes/runs/{runId}/steps/{stepInstanceId}/rework`
- `GET /api/processes/live`
- `GET /api/processes/runs/{runId}`
- `GET /api/processes/runs/{runId}/history`

Use `GET /api/processes/contract` first when building clients or smoke tests. It returns the current endpoint list and boundary summary from the running host.

Use `POST /api/processes/launch/check` for non-mutating launch preflight. It compiles the selected process template, resolves step executors, returns the launch plan and readiness findings, and does not create a process run.

## Launch

`POST /api/processes/launch/check`

`POST /api/processes/launch`

`ProcessLaunchApiRequest` fields:

- `definitionKey`
- `processDefinitionId`
- `liveRunProfileKey`
- `projectId`
- `projectNodeId`
- `requestedBy`
- `variables`
- `runReadiness`
- `execute`

Example:

```http
POST /api/processes/launch/check
Content-Type: application/json
```

```json
{
  "definitionKey": "business-plan-development",
  "projectId": "00000000-0000-0000-0000-000000000000",
  "projectNodeId": "node-id",
  "requestedBy": "api-client",
  "variables": {
    "Topic": "New offer"
  },
  "runReadiness": true,
  "execute": false
}
```

`launch/check` ignores `execute` and is the safe preflight path when the caller must not create a durable run.

`POST /api/processes/launch` always creates and schedules a durable run when readiness allows launch. `execute: false` avoids enqueuing immediate dispatcher execution; it is not a dry run. Use `execute: true` only when the caller intends to enqueue runtime execution immediately.

The launch response includes:

- `definitionId`
- `launchPlanId`
- `runId`
- `stage`
- `route`
- `launchPlan`
- `warnings`

## Dispatch And Operator Actions

Dispatch ready work:

```http
POST /api/processes/runs/{runId}/dispatch
Content-Type: application/json
```

```json
{
  "requestedBy": "api-client"
}
```

Cancel a run:

```http
POST /api/processes/runs/{runId}/cancel
Content-Type: application/json
```

```json
{
  "requestedBy": "api-client",
  "reason": "Operator requested process run cancellation."
}
```

Request rework for a step instance:

```http
POST /api/processes/runs/{runId}/steps/{stepInstanceId}/rework
Content-Type: application/json
```

```json
{
  "requestedBy": "api-client",
  "reason": "Operator requested process step rework."
}
```

The dispatch path runs through `ProcessRuntimeDispatchApplicationService` and `ProcessRuntimeEngine`. Cancel and rework run through `ProcessRuntimeOperatorApplicationService`.

## Readback

List live processes:

```http
GET /api/processes/live?take=50&windowMinutes=240
```

Query fields:

- `take`: clamped to 1..500, default `50`.
- `windowMinutes`: clamped to 1 minute..30 days, default `240`.

Read one run:

```http
GET /api/processes/runs/{runId}
```

Read recent run history:

```http
GET /api/processes/runs/{runId}/history?fromUtc=2026-06-25T00:00:00Z&toUtc=2026-06-25T12:00:00Z&take=100
```

History query fields:

- `fromUtc`: defaults to 24 hours before `toUtc`.
- `toUtc`: defaults to current UTC.
- `take`: clamped to 1..1000, default `100`.

Run and history responses expose projection freshness. Treat stale freshness or backlog as an operating signal, not as proof that a run is complete.

## Project-Structure Bridge

Node-bound process work is currently exposed through the Project Structure API and runtime tools:

- `POST /api/project-structure/projects/{projectId}/nodes/{nodeId}/process-definition`
- `POST /api/project-structure/projects/{projectId}/nodes/{nodeId}/process/start`
- `project_structure_node_process_definition_link`
- `project_structure_node_process_start`
- `project_structure_process_subprocess_launch`

Use the project-structure bridge when a process is linked to a project node or when governed subprocess launch is required inside process automation.

## Direct Tool Boundary

The current source tree does not contain `ProcessAgentRuntimeToolProvider`, and `AddProcessesModule` does not register direct `processes_*` runtime tools.

If a prompt or older doc asks for `processes_definition_save`, `processes_runs_list`, `processes_artifact_record`, `processes_step_transition`, or similar direct process tools, do not invent them. Use the HTTP commands in this skill or the project-structure bridge commands above, then record the missing direct tool as a hardening gap.

## Not Current HTTP Commands

These older route families are not currently mapped by `src/App/CanDoItAll.Web/Api/ProcessesApi.cs`:

- `/api/processes/definitions`
- `/api/processes/templates`
- `/api/processes/runs`
- `/api/processes/runs/start`
- `/api/processes/runs/stop`
- step transition or rerun-agent routes
- step-scoped artifacts or assignments
- direct messages
- manager directives
- escalations
- operator approvals
- analytics
- launch-plan HR/provisioning routes

Do not call those routes until they are reintroduced with typed handlers, OpenAPI visibility, tests, and refreshed skill documentation.

## Validation

- Before launch, call `POST /api/processes/launch/check` when you need readiness diagnostics without creating a run.
- After launch, read back `GET /api/processes/runs/{runId}` when `runId` is present.
- After dispatch, cancel, or rework, read the run and history routes.
- For node-bound process starts, validate both the project-structure operation result and the process run readback.
- For docs-only skill updates, run `git diff --check` and regenerate the source route appendix.
- For runtime behavior changes, add focused tests around `ProcessesApi`, `ProcessLaunchApplicationService`, `ProcessRuntimeDispatchApplicationService`, and `ProcessRuntimeOperatorApplicationService`.

## Source Route Appendix

<!-- api-docs-skills-parity:routes:start -->

Processes API route appendix. Generated from Minimal API registrations; refresh from `src/App/CanDoItAll.Web/Api/ProcessesApi.cs` when routes change.

| Method | Route |
| --- | --- |
| `GET` | `/api/processes/contract` |
| `POST` | `/api/processes/launch` |
| `POST` | `/api/processes/launch/check` |
| `GET` | `/api/processes/live` |
| `GET` | `/api/processes/runs/{runId:guid}` |
| `POST` | `/api/processes/runs/{runId:guid}/cancel` |
| `POST` | `/api/processes/runs/{runId:guid}/dispatch` |
| `GET` | `/api/processes/runs/{runId:guid}/history` |
| `POST` | `/api/processes/runs/{runId:guid}/steps/{stepInstanceId:guid}/rework` |

<!-- api-docs-skills-parity:routes:end -->
