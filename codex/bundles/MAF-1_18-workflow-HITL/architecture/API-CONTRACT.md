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
    "message": "Reviewed and approved."
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

Add the focused authorized read endpoint:

- `GET /api/workflows/external-response-operations/{operationId}`

It is a read model over the existing operation/request/run relationship, not a second
mutation API. Return only safe state/outcome/timestamps, run state, safe message, and a
safe next-pending-request projection.

## Authorization

At minimum, authorization input must include:

- authenticated principal/service identity;
- canonical database profile and server-owned workspace scope;
- request ID;
- run origin;
- workflow ID/version;
- project/tenant/workspace scope where present;
- request kind;
- required approval policy/capabilities;
- assignment or intended approver metadata where present.

Authorization is enforced in `WorkflowExternalResponseService` or an injected authorizer, not solely through endpoint group policy.

Persist the server-owned target profile/scope additively through existing run `OriginJson`
and request-boundary `AuthorizationPolicyJson`. Cross-profile access is denied. Agents
require their admitted governance scope; organization-scoped human/API authority may
cover a server-verified narrower run only within the same profile. Missing or legacy scope
fails closed. This contract does not invent a per-user project ACL that the repository
does not implement.

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
- persisted request belongs to the current run and is pending-compatible;
- checkpoint linkage is present for native resumable runs;
- redaction/payload policy succeeds.

Do not reject a same-key replay solely because the request has already advanced to
`ResponseClaimed`, `Responded`, or `Denied`; first let the durable operation store resolve
the existing same-fingerprint operation.

## Recovery and persistence decision

SB05 adds no relational table, column, model-snapshot edit, or EF migration. Scope/policy
uses the existing JSON snapshots. Recovery reconstructs and revalidates authorization
from the durable operation plus request boundary. Approval action is derived only from the
validated protected response (`approved=true`/`false`); HumanInput derives
`submit-input`. No caller supplies an action or authorization grant. Missing or corrupt
legacy authorization material fails closed.

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

Public run, request, event, artifact, checkpoint, response, and operation projections must
also exclude `RequestJson`, `ResponseJson`, raw event `PayloadJson`, native checkpoint IDs,
payload references/hashes, artifact storage paths, protected keys, and internal policy.

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

## Compatibility decision

Repository client inventory found no consumer that requires the old raw `ResponseJson`
HTTP shape. Remove it rather than versioning it. Discovery of a concrete dependent client
triggers IK-14 and stops implementation for an explicit compatibility decision; it does
not authorize silently adding a second wire contract.
