# 005 — Workflow start idempotency

Status: **missing in the pinned public start request**  
Priority: **high**

## Observed contract

`POST /api/workflows/definitions/{workflowId}/runs/start` accepts workflow ID, version ID,
`inputJson`, and requested backend. The internal launch is constructed with
`WorkflowLaunchIdempotency.NotRequested`; the public DTO has no idempotency field.

A partner cannot safely determine whether to retry after a timeout or dropped response.
For CRM operations, duplicate runs can duplicate downstream side effects.

## Needed API

Accept a tenant-scoped idempotency key through a standard header or request member:

```http
POST /api/workflows/definitions/{workflowId}/runs/start
Idempotency-Key: crm-lead-7350:qualification:v2
```

Return the original run for an identical replay. Return `409 Conflict` when the same key
is reused with a different workflow ID, version ID, backend, or canonical input hash.

Add a lookup:

```http
GET /api/workflows/runs/by-idempotency-key/{key}
```

## Required evidence

Persist and return:

- idempotency key or its safe hash;
- workflow and version IDs;
- canonical input hash;
- created-versus-replayed flag;
- original run ID and terminal state.

## Acceptance

Concurrent identical submissions and post-timeout retries result in one workflow run and
one set of side effects.
