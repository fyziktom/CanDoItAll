# SB10 — API security and external-client contract

Status: **Locked**  
Proof tier: **Governed**  
Depends on: **SB09**

## Outcome

Harden the API for enterprise clients and future chatbot deployments without implementing channels, moderation, or UI.

## Owned requirements

- `RQ-027` — Conversation origin is server-owned and cannot be spoofed by an HTTP client.
- `RQ-028` — Enforce LLM Chat read/manage/execute API scopes when bearer authorization is enabled.
- `RQ-029` — Do not expose prompts, system instructions, credentials, raw provider payloads, or raw provider errors through logs/API/SSE.
- `RQ-033` — Preserve a clean future LlmChatDeployment boundary for enterprise chatbot channels without dormant deployment fields now.

## Scope

- Make conversation origin server-owned: HTTP creates Api origin, internal application calls create Application origin.
- Add and enforce LLM Chat read, manage, and execute policies/scopes when bearer authorization is enabled.
- Preserve trusted-local authorization-disabled behavior.
- Use Idempotency-Key or validated caller operation ID with redacted fingerprint conflicts.
- Decide/implement bounded idempotent conversation creation for external clients or document a justified defer.
- Authenticate SSE through normal headers/cookies; never query-string credentials.
- Version transport DTOs/event schemas and keep domain/EF entities out of OpenAPI.
- Document a later LlmChatDeployment boundary for participant identity, moderation, rate limits, retention, data residency, channels, and human handoff.

## Explicit non-goals

- No public widget.
- No anonymous access.
- No wildcard CORS.
- No moderation engine.
- No participant/deployment tables.

## Current-source entry points

- `src/App/CanDoItAll.Web/Api/LlmChatsApi.cs`
- `src/App/CanDoItAll.Web/Api/LlmChatOperationsApi.cs`
- `src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiAccess.cs`
- `src/App/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs`

Reinspect current source and nearby tests before editing. Paths are orientation, not a fixed file-edit
list.

## C# Architecture Impact

This work unit changes a correctness or extensibility boundary. Do not satisfy it by adding another
partial file, façade over unchanged behavior, callback that runs after a commit, or an interface whose
only implementation remains a monolith.

## Boundary Ownership

Harden the API for enterprise clients and future chatbot deployments without implementing channels, moderation, or UI.

The product core owns invariants and contracts. EF/provider/host/Web details remain in their adapters.
Composition wires these owners and does not implement the behavior.

## Dependency Direction

Preserve `architecture/02-csharp-dependency-direction.md`. New references require a recorded graph
decision and no cycle. Product code must remain independent of Web/Razor and agent execution.

## Pattern Decision

Transport-owned DTOs and policy authorization; future deployment is a separate aggregate/adapter boundary.

Any deviation must be written to `architecture/12-architecture-decision-register.md` before code and
must preserve the acceptance criteria.

## Testability Contract

The changed behavior must be directly testable through its new owner. Use the smallest focused tests:

- Focused auth-enabled and auth-disabled real-host tests.
- Origin spoofing, scope denial, fingerprint redaction, and SSE authentication tests.

Critical database/lifecycle claims require real PostgreSQL proof; mocks alone are supporting evidence.

## Partial Class Policy

No new production partial file may be the final boundary. A temporary extraction partial is allowed only
with a named deletion step inside this same subbundle and proof that it is removed before closure.

## Architecture Proof Required

- before/after owner and dependency evidence;
- direct test of the new owner;
- negative test that fails against the previous shallow implementation;
- source assertion that superseded behavior is no longer reachable;
- no cycle and no forbidden dependency;
- actual commands and commit SHA in the proof manifest.

## Validation budget

Follow `test-budget.json` and `plan/04-test-budget-and-gates.md`. During this work unit:

- no solution-wide test command;
- no unfiltered Unit or Integration project;
- no Playwright/LiveProcess/LongRunning/Quarantined gate;
- at most the declared focused command budget;
- do not rerun an unchanged failed command without a concrete fix or diagnostic reason.

## Acceptance checklist

- [ ] An API client cannot choose or spoof stored conversation origin.
- [ ] Authorization-enabled hosts enforce distinct read, manage, and execute policies.
- [ ] Authorization-disabled trusted-local hosts preserve documented local behavior.
- [ ] No API/SSE error exposes prompts, system instructions, credentials, or raw provider failures.
- [ ] OpenAPI exposes versioned transport DTOs and stable links, not domain or EF entities.
- [ ] Future chatbot concerns remain a separate documented deployment boundary rather than dormant definition fields.

## Reopen triggers

- deployment/channel implementation starts prematurely
- browser clients require ungoverned CORS changes
- scope claims exist without endpoint enforcement

## Progression decision

Unlock SB11 after this work unit passes, unless a checkpoint applies.

Update `SESSION-HANDOFF.md`, `proof-manifest.json`, root `EXECUTION-PROGRESS.md`,
`requirements-index.md`, and traceability before moving forward.
