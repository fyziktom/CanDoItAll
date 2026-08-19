# SB04 — Durable dispatch lease and multi-instance cancellation

Status: **Completed**
Proof tier: **Governed**  
Depends on: **SB03**

## Outcome

Decouple paid execution from the HTTP request and make ownership, heartbeat, cancellation, and stale-run recovery safe across application instances.

## Owned requirements

- `RQ-013` — Use durable cross-instance execution ownership with claim, heartbeat, expiry, and release.
- `RQ-014` — Support bounded cross-instance cancellation and never infer liveness from an in-memory registry alone.
- `RQ-015` — Execute admitted operations independently from the initiating HTTP request through an available dispatcher.
- `RQ-016` — Never automatically redispatch when durable evidence says a provider dispatch may have started.

## Scope

- Add durable execution owner, lease generation/expiry/heartbeat, dispatch phase, and cancellation-request metadata.
- Implement atomic claim, renew, release, and stale-lease takeover decisions.
- Introduce an application-owned dispatcher/hosted worker that claims Pending operations independently of request cancellation.
- Use a local signal for low latency plus durable polling for multi-instance correctness.
- Keep the in-process CTS registry only as a current-owner optimization; absence never proves orphaning.
- Make remote cancellation durable and observed at bounded intervals and before finalization.
- Move an expired post-dispatch lease to RecoveryRequired and never auto-redispatch.
- Expose dispatcher availability and reject admission predictably when no executor can claim work.

## Explicit non-goals

- No PostgreSQL LISTEN/NOTIFY requirement.
- No new message-broker dependency.
- No auto retry after possibly billable dispatch.

## Current-source entry points

- `src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationCancellationRegistry.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationApplicationService.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Repositories/EfLlmChatOperationRepositories.cs`
- `src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`

Reinspect current source and nearby tests before editing. Paths are orientation, not a fixed file-edit
list.

## C# Architecture Impact

This work unit changes a correctness or extensibility boundary. Do not satisfy it by adding another
partial file, façade over unchanged behavior, callback that runs after a commit, or an interface whose
only implementation remains a monolith.

## Boundary Ownership

Decouple paid execution from the HTTP request and make ownership, heartbeat, cancellation, and stale-run recovery safe across application instances.

The product core owns invariants and contracts. EF/provider/host/Web details remain in their adapters.
Composition wires these owners and does not implement the behavior.

## Dependency Direction

Preserve `architecture/02-csharp-dependency-direction.md`. New references require a recorded graph
decision and no cycle. Product code must remain independent of Web/Razor and agent execution.

## Pattern Decision

Database-backed competing-consumer lease with local wake-up; at-most-one owner and fail-closed uncertain dispatch.

Any deviation must be written to `architecture/12-architecture-decision-register.md` before code and
must preserve the acceptance criteria.

## Testability Contract

The changed behavior must be directly testable through its new owner. Use the smallest focused tests:

- Two-service-provider integration tests sharing PostgreSQL.
- Lease/heartbeat/expiry tests with fake TimeProvider.
- Cross-instance cancellation and request-disconnect tests with a deterministic slow provider.

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

- [x] Only one instance can hold an execution lease for an operation at a time.
- [x] A client disconnect after admission does not cancel the durable operation.
- [x] Explicit cancellation reaches a local owner and is observed cross-instance within the configured bound.
- [x] Local registry absence never recovers or abandons another instance's live operation.
- [x] Expired pre-dispatch work may be reclaimed, while expired post-dispatch work becomes RecoveryRequired.
- [x] A host without an available dispatcher cannot falsely accept unexecutable work.

## Reopen triggers

- a host lane disables workers without availability handling
- streaming bypasses durable ownership
- multiple provider calls start under one lease generation

## Progression decision

SB04 passed at `7389daff6c21a4568895e514debe110434908d67`; SB05 is unlocked.

Update `SESSION-HANDOFF.md`, `proof-manifest.json`, root `EXECUTION-PROGRESS.md`,
`requirements-index.md`, and traceability before moving forward.
