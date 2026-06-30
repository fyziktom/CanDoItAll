# Process Agent Operator Runbook

## Scope

This runbook covers current process runs that launch through `ProcessLaunchApplicationService`, dispatch through `ProcessRuntimeDispatchApplicationService`, and execute AgentFramework-backed steps through the module adapter in `CanDoItAll.Modules.Processes`.

For the source-level map, read [Processes, MAF, and providers implementation map](processes-maf-providers-implementation-map.md).

## Current Runtime Status

The current process runtime is source-backed by:

- `src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`
- `src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs`
- `src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.cs`
- `src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRuntimeDispatchQueueServices.cs`
- `src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRuntimeIntegrationServices.cs`
- `src/App/CanDoItAll.Web/Api/ProcessesApi.cs`

Do not use older docs that reference `ProcessesService`, `ProcessRunAutomationDispatchService`, `ProcessOutboxService`, `ProcessRunRecoveryWorker`, or a process-driver runtime host as current runtime entry points. Those names are historical or roadmap context unless source files are reintroduced.

## Triage Order

1. Read the live projection with `GET /api/processes/live`.
2. Read the target run with `GET /api/processes/runs/{runId}`.
3. Read timeline history with `GET /api/processes/runs/{runId}/history`.
4. If ready work is stuck, run `POST /api/processes/runs/{runId}/dispatch` once and read the run again.
5. If the run must stop, use `POST /api/processes/runs/{runId}/cancel` with a concrete reason.
6. If a step needs correction, use `POST /api/processes/runs/{runId}/steps/{stepInstanceId}/rework` with the smallest actionable rework reason.
7. If the failure mentions provider quota, billing, rate limit, or disabled provider state, inspect the agent/provider profile before retrying.

## Current Process API

The active process API routes are:

| Method | Route | Operator use |
| --- | --- | --- |
| `GET` | `/api/processes/contract` | Confirm current route list and boundary note. |
| `POST` | `/api/processes/launch/check` | Preflight process template, executor resolution, and readiness without creating a run. |
| `POST` | `/api/processes/launch` | Launch a process from definition key or definition id. |
| `POST` | `/api/processes/runs/{runId}/dispatch` | Execute ready work for a run. |
| `POST` | `/api/processes/runs/{runId}/cancel` | Request cancellation. |
| `POST` | `/api/processes/runs/{runId}/steps/{stepInstanceId}/rework` | Request step rework. |
| `GET` | `/api/processes/live` | Read live process snapshots. |
| `GET` | `/api/processes/runs/{runId}` | Read run detail projection. |
| `GET` | `/api/processes/runs/{runId}/history` | Read timeline history projection. |

Older routes for assignments, artifacts, escalations, direct messages, approvals, manager directives, template import/export, and analytics are not currently mapped in `ProcessesApi.cs`. Do not operate them through undocumented endpoints.

Use `launch/check` for dry-run validation. `POST /api/processes/launch` creates and schedules a durable run when readiness allows launch; `execute: false` only avoids immediate dispatch queueing.

## Project-Structure Launches

Project-structure process starts use `ProjectStructureProcessNodeService`.

- `project_structure_node_process_definition_link` links a project-structure node to a process definition.
- `project_structure_node_process_start` starts or prepares the process linked to a project-structure node.
- `project_structure_process_subprocess_launch` starts a child process from inside governed process automation and requires the parent step to allow `ExecuteExternalAction`.

When triaging project-structure launches, preserve project id, node id, linked process definition id, process run node id, and launch variables. For subprocesses, verify parent run id, parent step id, parent assignment, operation contract, and inherited project scope.

## Provider Failures

Provider failures are normalized by `AgentProviderFailureDisplayFormatter`.

- Quota or billing failures usually require adding provider credits/billing or switching the agent to a provider with available quota.
- Rate-limit failures usually require waiting for reset or reducing concurrency.
- General provider failures require checking provider profile state, credential resolution, model selection, transport metadata, and health check result.

Do not paste provider keys into logs, docs, screenshots, or operator notes. Configure provider credentials through environment variables or the runtime secret mechanism documented in [Secure configuration](secure-configuration.md).

## Rework Guidance

Use rework only when the current step has a specific correction. The reason should say what to fix and cite current run evidence. Do not use rework to restart unrelated steps or hide provider/configuration failures.

If a step is blocked because an upstream artifact or subprocess is missing, fix the upstream cause first. Current dispatch logic can release/rework parent claims after terminal child runs, but it cannot invent missing external evidence.

## Cancellation Guidance

Cancellation should include a concrete reason. After cancellation, read live/detail/history projections again to confirm state and projection freshness.

## Validation Gates

After changing process operator behavior, prefer focused tests first:

```powershell
dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~ProcessRuntimeDispatchApplicationServiceTests"
dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --filter "FullyQualifiedName~ProjectStructureAgentIntegrationTests"
```
For documentation-only changes, run:

```powershell
git diff --check
```
