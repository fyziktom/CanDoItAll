# SB06 — Backend hardening checkpoint

Status: **Locked**  
Proof tier: **Governed**  
Depends on: **SB05**

## Outcome

Prove the non-streaming backend is transactionally, lifecycle, profile, and multi-instance safe before adding streaming complexity.

## Owned requirements

- `RQ-003` — Maintain one canonical writable owner for conversation title and transcript metadata.
- `RQ-004` — Create and rename conversation state atomically without orphan or divergent rows.
- `RQ-005` — Commit operation claim, pending user message, active turn, and admission evidence atomically.
- `RQ-006` — Commit assistant finalization or exact failure compensation atomically with operation state and usage evidence.
- `RQ-007` — Escalate unresolved compensation to RecoveryRequired; never leave a live active turn behind a terminal failure.
- `RQ-008` — A durable cancellation request committed before semantic completion must prevent Succeeded.
- `RQ-009` — Resolve idempotent replay by operation identity/fingerprint before mutable lifecycle validation.
- `RQ-010` — Conversation archive must not race an active turn or nonterminal operation.
- `RQ-011` — Fence every public use case from first read through final commit/return to one database profile identity and generation.
- `RQ-012` — A profile switch must prevent old-generation writes and produce deterministic retained evidence.
- `RQ-013` — Use durable cross-instance execution ownership with claim, heartbeat, expiry, and release.
- `RQ-014` — Support bounded cross-instance cancellation and never infer liveness from an in-memory registry alone.
- `RQ-015` — Execute admitted operations independently from the initiating HTTP request through an available dispatcher.
- `RQ-016` — Never automatically redispatch when durable evidence says a provider dispatch may have started.
- `RQ-017` — Use bounded SQL/keyset read models for collection and transcript pagination without N+1 queries.
- `RQ-018` — Build provider context windows from bounded database reads rather than full transcript materialization.

## Scope

- Run the union of focused backend Unit and PostgreSQL integration classes from SB01-SB05.
- Run architecture boundary, migration-model, and source ownership checks.
- Review state machine, canonical truth, profile scope, execution lease, and query-plan evidence.
- Delete superseded wrappers/sinks/independent-context UoW paths rather than leaving parallel implementations.
- Issue CP1 Ready or Blocked.

## Explicit non-goals

- No streaming contracts.
- No SSE endpoint.
- No full solution suite.

## Current-source entry points

- `reviews/CP1-BACKEND-HARDENING.md`
- `plan/03-architecture-checkpoints.md`
- `plan/04-test-budget-and-gates.md`

Reinspect current source and nearby tests before editing. Paths are orientation, not a fixed file-edit
list.

## C# Architecture Impact

This work unit changes a correctness or extensibility boundary. Do not satisfy it by adding another
partial file, façade over unchanged behavior, callback that runs after a commit, or an interface whose
only implementation remains a monolith.

## Boundary Ownership

Prove the non-streaming backend is transactionally, lifecycle, profile, and multi-instance safe before adding streaming complexity.

The product core owns invariants and contracts. EF/provider/host/Web details remain in their adapters.
Composition wires these owners and does not implement the behavior.

## Dependency Direction

Preserve `architecture/02-csharp-dependency-direction.md`. New references require a recorded graph
decision and no cycle. Product code must remain independent of Web/Razor and agent execution.

## Pattern Decision

Checkpoint only; no new abstraction unless required to remove a duplicate production path.

Any deviation must be written to `architecture/12-architecture-decision-register.md` before code and
must preserve the acceptance criteria.

## Testability Contract

The changed behavior must be directly testable through its new owner. Use the smallest focused tests:

- One filtered Unit union command for owning backend classes.
- One filtered Integration union command for PostgreSQL owners.
- Affected project builds and static architecture/migration checks.

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

- [ ] All SB01-SB05 acceptance criteria have current-head proof.
- [ ] No parallel legacy turn-execution or independent-transaction path remains reachable.
- [ ] Focused backend Unit and PostgreSQL integration gates pass.
- [ ] Migration/model and database-transfer proof pass when schema changed.
- [ ] CP1 explicitly unlocks or blocks streaming work.

## Reopen triggers

- streaming changes the hardened transaction/state protocol
- later API proof exposes profile or lease behavior not covered by CP1

## Progression decision

Close `reviews/CP1-BACKEND-HARDENING.md`. Unlock SB07 only when CP1 is Ready.

Update `SESSION-HANDOFF.md`, `proof-manifest.json`, root `EXECUTION-PROGRESS.md`,
`requirements-index.md`, and traceability before moving forward.
