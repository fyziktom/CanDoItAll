# Workflow HITL API Contract

## Existing routes to retain

- `GET /api/workflows/runs/{runId}/pending-requests`
- `POST /api/workflows/external-requests/{requestId}/response`

Do not create `/approvals` as a second competing execution API unless it is only a read-model alias with the same service boundary and the repository's API convention requires it.

## Response request

Recommended wire shape:

```json
{
  "expectedRequestVersion": 3,
  "response": {
    "approved": true,
    "comment": "Reviewed and approved."
  }
}
```

For HumanInput, `response` follows the request's declared schema.

HTTP header:

```text
Idempotency-Key: caller-stable-key
```

Rules:

- `response` is a JSON value, not a JSON string containing encoded JSON;
- reject unknown top-level members if that is the current API convention;
- set a bounded request-body size;
- do not accept actor, tenant, project, run, port, tool arguments, or required capabilities from the body;
- use the route request ID and persisted request data.

## Recommended response representation

```json
{
  "outcome": "Accepted",
  "operationId": "guid",
  "operationState": "Completed",
  "replayed": false,
  "run": {},
  "request": {},
  "nextPendingRequest": null,
  "message": "Workflow external response was accepted and the run completed."
}
```

Long-running continuation may return `202 Accepted` with operation status. Synchronous completion may return `200 OK`. Choose one coherent repository convention and document it.

## Status query

Add or expose through existing detail:

- `GET /api/workflows/external-requests/{requestId}`
- `GET /api/workflows/external-response-operations/{operationId}`

A separate endpoint is required only when existing run detail cannot provide stable operation polling. Reuse existing run detail where sufficient.

## Authorization

At minimum, authorization input must include:

- authenticated principal/service identity;
- request ID;
- run origin;
- workflow ID/version;
- project/tenant/workspace scope where present;
- request kind;
- required approval policy/capabilities;
- assignment or intended approver metadata where present.

Authorization is enforced in `WorkflowExternalResponseService` or an injected authorizer, not solely through endpoint group policy.

Required negative cases:

- anonymous;
- authenticated but wrong project/tenant;
- actor not assigned/allowed for approval;
- service identity lacking approval capability;
- workflow/model identity attempting self-approval;
- stale or superseded request.

## Validation

Validate before operation claim:

- response JSON is syntactically valid;
- request kind matches the response contract;
- approval response contains a boolean decision;
- HumanInput response matches stored schema;
- payload is within size/depth limits;
- expected request version matches;
- persisted request belongs to the current run and is pending;
- checkpoint linkage is present for native resumable runs;
- redaction/payload policy succeeds.

## Audit

Persist safe audit information:

- actor kind and stable ID;
- request/run/workflow IDs;
- operation ID;
- action (`approve`, `deny`, `submit-input`);
- payload hash and schema version;
- idempotency key hash;
- correlation/trace ID;
- timestamps and attempts;
- outcome and safe diagnostic;
- next run/request/checkpoint references.

Do not log raw secrets, full tool arguments, credentials, unrestricted user input, or checkpoint JSON.

## Status mapping

Recommended mapping; adapt to repository conventions without losing typed outcomes:

| Condition | HTTP |
|---|---:|
| Accepted and completed/waiting again | 200 |
| Accepted and still resuming | 202 |
| Invalid JSON/schema/size | 400 |
| Unauthenticated | 401 |
| Unauthorized | 403 |
| Request/run not found | 404 |
| Same idempotency key with different payload | 409 |
| Already answered, run not waiting, active conflicting operation, stale version | 409 |
| Cancelled/superseded request when repository uses Gone semantics | 410 |
| Legacy non-resumable, missing checkpoint, topology mismatch | 422 |
| Backend/lease/storage temporarily unavailable | 503 |
| Unexpected internal failure | 500 with safe problem details |

Do not use 502 for deterministic local resume failure unless an actual upstream gateway dependency justifies it.

## Compatibility

During migration, accept the old raw `ResponseJson` shape only when API compatibility requirements demand it. If retained:

- isolate it in a versioned compatibility DTO;
- parse it once at the edge;
- convert to the typed internal JSON value;
- mark it deprecated in the contract;
- do not let both shapes create different service behavior.
