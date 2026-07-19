---
name: candoitall-api-workflows
description: Use when managing CanDoItAll workflow settings, definitions, lifecycle, components, test runs, runtime runs, external requests, artifacts, events, executor catalog, and analytics through the HTTP API.
---

# CanDoItAll Workflows API

Use this skill when a task needs workflow authoring, lifecycle control, runtime observation, human/external request response, or workflow analytics through the CanDoItAll web API.

## Access

- Start the CanDoItAll web app and inspect Swagger/OpenAPI at `/swagger` or `/openapi/v1.json`.
- Check `/api/access/status` before assuming bearer tokens are required.
- If JWT is active, create a token from Settings -> API Access or `POST /api/access/tokens`, then send `Authorization: Bearer <token>`.
- Do not add or reinstall a workflow-specific MCP server; workflow control is through the HTTP API.

## Definition And Authoring Work

- Contract discovery: `GET /api/workflows/contract`.
- Settings: `GET /api/workflows/settings`, `POST /api/workflows/settings`.
- Runtime and executor catalogs: `GET /api/workflows/runtime-backends`, `GET /api/workflows/executor-catalog`.
- Definitions: `GET /api/workflows/definitions`, `GET /api/workflows/definitions/{workflowId}`, `GET /api/workflows/definitions/{workflowId}/versions/{versionId}`, `POST /api/workflows/definitions`, `DELETE /api/workflows/definitions/{workflowId}`.
- Lifecycle: `POST /api/workflows/definitions/{workflowId}/publish`, `/suspend`, and `/archive`; pass `expectedVersionId` when coordinating concurrent edits.
- Import/export: `GET /api/workflows/definitions/{workflowId}/export`, `POST /api/workflows/definitions/import`.
- Validation: `POST /api/workflows/definitions/{workflowId}/validate` for saved definitions and `POST /api/workflows/validate` for drafts.
- LLM execution bindings and providers: `GET /api/workflows/provider-options`, `GET /api/workflows/components`, `GET /api/workflows/components/{componentId}`, `POST /api/workflows/components`, `DELETE /api/workflows/components/{componentId}`. Reusable prompt content is canonical in `/api/prompt-gallery`; workflow components retain provider/model/runtime settings plus an immutable Gallery item/version reference and prompt snapshot.

## Runtime Work

- Test runs: `POST /api/workflows/test-runs`.
- Start runs: `POST /api/workflows/runs/start` or `POST /api/workflows/definitions/{workflowId}/runs/start`.
- Observe runs: `GET /api/workflows/runs`, `GET /api/workflows/runs/page`, `GET /api/workflows/runs/{runId}`, `GET /api/workflows/runs/{runId}/detail`.
- Cancel runs: `POST /api/workflows/runs/{runId}/cancel`.
- Events, checkpoints, and artifacts: `GET /api/workflows/runs/{runId}/events`, `GET /api/workflows/runs/{runId}/events/page`, `GET /api/workflows/runs/{runId}/checkpoints`, and `GET /api/workflows/runs/{runId}/artifacts`.
- Artifact content: `GET /api/workflows/runs/{runId}/artifacts/{artifactId}/content`.
- Human or external input: `GET /api/workflows/runs/{runId}/pending-requests`, `POST /api/workflows/external-requests/{requestId}/response`.
- Analytics: `GET /api/workflows/analytics`.

## Runtime DTOs

`WorkflowRunStartApiRequest` fields:

- `workflowId`
- `versionId`
- `inputJson`
- `requestedBackend`
- `sourceProcessRunId`
- `sourceProcessAssignmentId`

Use `sourceProcessRunId` and `sourceProcessAssignmentId` when the workflow run is part of a governed process step.

`WorkflowRunListApiQuery` fields:

- `workflowId`
- `state`
- `backend`
- `search`
- `take`
- `pageIndex`
- `pageSize`

`WorkflowEventListApiQuery` fields:

- `pageIndex`
- `pageSize`

`WorkflowAnalyticsApiQuery` fields:

- `workflowId`
- `state`
- `backend`
- `search`
- `take`

## Operating Rules

- Validate a draft or saved definition before publishing or running it.
- Read `GET /api/workflows/contract` before building clients or smoke tests; use OpenAPI for full schema detail.
- Prefer explicit lifecycle endpoints over resubmitting a full definition only to change status.
- Use import/export envelopes for portable workflow definition movement; do not hand-copy internal persistence records.
- Search and version reusable instructions through the Prompt Gallery API. Treat workflow component endpoints as execution-binding compatibility endpoints, not a second prompt library.
- Use `expectedVersionId` for lifecycle commands when another agent or UI may be editing the same definition.
- For long or active runs, prefer paged run and event routes before fetching full run detail.
- After responding to a pending external request, read back `/runs/{runId}/detail` and `/events` to confirm the state transition.
- Treat `DurableTask` and `AzureFunctions` backends as configured capabilities; do not silently fall back to `InProcess` when a requested backend is missing.

## Executor Side-Effect Contracts

- Executor catalog entries expose `WorkflowExecutorSideEffectDescriptor`. Treat `None`, external read, external write, and idempotent processed-marker contracts as workflow governance data, not UI hints.
- Email mark-processed executors must distinguish preview from commit with `sideEffectMode`, `dryRun`, `committed`, `idempotencyRecord`, `processedMarker`, and `externalSideEffectReceipt`.
- Do not retry an external-write executor unless its side-effect contract is idempotent retry safe. Preserve `idempotencyKey` and provider-scoped key prefixes when reviewing workflow output or scheduler replay behavior.
- For governed process workflow runs, preserve `sourceProcessRunId` and `sourceProcessAssignmentId` so workflow artifacts and side-effect receipts remain tied to the owning process run.

## Validation

- Use Swagger/OpenAPI to confirm route shape before writing client code.
- Use `GET /api/workflows/contract` as the quick route and boundary check for operator automation.
- After saving, importing, or changing lifecycle status, read back the specific definition id and version id.
- After starting, cancelling, or responding to a run, read back the run detail plus events.
- For artifacts, verify both the artifact metadata and the referenced storage path when content matters.

## Source Route Appendix

<!-- api-docs-skills-parity:routes:start -->

Workflows API route appendix. Generated from Minimal API registrations; refresh from `src/App/CanDoItAll.Web/Api/WorkflowsApi.cs` when routes change.

| Method | Route |
| --- | --- |
| `GET` | `/api/workflows/analytics` |
| `GET` | `/api/workflows/components` |
| `POST` | `/api/workflows/components` |
| `DELETE` | `/api/workflows/components/{componentId:guid}` |
| `GET` | `/api/workflows/components/{componentId:guid}` |
| `GET` | `/api/workflows/contract` |
| `GET` | `/api/workflows/definitions` |
| `POST` | `/api/workflows/definitions` |
| `DELETE` | `/api/workflows/definitions/{workflowId:guid}` |
| `GET` | `/api/workflows/definitions/{workflowId:guid}` |
| `POST` | `/api/workflows/definitions/{workflowId:guid}/archive` |
| `GET` | `/api/workflows/definitions/{workflowId:guid}/export` |
| `POST` | `/api/workflows/definitions/{workflowId:guid}/publish` |
| `POST` | `/api/workflows/definitions/{workflowId:guid}/runs/start` |
| `POST` | `/api/workflows/definitions/{workflowId:guid}/suspend` |
| `POST` | `/api/workflows/definitions/{workflowId:guid}/validate` |
| `GET` | `/api/workflows/definitions/{workflowId:guid}/versions/{versionId:guid}` |
| `POST` | `/api/workflows/definitions/import` |
| `GET` | `/api/workflows/executor-catalog` |
| `POST` | `/api/workflows/external-requests/{requestId:guid}/response` |
| `GET` | `/api/workflows/provider-options` |
| `GET` | `/api/workflows/runs` |
| `GET` | `/api/workflows/runs/{runId:guid}` |
| `GET` | `/api/workflows/runs/{runId:guid}/artifacts` |
| `GET` | `/api/workflows/runs/{runId:guid}/artifacts/{artifactId:guid}/content` |
| `POST` | `/api/workflows/runs/{runId:guid}/cancel` |
| `GET` | `/api/workflows/runs/{runId:guid}/detail` |
| `GET` | `/api/workflows/runs/{runId:guid}/events` |
| `GET` | `/api/workflows/runs/{runId:guid}/events/page` |
| `GET` | `/api/workflows/runs/{runId:guid}/checkpoints` |
| `GET` | `/api/workflows/runs/{runId:guid}/pending-requests` |
| `GET` | `/api/workflows/runs/page` |
| `POST` | `/api/workflows/runs/start` |
| `GET` | `/api/workflows/runtime-backends` |
| `GET` | `/api/workflows/settings` |
| `POST` | `/api/workflows/settings` |
| `POST` | `/api/workflows/test-runs` |
| `POST` | `/api/workflows/validate` |

<!-- api-docs-skills-parity:routes:end -->
