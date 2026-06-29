# Process Runtime Restoration Ledger

## Status

Historical release-candidate ledger. The older restoration effort has been superseded by the rebuilt `CanDoItAll.Processes.*` runtime and the current process module registration.

Use [Processes, MAF, and providers implementation map](processes-maf-providers-implementation-map.md) for current source-grounded architecture, active services, API routes, known gaps, and the next hardening-refactor roadmap.

## Current Runtime Position

Current launch and dispatch ownership is:

- `ProcessLaunchApplicationService`
- `ProcessRuntimeDispatchApplicationService`
- `ProcessRuntimeEngine`
- `ProcessRuntimeDispatchQueueWorker`
- `AgentFrameworkProcessExecutionAdapter`
- `ProjectStructureProcessNodeService`
- `ProcessesApi`

Current operator checks and readback are:

- `GET /api/processes/contract`
- `POST /api/processes/launch/check`
- `GET /api/processes/live`
- `GET /api/processes/runs/{runId}`
- `GET /api/processes/runs/{runId}/history`

Current process mutation routes are:

- `POST /api/processes/launch`
- `POST /api/processes/runs/{runId}/dispatch`
- `POST /api/processes/runs/{runId}/cancel`
- `POST /api/processes/runs/{runId}/steps/{stepInstanceId}/rework`

## Open Hardening Items

- Decide whether direct `processes_*` MAF runtime tools should be reintroduced or retired from remaining policy/test references.
- Harden dispatch queue durability and recovery beyond the current local in-memory queue plus EF runtime stores.
- Add source-backed API route snapshots for `/api/processes`.
- Add provider runtime handle invalidation proof after provider profile edits.
- Add operator readiness fields for projection lag, stale claims, active dispatches, and provider failure categories.

## Reopen Triggers

Reopen process runtime docs before release if any of these occur:

- A new process start path bypasses `ProcessLaunchApplicationService` or `ProjectStructureProcessNodeService`.
- Dispatch stops using `ProcessRuntimeDispatchApplicationService` and `ProcessRuntimeEngine`.
- `/api/processes` adds or removes routes.
- A concrete direct process runtime tool provider is introduced.
- Provider runtime dispatch, credential resolution, or failure classification changes.
