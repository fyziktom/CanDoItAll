# SB11 — Focused PostgreSQL, HTTP, SSE, and portability proof

Status: **Locked**  
Proof tier: **Governed**  
Depends on: **SB10**

## Outcome

Prove the complete hardened backend and streaming API through realistic deterministic paths before cleanup and the one-time broad gate.

## Owned requirements

- `RQ-019` — Provide an additive provider-neutral incremental LLM invocation port without breaking existing non-streaming callers.
- `RQ-020` — Implement true incremental streaming for OpenAI, Azure OpenAI, and Ollama, with a deterministic fallback policy.
- `RQ-021` — Retry a stream only before its first emitted delta and never after partial output is externally visible.
- `RQ-022` — Persist a bounded per-operation event journal with monotonic sequence and durable replay authority.
- `RQ-023` — Expose SSE with Last-Event-ID/after replay, gaps, heartbeat, anti-buffering, profile lifetime, and terminal closure.
- `RQ-024` — SSE/client disconnect must not cancel the durable operation; explicit cancellation remains authoritative.
- `RQ-025` — Turn start must return 202 Accepted promptly with operation, status, and event links.
- `RQ-026` — Audit actual provider attempts with deterministic outcomes shared by direct and recovery reducers.
- `RQ-027` — Conversation origin is server-owned and cannot be spoofed by an HTTP client.
- `RQ-028` — Enforce LLM Chat read/manage/execute API scopes when bearer authorization is enabled.
- `RQ-029` — Do not expose prompts, system instructions, credentials, raw provider payloads, or raw provider errors through logs/API/SSE.
- `RQ-030` — Keep EF migration, model snapshot, retention, and database-transfer behavior consistent with the hardened schema.
- `RQ-031` — Keep implementation portable and prove affected behavior on Linux plus the final Windows/Linux/macOS CI matrix.

## Scope

- Run focused Unit tests for state machines, streaming parsers, event schemas, authorization, and architecture boundaries.
- Run focused PostgreSQL integration for transactions, migration, transfer, profile switching, leases, multi-instance execution, replay, and pagination.
- Run focused real-host HTTP/SSE tests with a slow deterministic provider double.
- Prove non-streaming fallback and real protocol parsing without live external provider calls.
- Build affected projects with the CI package dependency graph.
- Run focused Linux host proof where available; reserve Windows/macOS for the final CI matrix.
- Issue CP2 Ready or Blocked.

## Explicit non-goals

- No full solution stable test.
- No Playwright.
- No billable live-provider test.

## Current-source entry points

- `specifications/07-validation-matrix.md`
- `plan/04-test-budget-and-gates.md`
- `reviews/CP2-STREAMING-API.md`

Reinspect current source and nearby tests before editing. Paths are orientation, not a fixed file-edit
list.

## C# Architecture Impact

This work unit changes a correctness or extensibility boundary. Do not satisfy it by adding another
partial file, façade over unchanged behavior, callback that runs after a commit, or an interface whose
only implementation remains a monolith.

## Boundary Ownership

Prove the complete hardened backend and streaming API through realistic deterministic paths before cleanup and the one-time broad gate.

The product core owns invariants and contracts. EF/provider/host/Web details remain in their adapters.
Composition wires these owners and does not implement the behavior.

## Dependency Direction

Preserve `architecture/02-csharp-dependency-direction.md`. New references require a recorded graph
decision and no cycle. Product code must remain independent of Web/Razor and agent execution.

## Pattern Decision

Behavioral proof through real HTTP host and PostgreSQL; fake provider replaces only the external network boundary.

Any deviation must be written to `architecture/12-architecture-decision-register.md` before code and
must preserve the acceptance criteria.

## Testability Contract

The changed behavior must be directly testable through its new owner. Use the smallest focused tests:

- One filtered Unit union command.
- One filtered Integration union for PostgreSQL/backend.
- One filtered Integration union for HTTP/SSE.
- Affected project builds and bundle/source validators.

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

- [ ] Atomicity, profile fencing, distributed lease, cancellation, and idempotency scenarios pass against PostgreSQL.
- [ ] A slow streaming provider produces incremental SSE before terminal completion.
- [ ] Reconnect, gap, heartbeat, disconnect, explicit cancellation, and terminal closure pass through the real host.
- [ ] OpenAI/Azure/Ollama parser tests cover fragmented frames and failures without live network access.
- [ ] Migration, model snapshot, database transfer, and restart tests pass.
- [ ] Affected projects build with the CI package graph on the available Linux host.
- [ ] CP2 explicitly declares the backend/API ready or blocked.

## Reopen triggers

- final stable gate exposes a branch-induced owning regression
- CI reveals OS-specific streaming behavior
- OpenAPI changes after CP2

## Progression decision

Close `reviews/CP2-STREAMING-API.md`. Unlock SB12 only when CP2 is Ready.

Update `SESSION-HANDOFF.md`, `proof-manifest.json`, root `EXECUTION-PROGRESS.md`,
`requirements-index.md`, and traceability before moving forward.
