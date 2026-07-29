# Process Agent Operator Runbook

This runbook covers the current PostgreSQL-backed process runtime, local dispatch queue, AgentFramework execution adapter, projections, and durable run records.

## Source Of Truth

- [`ProcessLaunchApplicationService.cs`](../src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs)
- [`ProcessRuntimeDispatchApplicationService.cs`](../src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs)
- [`ProcessRuntimeEngine.cs`](../src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.cs)
- [`ProcessRuntimeDispatchQueue.cs`](../src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRuntimeDispatchQueue.cs)
- [`ProcessRuntimeDispatchQueueServices.cs`](../src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRuntimeDispatchQueueServices.cs)
- [`AgentFrameworkProcessExecutionAdapter.cs`](../src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs)
- [`ProcessesApi.cs`](../src/App/CanDoItAll.Web/Api/ProcessesApi.cs)
- [`ProcessRunRecordsApi.cs`](../src/App/CanDoItAll.Web/Api/ProcessRunRecordsApi.cs)

## Triage Order

1. Confirm runtime readiness with `GET /_dev/runtime` and API state with `GET /api/access/status`.
2. Read `GET /api/processes/live`.
3. If the run id is unknown, search `GET /api/processes/runs` with the narrowest project, definition, participant, disposition, or time filter.
4. Read `GET /api/processes/runs/{runId}` for current projection state.
5. Read `GET /api/processes/runs/{runId}/summary` for durable facts, completeness warnings, costs, and manager narrative status.
6. Use `GET /api/processes/runs/{runId}/graph` or `/history` only when topology or event sequence is needed.
7. If ready work is not progressing, dispatch once, then repeat the reads before dispatching again.
8. Cancel or request rework only with a concrete, evidence-backed reason.

## Route Contract

`GET /api/processes/contract` returns this source-backed set:

| Method | Route | Operator use |
| --- | --- | --- |
| `GET` | `/api/processes/contract` | Discover the route contract. |
| `POST` | `/api/processes/launch/check` | Validate launch readiness without creating a run. |
| `POST` | `/api/processes/launch` | Create a durable run and optionally queue it. |
| `POST` | `/api/processes/runs/{runId}/dispatch` | Execute ready work once. |
| `POST` | `/api/processes/runs/{runId}/cancel` | Request cancellation. |
| `POST` | `/api/processes/runs/{runId}/steps/{stepInstanceId}/rework` | Request focused step rework. |
| `GET` | `/api/processes/live` | Read live projections. |
| `GET` | `/api/processes/runs` | Search durable run records with cursor paging. |
| `GET` | `/api/processes/runs/analytics` | Aggregate run-record metrics; the default window is 30 days. |
| `GET` | `/api/processes/runs/{runId}` | Read current run detail projection. |
| `GET` | `/api/processes/runs/{runId}/summary` | Read paged durable facts and bounded narrative. |
| `GET` | `/api/processes/runs/{runId}/graph` | Read a paged run graph. |
| `GET` | `/api/processes/runs/{runId}/history` | Read timeline history. |

Use OpenAPI for query and body schemas. `launch/check` is the dry run. A successful `launch` persists a run; `execute: false` only prevents immediate queueing.

## Dispatch And Run-Record Defaults

The dispatch queue is bounded and process-local. PostgreSQL holds the run state; recovery scans repopulate local work after interruption. Defaults come from [`ProcessRuntimeDispatchQueueOptions.cs`](../src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRuntimeDispatchQueueOptions.cs).

| Configuration key | Default |
| --- | --- |
| `Processes:RuntimeDispatchQueue:EnableRecovery` | `true` |
| `Processes:RuntimeDispatchQueue:ImmediateQueueCapacity` | `4096` |
| `Processes:RuntimeDispatchQueue:RecoveryQueueCapacity` | `4096` |
| `Processes:RuntimeDispatchQueue:ActiveClaimWithoutExecutionRunStaleAfter` | `00:02:00` |

The worker allows up to two immediate and two recovery dispatches concurrently, performs recovery discovery every 15 seconds, and deduplicates/defer-retries a run id. These worker timings are implementation constants, not configuration settings.

Durable fact aggregation and manager narratives use [`ProcessRunRecordProcessingOptions.cs`](../src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRunRecordProcessingOptions.cs).

| Configuration key | Default |
| --- | --- |
| `Processes:RunRecords:Enabled` | `true` |
| `Processes:RunRecords:BatchSize` | `8` |
| `Processes:RunRecords:MaximumAttempts` | `5` |
| `Processes:RunRecords:LeaseDuration` | `00:10:00` |
| `Processes:RunRecords:RetryBaseDelay` | `00:00:30` |
| `Processes:RunRecords:RetryMaximumDelay` | `00:30:00` |
| `Processes:RunRecords:PollInterval` | `00:00:02` |

The run-record worker is hosted only on a runtime lane that permits background workers. A missing or pending narrative does not invalidate already-available hard facts; inspect status, attempts, next retry, completeness, and warnings.

## Launch And Project Structure

Project Structure starts are handled by `ProjectStructureProcessNodeService`.

- `project_structure_node_process_definition_link` associates a node with a definition.
- `project_structure_node_process_start` prepares or launches the associated definition.
- `project_structure_process_subprocess_launch` launches a child from governed automation and requires the parent operation contract to allow external action.

Preserve project id, node id, definition id, run id, launch variables, and the parent run/step relationship in operator evidence.

## Dispatch

Use manual dispatch only after confirming that the run has ready work and the background worker is not already progressing it. The queue prevents concurrent local dispatch of the same run and delays retry after unexpected dispatch failure. Repeated manual calls are not a substitute for repairing a provider, capability, database, or worker fault.

After dispatch, read current detail, summary, and history again. Projection readback is the evidence that the operation completed.

## Provider Failures

Provider failures are normalized into quota/billing, rate-limit, and general provider categories.

- Quota or billing: repair provider billing or select an available provider.
- Rate limit: wait for reset or reduce concurrency.
- General failure: check enablement, credential resolution, model, transport, base URL, timeout, and provider health.

Never paste credentials into logs, screenshots, documentation, or rework reasons. Use environment variables or the runtime secret store described in [Secure configuration](secure-configuration.md).

## Rework And Cancellation

Rework must name the specific correction and cite current evidence. Do not use it to conceal provider/configuration failures or restart unrelated steps.

Cancellation must include a concrete reason. Read live, detail, summary, and history after the request to confirm terminal state and projection freshness.

If a step waits on a subprocess, inspect the child run first. Recovery can release or rework parent claims after a child stops, but it cannot synthesize missing external evidence.

## Validation

For process behavior changes:

```powershell
dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~ProcessRuntimeDispatchApplicationServiceTests"
dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --filter "FullyQualifiedName~ProjectStructureAgentIntegrationTests"
```

Then run the stable gate in [Testing](testing.md).
