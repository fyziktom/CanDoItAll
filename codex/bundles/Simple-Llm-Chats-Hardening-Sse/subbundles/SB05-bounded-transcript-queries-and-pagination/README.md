# SB05 — Bounded transcript queries and pagination

Status: **Locked**  
Proof tier: **Governed**  
Depends on: **SB04**

## Outcome

Remove full-transcript and N+1 reads from list, detail, pagination, and per-turn context-window construction.

## Owned requirements

- `RQ-017` — Use bounded SQL/keyset read models for collection and transcript pagination without N+1 queries.
- `RQ-018` — Build provider context windows from bounded database reads rather than full transcript materialization.

## Scope

- Create explicit SQL read models for definition list, conversation list, operation list/detail, and transcript page.
- Use deterministic keyset pagination for externally consumable collections with enforced page limits.
- Page transcript rows in SQL rather than loading a full LlmConversationDocument then Skip/Take.
- Build provider context windows from system entries plus the newest bounded message range.
- Eliminate per-item definition revision/tag and conversation transcript lookups.
- Preserve canonical ordering by sequence, never timestamp.
- Add query-count and large-transcript characterization tests.

## Explicit non-goals

- No summarization or RAG.
- No full-text search.
- No UI virtualization.

## Current-source entry points

- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Conversations/EfLlmConversationStore.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Repositories/EfLlmChatConversationRepository.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatDefinitionApplicationService.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatConversationApplicationService.cs`

Reinspect current source and nearby tests before editing. Paths are orientation, not a fixed file-edit
list.

## C# Architecture Impact

This work unit changes a correctness or extensibility boundary. Do not satisfy it by adding another
partial file, façade over unchanged behavior, callback that runs after a commit, or an interface whose
only implementation remains a monolith.

## Boundary Ownership

Remove full-transcript and N+1 reads from list, detail, pagination, and per-turn context-window construction.

The product core owns invariants and contracts. EF/provider/host/Web details remain in their adapters.
Composition wires these owners and does not implement the behavior.

## Dependency Direction

Preserve `architecture/02-csharp-dependency-direction.md`. New references require a recorded graph
decision and no cycle. Product code must remain independent of Web/Razor and agent execution.

## Pattern Decision

CQRS-style bounded read models over the canonical tables; no second persistence truth.

Any deviation must be written to `architecture/12-architecture-decision-register.md` before code and
must preserve the acceptance criteria.

## Testability Contract

The changed behavior must be directly testable through its new owner. Use the smallest focused tests:

- Focused query-model unit tests.
- PostgreSQL integration with thousands of messages and multiple definitions.
- SQL command-count assertions where current test infrastructure permits.

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

- [ ] Transcript paging executes a bounded SQL query and never materializes the full transcript.
- [ ] Conversation and definition listings do not issue one query per item.
- [ ] Context-window construction reads only the bounded entries it can send.
- [ ] Externally exposed collections use deterministic cursors and enforced page limits.
- [ ] Large-transcript tests prove stable memory/query behavior without changing canonical content.

## Reopen triggers

- a new endpoint loads documents for list projection
- stream events misuse transcript sequence
- deployment requirements need unplanned participant indexes

## Progression decision

Unlock SB06 after this work unit passes, unless a checkpoint applies.

Update `SESSION-HANDOFF.md`, `proof-manifest.json`, root `EXECUTION-PROGRESS.md`,
`requirements-index.md`, and traceability before moving forward.
