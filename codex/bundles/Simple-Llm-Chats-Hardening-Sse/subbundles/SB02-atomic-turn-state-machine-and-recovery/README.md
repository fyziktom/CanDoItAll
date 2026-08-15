# SB02 — Atomic turn state machine and recovery

Status: **Completed**
Proof tier: **Governed**  
Depends on: **SB01**

## Outcome

Make admission, finalization, cancellation, compensation, reconciliation, and idempotent replay one deterministic durable protocol.

## Owned requirements

- `RQ-005` — Commit operation claim, pending user message, active turn, and admission evidence atomically.
- `RQ-006` — Commit assistant finalization or exact failure compensation atomically with operation state and usage evidence.
- `RQ-007` — Escalate unresolved compensation to RecoveryRequired; never leave a live active turn behind a terminal failure.
- `RQ-008` — A durable cancellation request committed before semantic completion must prevent Succeeded.
- `RQ-009` — Resolve idempotent replay by operation identity/fingerprint before mutable lifecycle validation.
- `RQ-010` — Conversation archive must not race an active turn or nonterminal operation.
- `RQ-016` — Never automatically redispatch when durable evidence says a provider dispatch may have started.
- `RQ-026` — Audit actual provider attempts with deterministic outcomes shared by direct and recovery reducers.

## Scope

- Replace post-commit evidence callbacks with atomic turn commands.
- Persist operation claim, pending user message, active-turn marker, admission evidence, and event in one transaction.
- Persist assistant message, active-turn clearing, usage, completion evidence, terminal operation state, and terminal event in one transaction.
- Persist exact compensation and terminal failure/cancellation atomically; compensation exhaustion must produce RecoveryRequired.
- Resolve an existing operation/fingerprint before validating current definition or conversation lifecycle.
- Add monotonic durable cancellation evidence checked in the same finalization transaction so prior cancellation cannot become Succeeded.
- Atomically block archive while an active turn or nonterminal operation exists.
- Use one pure reducer for direct completion, retry, restart reconciliation, and explicit recovery.

## Explicit non-goals

- No background dispatcher yet.
- No provider streaming yet.
- No UI.

## Current-source entry points

- `src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationApplicationService.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats/Operations/LlmChatOperation.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/ProfileFencedLlmConversationStore.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations/LlmConversationService.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/AuditedLlmChatInvocationPort.cs`

Reinspect current source and nearby tests before editing. Paths are orientation, not a fixed file-edit
list.

## C# Architecture Impact

This work unit changes a correctness or extensibility boundary. Do not satisfy it by adding another
partial file, façade over unchanged behavior, callback that runs after a commit, or an interface whose
only implementation remains a monolith.

## Boundary Ownership

Make admission, finalization, cancellation, compensation, reconciliation, and idempotent replay one deterministic durable protocol.

The product core owns invariants and contracts. EF/provider/host/Web details remain in their adapters.
Composition wires these owners and does not implement the behavior.

## Dependency Direction

Preserve `architecture/02-csharp-dependency-direction.md`. New references require a recorded graph
decision and no cycle. Product code must remain independent of Web/Razor and agent execution.

## Pattern Decision

Transactional state-machine command store plus pure deterministic reducer; provider calls stay outside transactions.

Any deviation must be written to `architecture/12-architecture-decision-register.md` before code and
must preserve the acceptance criteria.

## Testability Contract

The changed behavior must be directly testable through its new owner. Use the smallest focused tests:

- Direct state-machine unit tests for every legal/illegal transition.
- PostgreSQL crash-window tests at admission, dispatch, provider return, assistant commit, compensation, and terminal state.
- Idempotent replay tests after definition suspension and conversation archive.

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

- [ ] Turn admission is one transaction across operation, transcript, evidence, and event state.
- [ ] Successful completion is one transaction across assistant message, usage, active-turn clearing, and operation success.
- [ ] Failed compensation cannot leave a terminal Failed or Cancelled operation with a live active turn.
- [ ] A cancellation request committed before finalization prevents Succeeded.
- [ ] Same operation ID and fingerprint replays the original result even after later lifecycle changes.
- [ ] Same operation ID with a different fingerprint conflicts before provider dispatch.
- [ ] Conversation archive cannot race an active or nonterminal turn.
- [ ] Direct completion and recovery reduce identical durable evidence to the same outcome.

## Reopen triggers

- a dispatcher adds an unmodeled transition
- streaming changes partial-output semantics
- reconciliation can redispatch after possible dispatch

## Progression decision

Unlock SB03 after this work unit passes, unless a checkpoint applies.

Update `SESSION-HANDOFF.md`, `proof-manifest.json`, root `EXECUTION-PROGRESS.md`,
`requirements-index.md`, and traceability before moving forward.
