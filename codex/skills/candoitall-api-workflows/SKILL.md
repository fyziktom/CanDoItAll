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

- Settings: `GET /api/workflows/settings`, `POST /api/workflows/settings`.
- Runtime and executor catalogs: `GET /api/workflows/runtime-backends`, `GET /api/workflows/executor-catalog`.
- Definitions: `GET /api/workflows/definitions`, `GET /api/workflows/definitions/{workflowId}`, `GET /api/workflows/definitions/{workflowId}/versions/{versionId}`, `POST /api/workflows/definitions`, `DELETE /api/workflows/definitions/{workflowId}`.
- Lifecycle: `POST /api/workflows/definitions/{workflowId}/publish`, `/suspend`, and `/archive`; pass `expectedVersionId` when coordinating concurrent edits.
- Import/export: `GET /api/workflows/definitions/{workflowId}/export`, `POST /api/workflows/definitions/import`.
- Validation: `POST /api/workflows/definitions/{workflowId}/validate` for saved definitions and `POST /api/workflows/validate` for drafts.
- LLM components and providers: `GET /api/workflows/provider-options`, `GET /api/workflows/components`, `GET /api/workflows/components/{componentId}`, `POST /api/workflows/components`, `DELETE /api/workflows/components/{componentId}`.

## Runtime Work

- Test runs: `POST /api/workflows/test-runs`.
- Start runs: `POST /api/workflows/runs/start` or `POST /api/workflows/definitions/{workflowId}/runs/start`.
- Observe runs: `GET /api/workflows/runs`, `GET /api/workflows/runs/page`, `GET /api/workflows/runs/{runId}`, `GET /api/workflows/runs/{runId}/detail`.
- Cancel runs: `POST /api/workflows/runs/{runId}/cancel`.
- Events and artifacts: `GET /api/workflows/runs/{runId}/events`, `GET /api/workflows/runs/{runId}/events/page`, `GET /api/workflows/runs/{runId}/artifacts`.
- Human or external input: `GET /api/workflows/runs/{runId}/pending-requests`, `POST /api/workflows/external-requests/{requestId}/response`.
- Analytics: `GET /api/workflows/analytics`.

## Operating Rules

- Validate a draft or saved definition before publishing or running it.
- Prefer explicit lifecycle endpoints over resubmitting a full definition only to change status.
- Use import/export envelopes for portable workflow definition movement; do not hand-copy internal persistence records.
- Use `expectedVersionId` for lifecycle commands when another agent or UI may be editing the same definition.
- For long or active runs, prefer paged run and event routes before fetching full run detail.
- After responding to a pending external request, read back `/runs/{runId}/detail` and `/events` to confirm the state transition.
- Treat `DurableTask` and `AzureFunctions` backends as configured capabilities; do not silently fall back to `InProcess` when a requested backend is missing.

## Validation

- Use Swagger/OpenAPI to confirm route shape before writing client code.
- After saving, importing, or changing lifecycle status, read back the specific definition id and version id.
- After starting, cancelling, or responding to a run, read back the run detail plus events.
- For artifacts, verify both the artifact metadata and the referenced storage path when content matters.
