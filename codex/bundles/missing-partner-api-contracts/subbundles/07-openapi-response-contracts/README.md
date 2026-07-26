# OpenAPI Response Contracts

## Status

- `Completed`

## Objective

- Close N006 and document every new route with explicit runtime-matching response/error
  schemas suitable for generated clients.

## Success Criteria

- Named agent/workflow endpoints publish typed success responses.
- Relevant 400/401/403/404/409/422/5xx responses publish the actual shared error/Problem
  Details shape.
- Generated schemas match runtime required/nullability/enum serialization.

## Covered Inputs

- N006 / R006 and response surfaces from N001-N005/N007.

## Prerequisites

- SB01-SB06 closed and final DTO shapes stable.

## Exact Source References

- `C:\repositories\CanDoItAll\src\App\CanDoItAll.Web\Api\AgentsApi.cs`
- `C:\repositories\CanDoItAll\src\App\CanDoItAll.Web\Api\WorkflowsApi.cs`
- `C:\repositories\CanDoItAll\src\App\CanDoItAll.Web\Api`
- `C:\repositories\CanDoItAll\src\App\CanDoItAll.Web\Api\ApiEndpointResults.cs`
- `C:\repositories\CanDoItAll\src\App\CanDoItAll.Web\Api\ApiServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\tests\Integration\CanDoItAll.Tests.Integration`

## Deliverables

- Explicit response DTOs and Minimal API metadata/typed results.
- Contract tests for required named operations and response schemas.
- Representative runtime serialization parity tests.

## Dependency Impact

- Sole source for SB08 snapshot/provenance and API-skill updates.

## Validation Depth

- Proof tier: `Behavioral`.

## Implementation Steps

1. Enumerate named operations and actual runtime result types.
2. Replace accidental domain/persistence exposure with explicit public response DTOs.
3. Add success/error metadata without changing established runtime semantics accidentally.
4. Assert OpenAPI `$ref`/required/nullability/enum shapes.
5. Deserialize representative runtime responses using generated-contract shapes.

## Scope Exceptions

- Full unrelated route-family response-schema completion is not implied beyond named and
  newly touched endpoints.

## Do Not Do

- Do not satisfy tests with dummy schema types that runtime never returns.
- Do not globally change enum serialization and break existing clients unless explicitly
  versioned and proven.

## Acceptance Checklist

- [x] all input-named operations have typed success schemas
- [x] new operations have relevant error schemas
- [x] runtime serialization parity passes
- [x] portable structured-output schema contains no `.NET Type`
- [x] generated client needs no handwritten response types for owned routes

## Proof Required

- Focused OpenAPI integration tests and Web build.
- Host-generated OpenAPI inspection before SB08.

## Browser Validation Logging

- N/A.

## C# Architecture Impact

### Boundary Ownership

- Web owns public response DTOs; domain owners retain internal models.

### Dependency Direction

- Mapping flows domain -> Web response only.

### Pattern Decision

- Transport DTO adapter.

### Testability Contract

- OpenAPI and real payloads are compared in integration tests.

### Partial Class Policy

- No endpoint-file growth beyond cohesive route families; split new route family.

### Architecture Proof Required

- Source/runtime assertion that DTOs are real endpoint returns.

## Progression Gate

- OpenAPI tests and canonical host document pass, unlocking SB08.

## Reopen Triggers

- Any earlier contract changes, missing schema, or runtime/schema mismatch.

## Implementation Evidence

- Explicit success metadata now covers the eight input-named operations and the unscoped
  portable execution route.
- Every dedicated SB01-SB06 operation publishes its real success/error response schema,
  including 401/403 where API authorization applies.
- JWT challenge/forbid responses now serialize the same `ApiErrorResponse` documented in
  OpenAPI.
- Workflow stable-identity 400 metadata now matches its actual shared error envelope.
- Workflow-start validation diagnostics now use one typed `ApiErrorResponse` shape rather
  than an undocumented alternate response.
- Agent save validation is mapped to a typed 400, and missing scoped execution details are
  mapped to a typed 404.

## Validation Evidence

- Final Web build: 0 errors; only the recorded 125 baseline NU1903 warnings.
- `ApiResponseContractIntegrationTests`: 4/4 passed over 22 unique operations. The slice
  proves success and declared error schema references, required/nullability rules,
  string-enum values, portable execution requests/results without runtime `System.Type`
  or `AgentStructuredOutputContract`, representative agents/providers/workflows/stable
  resolution/error payload parity, and an auth-enabled 401 JSON error envelope.
- One prior combined run hit the known transient PostgreSQL lease-cleanup `42501` after
  assertions passed; the immediate clean retry passed 4/4.
- Final scoped CodeAnalytics snapshot:
  `snap-20260726050323-7a05e048` (5 projects, 379 documents, no blocking errors).
  New metadata/auth helper files have no findings or open questions. The only module/type
  cycles retain the exact pre-existing node suffixes
  `efe376421f64`/`f602d7c77eb2` and
  `5374bd3c4751`/`a9e2e15d6c60`.
- `git diff --check`: no whitespace errors; line-ending warnings only.

## Closure Decision

- N006 is solved. Generated clients receive typed responses and a runtime-matching error
  envelope for every owned operation.
- SB07 is closed and the final canonical host capture/SharedInfo synchronization in SB08
  is unlocked.
