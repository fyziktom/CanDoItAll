# SB09 — Asynchronous turn API and SSE

Status: **Locked**  
Proof tier: **Governed**  
Depends on: **SB08**

## Outcome

Expose durable asynchronous turn admission and replayable SSE for slow/long responses without binding execution to one HTTP connection.

## Owned requirements

- `RQ-023` — Expose SSE with Last-Event-ID/after replay, gaps, heartbeat, anti-buffering, profile lifetime, and terminal closure.
- `RQ-024` — SSE/client disconnect must not cancel the durable operation; explicit cancellation remains authoritative.
- `RQ-025` — Turn start must return 202 Accepted promptly with operation, status, and event links.
- `RQ-029` — Do not expose prompts, system instructions, credentials, raw provider payloads, or raw provider errors through logs/API/SSE.

## Scope

- Change turn start to return 202 Accepted promptly with Location, operationId, statusUrl, eventsUrl, and revision metadata.
- Add GET /api/llm-chat-operations/{operationId}/events.
- Reuse ServerSentEventResponseWriter for cursor parsing, framing, heartbeat, anti-buffering headers, and gap events.
- Implement a durable journal reader compatible with the existing bounded replay reader shape, combining SQL replay with local wake-up and bounded polling.
- Support Last-Event-ID and existing after cursor with consistent invalid/conflict handling.
- Emit versioned typed event names and close after a terminal event.
- Terminate the projection on profile switch without losing durable events in the correct profile.
- Never cancel the operation on SSE disconnect; only explicit cancellation changes operation state.
- Update OpenAPI and stable transport errors.

## Explicit non-goals

- No WebSocket.
- No browser UI.
- No bearer token in query string.
- No full-response server buffering.

## Current-source entry points

- `src/App/CanDoItAll.Web/Api/LlmChatOperationsApi.cs`
- `src/App/CanDoItAll.Web/Api/LlmChatsApi.cs`
- `src/App/CanDoItAll.Web/Api/Streaming/ServerSentEventResponseWriter.cs`
- `src/App/CanDoItAll.Web/Api/Streaming/ProfileBoundedReplayEventStream.cs`
- `src/App/CanDoItAll.Web/Api/WorkflowRunEventsApi.cs`

Reinspect current source and nearby tests before editing. Paths are orientation, not a fixed file-edit
list.

## C# Architecture Impact

This work unit changes a correctness or extensibility boundary. Do not satisfy it by adding another
partial file, façade over unchanged behavior, callback that runs after a commit, or an interface whose
only implementation remains a monolith.

## Boundary Ownership

Expose durable asynchronous turn admission and replayable SSE for slow/long responses without binding execution to one HTTP connection.

The product core owns invariants and contracts. EF/provider/host/Web details remain in their adapters.
Composition wires these owners and does not implement the behavior.

## Dependency Direction

Preserve `architecture/02-csharp-dependency-direction.md`. New references require a recorded graph
decision and no cycle. Product code must remain independent of Web/Razor and agent execution.

## Pattern Decision

202 command resource plus GET status and GET durable replay stream; SSE is a projection, never execution owner.

Any deviation must be written to `architecture/12-architecture-decision-register.md` before code and
must preserve the acceptance criteria.

## Testability Contract

The changed behavior must be directly testable through its new owner. Use the smallest focused tests:

- Real-host SSE tests with a deterministic slow streaming provider.
- Reconnect, Last-Event-ID/after, gap, heartbeat, terminal close, and profile-switch tests.
- Disconnect-without-cancel and explicit-cancel tests.

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

- [ ] Turn start returns 202 without waiting for provider completion.
- [ ] SSE delivers ordered deltas and exactly one terminal operation event.
- [ ] Reconnect resumes without duplicate semantic text or a second provider call.
- [ ] A replay gap emits stream.gap with a usable recovery cursor while status remains authoritative.
- [ ] SSE disconnect does not cancel or abandon the operation.
- [ ] Explicit cancellation is visible in operation status and event stream.
- [ ] The stream closes after terminal success, failure, cancellation, or RecoveryRequired.
- [ ] Existing anti-buffering, heartbeat, cursor, and profile-lifetime behavior is reused.

## Reopen triggers

- external clients require an incompatible cursor contract
- terminal events can be missed between status and stream attach
- supported proxies buffer incremental output

## Progression decision

Unlock SB10 after this work unit passes, unless a checkpoint applies.

Update `SESSION-HANDOFF.md`, `proof-manifest.json`, root `EXECUTION-PROGRESS.md`,
`requirements-index.md`, and traceability before moving forward.
