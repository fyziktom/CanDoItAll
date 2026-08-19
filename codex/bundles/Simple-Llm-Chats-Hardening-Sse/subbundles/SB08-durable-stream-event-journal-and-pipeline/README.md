# SB08 — Durable stream event journal and pipeline

Status: **Locked**  
Proof tier: **Governed**  
Depends on: **SB07**

## Outcome

Integrate incremental output into the durable operation lifecycle with replayable, bounded, non-canonical events.

## Owned requirements

- `RQ-006` — Commit assistant finalization or exact failure compensation atomically with operation state and usage evidence.
- `RQ-008` — A durable cancellation request committed before semantic completion must prevent Succeeded.
- `RQ-014` — Support bounded cross-instance cancellation and never infer liveness from an in-memory registry alone.
- `RQ-022` — Persist a bounded per-operation event journal with monotonic sequence and durable replay authority.
- `RQ-026` — Audit actual provider attempts with deterministic outcomes shared by direct and recovery reducers.
- `RQ-029` — Do not expose prompts, system instructions, credentials, raw provider payloads, or raw provider errors through logs/API/SSE.
- `RQ-030` — Keep EF migration, model snapshot, retention, and database-transfer behavior consistent with the hardened schema.

## Scope

- Add an append-only per-operation event journal with unique monotonic sequence.
- Commit operation state-transition events in the same transaction as the state they describe.
- Coalesce text deltas by configured byte/time thresholds; never persist one row per token.
- Retain every event while an operation is nonterminal and apply bounded terminal retention afterward.
- Commit one canonical assistant transcript message only after successful provider terminal completion.
- Preserve partial deltas on failure as explicitly incomplete, non-canonical event evidence.
- Publish a profile-bounded local wake-up only after durable commit; PostgreSQL remains replay authority.
- Update migration, cleanup/retention, and database-transfer decisions.

## Explicit non-goals

- No HTTP/SSE framing.
- No raw provider frame storage.
- No partial assistant transcript messages.

## Current-source entry points

- `src/Modules/CanDoItAll.Modules.LlmChats/Operations/LlmChatOperation.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Entities/LlmChatPersistenceRows.cs`
- `src/App/CanDoItAll.Web/Api/Streaming/ProfileBoundedReplayEventStream.cs`

Reinspect current source and nearby tests before editing. Paths are orientation, not a fixed file-edit
list.

## C# Architecture Impact

This work unit changes a correctness or extensibility boundary. Do not satisfy it by adding another
partial file, façade over unchanged behavior, callback that runs after a commit, or an interface whose
only implementation remains a monolith.

## Boundary Ownership

Integrate incremental output into the durable operation lifecycle with replayable, bounded, non-canonical events.

The product core owns invariants and contracts. EF/provider/host/Web details remain in their adapters.
Composition wires these owners and does not implement the behavior.

## Dependency Direction

Preserve `architecture/02-csharp-dependency-direction.md`. New references require a recorded graph
decision and no cycle. Product code must remain independent of Web/Razor and agent execution.

## Pattern Decision

Transactional outbox/event journal with post-commit local signal; journal is replay evidence, transcript stays canonical.

Any deviation must be written to `architecture/12-architecture-decision-register.md` before code and
must preserve the acceptance criteria.

## Testability Contract

The changed behavior must be directly testable through its new owner. Use the smallest focused tests:

- PostgreSQL sequence/concurrency/retention tests.
- Slow streaming operation tests for success, cancellation, and failure after partial output.
- Cross-instance committed-event visibility tests.

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

- [ ] Every operation event has a unique monotonic sequence within its operation.
- [ ] State-transition events commit in the same transaction as their state.
- [ ] Text chunks are coalesced and bounded rather than one row per token.
- [ ] Partial output is replayable but never canonical unless finalization succeeds.
- [ ] A second instance reads all committed events without first-instance memory.
- [ ] Event payloads contain no system prompt, user prompt, credential, or raw provider error.

## Reopen triggers

- SSE needs an unversioned event field
- retention creates a gap while operation is running
- event signal occurs before database commit

## Progression decision

Unlock SB09 after this work unit passes, unless a checkpoint applies.

Update `SESSION-HANDOFF.md`, `proof-manifest.json`, root `EXECUTION-PROGRESS.md`,
`requirements-index.md`, and traceability before moving forward.
