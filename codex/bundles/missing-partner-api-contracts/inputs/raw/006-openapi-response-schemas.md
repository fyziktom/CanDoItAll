# 006 — Complete OpenAPI response schemas

Status: **missing for key endpoints in the pinned snapshot**
Priority: **high**

## Observed contract

The pinned OpenAPI documents request schemas such as `AgentEditorModel`,
`AgentExecutionRunStartApiRequest`, and `WorkflowRunStartApiRequest`, but important `200`
responses have no JSON schema for routes including:

- `GET /api/agents`;
- `GET /api/agents/providers`;
- `POST /api/agents`;
- `POST /api/agents/{agentId}/execution-runs`;
- `GET /api/agents/{agentId}/execution-runs/{executionRunId}`;
- `GET /api/workflows/definitions`;
- `POST /api/workflows/definitions/{workflowId}/runs/start`;
- `GET /api/workflows/runs/{runId}/detail`.

Partners cannot reliably generate typed clients from this contract and are forced to
inspect source or sample runtime payloads.

## Needed API documentation change

Annotate minimal API results with explicit success and error response types. Document:

- every success status and DTO;
- problem details/error envelope for `400`, `401`, `403`, `404`, `409`, `422`, and
  relevant `5xx`;
- numeric enum meaning or string-enum serialization policy;
- nullable and required members;
- pagination envelopes where used.

Prefer explicit public response DTOs over exposing persistence/domain types accidentally.

## Acceptance

OpenAPI client generation produces typed agent provider, agent save, execution result,
execution detail, workflow catalog, workflow start detail, and error models without
handwritten response definitions. Contract tests compare generated schemas with runtime
serialization.
