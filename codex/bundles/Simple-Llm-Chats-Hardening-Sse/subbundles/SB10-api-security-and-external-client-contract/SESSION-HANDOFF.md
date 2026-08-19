# Session handoff — SB10

State: **Ready**

## Entry checklist

- [x] Root bundle status read
- [x] Dependencies complete and proof trusted
- [x] Actual repository/branch/head recorded
- [x] Current source and nearby tests inspected
- [x] Test budget understood
- [x] Database/dependency mode recorded

## Work performed

- Removed client-bindable conversation origin; HTTP supplies `Api`, while trusted product commands
  continue to preserve explicit `Application` origin.
- Added exact `api.llm-chats.read`, `api.llm-chats.manage`, and `api.llm-chats.execute` scopes and
  applied them to every LLM Chat route only when bearer authorization is enabled.
- Proved SSE accepts a normal read-scope Authorization header and rejects `access_token` query use.
- Versioned the operation snapshot as `candoitall.llm-chat-operation.v1`, retained the existing v1 SSE
  schema, documented retry guidance, and kept domain/EF entities out of OpenAPI response schemas.
- Removed the stored system prompt from definition responses and removed all raw exception-object
  logging from the LLM Chat product while retaining safe operation/conversation/type diagnostics.
- Documented why conversation-create idempotency is deferred until a future deployment-scoped external
  identity exists; added no dormant participant/channel/moderation/quota/retention fields or tables.

## Commands and results

- expected-red exact authorization test: 0/1, broad `api` incorrectly returned 200 before SB10;
- affected Web builds: both passed; final result 0 warnings/errors;
- focused real-host API union: 12/12 passed;
- direct product Application-origin test: 1/1 passed;
- exact PostgreSQL origin test: 1/1 passed and read `Origin.Api` from the authoritative row;
- CodeAnalytics `snap-20260815072303-363bd134`: four projects, zero cycles/blocking diagnostics;
- architecture, SSE, test-policy, redaction, partial, deployment-model, and diff guards passed.

Exact commands and results are recorded in `proof/SB10/transcripts` and `proof-manifest.json`.

## Bugs discovered and resolved

- The API accepted caller-controlled origin despite an `Api` default; the field is now absent and
  unmapped input is rejected.
- The parent API group authenticated callers but did not authorize LLM Chat capabilities; exact route
  policies now enforce distinct scopes and reject the broad `api` scope.
- Unexpected executor, state-machine, and application failures logged raw exception objects; the source
  audit found every path and replaced them with safe exception-type diagnostics.
- Definition detail responses exposed the stored system prompt; it is no longer part of the response DTO.

## Deviations

None. Four filtered test commands and two affected builds stayed within budget. The implementation is
two source commits because the architecture review required an additional exact PostgreSQL persistence
proof after the main implementation commit; `ebb8deae5f2deb0a379875fecf853ea8fc423be7` is the final
implementation head. No prohibited broad lane ran.

## Acceptance result

- [x] An API client cannot choose or spoof stored conversation origin.
- [x] Authorization-enabled hosts enforce distinct read, manage, and execute policies.
- [x] Authorization-disabled trusted-local hosts preserve documented local behavior.
- [x] No API/SSE error exposes prompts, system instructions, credentials, or raw provider failures.
- [x] OpenAPI exposes versioned transport DTOs and stable links, not domain or EF entities.
- [x] Future chatbot concerns remain a separate documented deployment boundary rather than dormant definition fields.

## Architecture result

- [x] Owner moved or strengthened as planned
- [x] Old shallow path removed/unreachable
- [x] Direct tests target the new owner
- [x] No forbidden reference/cycle/partial expansion
- [x] Architecture record updated if design changed

## Progression

Ready. SB11 is explicitly unlocked to run the focused PostgreSQL HTTP/SSE and portability proof and
make the CP2 decision. It must consume, not weaken, the SB10 origin/scope/redaction/deployment boundary.
