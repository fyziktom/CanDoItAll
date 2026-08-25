# SB00 API, OpenAPI, SSE, and Compose characterization

Captured: 2026-08-24  
Product behavior changed by this artifact: **No**

## API composition and authorization

- `ApiEndpointRouteBuilderExtensions.MapCanDoItAllApi` owns the `/api` group and delegates each
  endpoint family to a focused mapper. Shared providers therefore get one focused mapper call;
  endpoint logic does not belong in the composition file.
- Scope wire names belong in `ApiAccessScopeNames`; policy names and matching belong in
  `ApiAuthorizationPolicies`.
- Current umbrella/specific policy precedent is `HasApiOrSpecificScope`. Shared catalog and
  invoke scopes use that rule, with ordinal/case-sensitive values.
- Native challenge/forbidden results currently use `ApiErrorResponse`. An OpenAI-compatible
  route cannot rely on that handler unchanged because its 401/403 responses must use the OpenAI
  error envelope.

## Streaming

- `ServerSentEventResponseWriter` proves the repository's buffering, flush, heartbeat, and
  cancellation conventions for native SSE.
- `AgentProviderEventsApi` is the relevant request-owned cancellation pattern; LLM Chat durable
  operations intentionally have different disconnect semantics.
- The native named/id-bearing event writer is not the OpenAI wire contract. SB04 needs a
  dedicated bounded relay/writer while reusing the proven cancellation and cleanup principles.

## OpenAPI

- `AddCanDoItAllApi` registers OpenAPI and operation transformers.
- Runtime documents are `/openapi/v1.json` and `/swagger/v1/swagger.json`.
- SharedInfo requires both captures to be byte-identical. Its current validator checks the
  stored artifact/hash/route claims but does not fetch both live endpoints; SB11 must add that
  live parity proof before updating SharedInfo.

## Compose constraints

- Root `compose.yaml` is development-only and currently owns one app/database pair.
- The app image requires root context plus sibling `components` and `filetools` contexts.
- Each central/client instance needs its own `/data`, database, and
  `CANDOITALL_HOST_BINDING_ID`; sharing an app volume would violate persistence and vault
  isolation.
- The app remains read-only-root, non-root, capability-dropped, bounded, and reachable only
  through its declared loopback port. Database services stay off host ports.
- `.dockerignore` excludes `tests`, `tools`, `proof`, and `evidence`; the deterministic upstream
  and E2E orchestrator need an explicit build context or narrowly scoped include.
- `tools/Validation/Test-Docker.ps1` is intentionally specific to root Compose. SB10 adds a
  dedicated validator for `compose.shared-providers.e2e.yaml` rather than weakening it.

## Gate decision

Pass with the amendments above locked into downstream requirements. No API route or Compose
service is added in SB00.
