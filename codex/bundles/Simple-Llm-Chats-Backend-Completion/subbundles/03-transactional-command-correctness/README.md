# SB03 — Transactional Command Correctness

## Status

- `Complete — Pass on working-tree candidate based on c3c7713927b9519200900583f227ead95fafb5e9`

## Objective

- Make turn replay, optimistic concurrency, conversation revision pinning, and local cancellation notification agree with canonical durable state under real races.

## Success Criteria

- Same-request operation replay succeeds without a local worker and produces no duplicate side effect; only new admission requires a worker.
- Real two-context CAS losers return stable conflicts.
- Conversation creation cannot pin a stale/inactive definition revision.
- Cancellation notification cannot throw or race a disposed registration after durable state commits.

## Covered Inputs

- BC-020 through BC-024.

## Prerequisites

- SB02 `Pass`; current public error/DTO contracts are frozen.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationApplicationService.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatDefinitionApplicationService.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatConversationApplicationService.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationCancellationRegistry.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Repositories`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/LlmChatOperationTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/LlmChatConversationApplicationServiceTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/LlmChatsTurnApiIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/LlmChatsApiPostgreSqlIntegrationTests.cs`

## UI Composition Contract

- N/A — application/persistence/API behavior only.

## Deliverables

- Admission path that resolves durable replay before new-work availability gating.
- Typed persistence/application CAS translation.
- Atomic definition status/revision guard for conversation create.
- Synchronized idempotent no-throw cancellation registration lifecycle.

## Dependency Impact

- Critical lifecycle foundation for executor, recovery, audit, replay, and SSE work.

## Validation Depth

- Proof tier: `Behavioral`.
- Test solutions: Unit and Integration lane `.slnx` files.
- Filters: exact new methods in `LlmChatOperationIdempotencyTests`, `LlmChatOperationCancellationTests`, `LlmChatsTurnApiIntegrationTests`, and `LlmChatsApiPostgreSqlIntegrationTests`.
- Selection reason: direct deterministic race ownership plus real PostgreSQL/HTTP translation.
- Expected named cases: `Committed_replay_succeeds_without_an_available_executor`, `New_operation_still_requires_an_available_executor`, `Different_fingerprint_still_conflicts_without_dispatch`, `Cancellation_racing_registration_disposal_is_no_throw_and_durable`, `Throwing_live_cancellation_callback_cannot_override_durable_result`, `Concurrent_definition_update_loser_returns_stable_conflict`, `Concurrent_definition_status_loser_returns_stable_conflict`, `Concurrent_conversation_rename_loser_returns_stable_conflict`, `Conversation_create_cannot_pin_concurrently_archived_definition`, and `Conversation_create_pins_one_committed_current_revision` (10 cases).
- Invalidation keys: admission ordering, fingerprint comparison, dispatcher signal, cancellation registry, definition/conversation repositories/UoW, EF concurrency mapping.
- Broad-gate decision: deferred to SB10 for shared lifecycle/persistence changes.

## Implementation Steps

1. Add failing-first replay cases with executor unavailable and assert provider/message/audit/event counts.
2. Move/reshape availability validation inside the new-admission atomic boundary without weakening fingerprint conflict.
3. Add deterministic cancellation lookup/dispose/callback races and make registration notification synchronized, idempotent, and no-throw.
4. Add two-context PostgreSQL CAS barriers for update/status/rename and translate only known concurrency failures.
5. Guard definition active/current revision within conversation creation's transaction and prove archive/revision races.
6. Build Core/Persistence/Web, list exact cases, run Unit then PostgreSQL/host cases, and inspect canonical rows.

## C# Architecture Impact

- Application/persistence transaction contracts change; no new layer/project.

## Boundary Ownership

- Application owns use-case order; Persistence owns row lock/CAS mechanics; Web only maps typed results.

## Dependency Direction

- No EF exception type enters Core public contracts and no Web catch fabricates domain conflict.

## Pattern Decision

- PSR-02; durable replay is resolved before admission-only environment checks.

## Testability Contract

- Deterministic barriers/two contexts; assert return contract and exact durable side-effect counts.

## Partial Class Policy

- No partial type; keep changes in existing owners and extract only small private helpers.

## Architecture Proof Required

- Transaction boundary review and source assertion that HTTP admission still does not invoke provider execution.

## Scope Exceptions

- Conversation-create idempotency is not introduced.

## Do Not Do

- Do not silently retry CAS, convert every database exception to conflict, or weaken expected tokens.
- Do not make live registry state authoritative over durable cancellation.

## Acceptance Checklist

- [x] Ten named positive/negative/race cases discover and pass.
- [x] No duplicate provider/message/audit/event side effect on replay.
- [x] PostgreSQL canonical state matches returned conflict/success.
- [x] Changed project Release builds pass.

## Proof Required

- Failing-first/passing transcripts, exact discovery, deterministic barrier description, database row snapshots/counts, source assertions, and build results under `proof/SB03`.

## Browser Validation Logging

- N/A — no rendered UI.

## Progression Gate

- CP1 cannot proceed to SB04 until all canonical transaction/replay/cancellation invariants pass.

## Reopen Triggers

- Any later admission, fingerprint, cancellation, definition/conversation repository, UoW, or concurrency mapping change reopens SB03 and SB04-SB10.

## Suggested Agent Prompt

```text
Execute SB03 only. Use deterministic race tests and canonical durable assertions. Make the smallest transaction/lifetime changes and stop if a fix would require Web-owned persistence logic or silent retry.
```
