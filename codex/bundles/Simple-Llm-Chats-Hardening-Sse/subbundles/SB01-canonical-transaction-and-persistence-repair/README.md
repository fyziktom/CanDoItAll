# SB01 — Canonical transaction and persistence repair

Status: **Ready**
Proof tier: **Governed**  
Depends on: **SB00**

## Outcome

Remove duplicate writable conversation truth and make definition/conversation/transcript mutations genuinely atomic in one database transaction.

## Owned requirements

- `RQ-003` — Maintain one canonical writable owner for conversation title and transcript metadata.
- `RQ-004` — Create and rename conversation state atomically without orphan or divergent rows.
- `RQ-030` — Keep EF migration, model snapshot, retention, and database-transfer behavior consistent with the hardened schema.

## Scope

- Choose and document one canonical owner for conversation title, timestamps, transcript revision, and active-turn state.
- Remove or demote duplicate title/metadata across LlmChatConversationRow and LlmChatTranscriptRow; read models may join but not create a second writable truth.
- Replace the fake shared unit of work in which EfLlmConversationStore creates an independent AppDbContext and transaction.
- Implement atomic conversation create and rename through one command DbContext and one transaction.
- Update migration/model snapshot and database-transfer payload as required.
- Add failure-injection PostgreSQL tests proving no orphan transcript, orphan product row, or divergent title survives.

## Explicit non-goals

- No provider invocation.
- No operation dispatcher.
- No UI/API redesign.

## Current-source entry points

- `src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatConversationApplicationService.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Conversations/EfLlmConversationStore.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Repositories/EfLlmChatUnitOfWork.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Entities/LlmChatPersistenceRows.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/EntityConfigurations/LlmChatConversationConfigurations.cs`

Reinspect current source and nearby tests before editing. Paths are orientation, not a fixed file-edit
list.

## C# Architecture Impact

This work unit changes a correctness or extensibility boundary. Do not satisfy it by adding another
partial file, façade over unchanged behavior, callback that runs after a commit, or an interface whose
only implementation remains a monolith.

## Boundary Ownership

Remove duplicate writable conversation truth and make definition/conversation/transcript mutations genuinely atomic in one database transaction.

The product core owns invariants and contracts. EF/provider/host/Web details remain in their adapters.
Composition wires these owners and does not implement the behavior.

## Dependency Direction

Preserve `architecture/02-csharp-dependency-direction.md`. New references require a recorded graph
decision and no cycle. Product code must remain independent of Web/Razor and agent execution.

## Pattern Decision

Single transactional command store with separate read models; no ambient or service-located transaction.

Any deviation must be written to `architecture/12-architecture-decision-register.md` before code and
must preserve the acceptance criteria.

## Testability Contract

The changed behavior must be directly testable through its new owner. Use the smallest focused tests:

- Focused unit tests for canonical ownership/mapping.
- Focused PostgreSQL failure-injection tests for create and rename atomicity.
- Focused migration and database-transfer tests only when schema changes.

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

- [ ] Conversation title and transcript metadata have exactly one canonical writable owner.
- [ ] Conversation creation commits product binding and transcript root together or commits neither.
- [ ] Conversation rename updates the canonical title once and cannot leave divergent rows.
- [ ] No production conversation store creates a second AppDbContext inside an active product command.
- [ ] Migration and transfer payloads preserve the repaired canonical model.

## Reopen triggers

- a later turn protocol introduces a second transaction owner
- a read model accepts writes
- migration snapshot differs from runtime model

## Progression decision

Unlock SB02 after this work unit passes, unless a checkpoint applies.

Update `SESSION-HANDOFF.md`, `proof-manifest.json`, root `EXECUTION-PROGRESS.md`,
`requirements-index.md`, and traceability before moving forward.
