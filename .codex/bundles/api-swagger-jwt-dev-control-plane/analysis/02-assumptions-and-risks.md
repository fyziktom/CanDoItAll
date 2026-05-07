# Assumptions And Risks

## Assumptions

- The API belongs in the web host because it must track whichever port the web app is started under.
- Existing services are thread-safe at their public boundary because they create their own EF contexts or are scoped appropriately.
- The first token UI can issue self-contained JWTs without database-backed token revocation.

## Risks

- A broad API can become a second product surface with duplicated behavior. Mitigation: route handlers must call existing services and use shared result/error helpers only at the HTTP boundary.
- Swagger/OpenAPI support can accidentally expose development endpoints in production. Mitigation: map docs based on explicit configuration/environment, and keep auth independent of environment.
- Optional auth can be misread as weak auth. Mitigation: disabled means intentionally anonymous local API; enabled means all API groups require bearer tokens.
- Process run detail payloads can overload clients. Mitigation: add typed include/filter query object.

## Critical Path Risks

- JWT misconfiguration must stop startup when enabled; otherwise callers may believe an API is protected when it is not.
- Endpoint filter/auth must cover both new API groups and the existing project-structure group if JWT is active.
- Process launch endpoints must preserve HR matching and project-structure context flow rather than only starting already-published runs.

## Validation Risks

- Full browser proof may be blocked by local database/provider startup time. If blocked, record exact blocker and still run targeted builds/tests.
- Playwright proof for Settings UI depends on successful app launch; component tests may cover behavior but cannot fully replace browser proof.
- Existing package vulnerability warnings can appear during build and are not caused by this bundle unless new packages introduce them.

## Reopen Triggers

- Any endpoint writes directly to EF entities when a public service already exists.
- JWT-enabled integration test allows anonymous access to a protected API.
- Settings token UI can generate a token when JWT is disabled or signing key is missing.
- Process run detail endpoint ignores `stepRunId` or artifact filters and returns full detail anyway.
- Architecture review finds copied MCP wrapper logic instead of a shared helper or service call.
