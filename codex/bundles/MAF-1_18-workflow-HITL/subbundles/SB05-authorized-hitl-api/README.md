# SB05 — Authorized and Idempotent Workflow HITL API

## Status

Prepared

## Outcome

Complete the existing workflow pending-request/response API over the new service boundary with typed JSON, trusted actor identity, authorization, validation, idempotency, audit, stable outcomes, and operation status.

## Owned requirements

RQ-028, RQ-032 through RQ-040, and API/documentation portions of RQ-043.

## Non-goals

- adding a second approval execution API;
- workflow UI redesign;
- client polling UI;
- accepting actor/tenant/tool arguments from request body;
- exposing raw MAF request/checkpoint objects;
- weakening current authentication;
- making endpoint lambdas own domain transitions.

## Prerequisites

SB04 passed with persistent checkpoint/operation/recovery proof.

## Reopen triggers

- IK-13 authorization cannot be enforced at service layer;
- IK-14 old DTO compatibility is required;
- operation status cannot be represented coherently;
- API integration exposes checkpoint/secret data;
- request scope cannot be resolved from persisted run origin/project/tenant data.

## Exact sources and discovery

- `src/App/CanDoItAll.Web/Api/WorkflowsApi.cs`
- API group/auth conventions in `ApiEndpointRouteBuilderExtensions.cs`
- shared API error/problem result helpers;
- claims/service identity resolvers;
- project/workspace/tenant authorization services;
- `docs/api-control-plane.md`
- `WorkflowApiIntegrationTests`
- current run detail DTO and pending request records.

Map existing clients before removing the raw `ResponseJson` shape.

## Implementation boundary

### 1. Service boundary

Introduce/use `WorkflowExternalResponseService` that accepts:

- trusted actor context;
- request ID;
- expected request version;
- typed JSON response;
- idempotency key;
- correlation context.

It performs authorization, validation, operation create/replay/conflict, and runtime continuation. Endpoint code only binds/maps.

### 2. Typed payload

Prefer `JsonElement`, `JsonNode`, or a typed discriminated request DTO consistent with the repository.

Avoid a string containing encoded JSON.

If compatibility requires the old shape, version/deprecate it and convert once at the edge to the same internal command.

### 3. Authorization

Inject an authorizer that evaluates persisted:

- run origin/scope;
- workflow/project/workspace/tenant;
- request kind;
- intended approver/role/capability;
- actor identity.

Reject workflow/model self-approval. Ensure service/tool callers cannot bypass the same policy.

### 4. Validation

Enforce:

- content type/body size/depth;
- expected request version;
- approval schema or HumanInput schema;
- pending request/run relationship;
- checkpoint/resumability;
- payload policy/redaction.

### 5. Idempotency and status

Require or strongly support `Idempotency-Key` using existing API conventions.

Map:

- same key/payload replay;
- conflicting replay;
- active operation;
- completed/next wait;
- legacy/incompatible checkpoint;
- auth failures;
- retryable backend failure.

Expose operation state through existing run detail or a focused status endpoint when needed.

### 6. Audit

Persist actor, action, hashes, correlation, timestamps, outcome, and safe diagnostic. Do not log raw checkpoint JSON or secrets.

### 7. Contract/docs

Update endpoint contract list, OpenAPI metadata, response types, error types, and `docs/api-control-plane.md`.

## Acceptance criteria

- existing route remains the single response command route;
- typed JSON body is accepted without double encoding;
- actor comes from trusted principal/service context;
- unauthorized and self-approval requests fail before operation claim;
- request kind/schema/version/size are validated;
- same key/payload returns stable replay result;
- same key/different payload returns conflict;
- completed, resuming, waiting-again, denied, cancelled, retryable failure, terminal recovery failure are distinguishable;
- run detail/status exposes the next pending request when present;
- API never returns raw checkpoint JSON, credentials, or unrestricted governed arguments;
- service/tool caller paths use the same authorizer and operation service;
- integration tests cover status and authorization matrix;
- docs match runtime.

## Proof tier

Governed

## Focused validation

Extend/run:

- `WorkflowApiIntegrationTests`
- focused service authorization tests;
- response validator tests;
- audit/redaction tests;
- workflow lifecycle tests affected by status mapping.

Required HTTP matrix:

- approve success;
- deny success;
- HumanInput success;
- consecutive wait;
- anonymous;
- wrong scope;
- self-approval;
- invalid schema;
- stale request version;
- idempotent replay;
- conflicting replay;
- active operation;
- cancelled request;
- missing request;
- legacy/missing/topology mismatch;
- retryable backend unavailable.

Record exact discovered counts and sanitized request/response examples under `proof/SB05`.

## Invalidation keys

IK-13, IK-14, IK-16, IK-17, IK-18.

## Broad-gate decision

Do not run FG-01 until SB05 implementation, migrations, generated API artifacts, and focused tests are frozen. At SB05 closure declare the exact freeze commit/diff state.

## Closure record

Not executed.

Record:

- service/authorizer:
- wire DTO:
- compatibility decision:
- idempotency/status:
- audit/redaction:
- API matrix:
- docs:
- freeze declaration:
- tests/counts:
- blockers/deviations:
