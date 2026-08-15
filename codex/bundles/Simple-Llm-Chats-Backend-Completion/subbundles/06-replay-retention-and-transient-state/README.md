# SB06 — Replay, Retention, And Transient State

## Status

- `Ready`

## Objective

- Make operation-event replay transactionally coherent and make durable cleanup/process-local accelerators bounded without weakening SSE correctness.

## Success Criteria

- Replay pages cannot mix stale operation state with newer terminal events.
- Cleanup deletes at most the configured number of event rows, never starves behind empty operations, drains backlog safely, and never touches active/nonterminal/canonical transcript/audit.
- Signal/schedule dictionaries evict idle/completed/profile-generation state under races with a deterministic bound.
- Retained/gap/high-water/terminal behavior remains correct after partial and full cleanup.

## Covered Inputs

- BC-050 through BC-054.

## Prerequisites

- SB05 `Pass` with durable high-water current.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Repositories/EfLlmChatOperationEventRepository.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationEventJournal.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationEventSignal.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationEventRetentionService.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationEventStreamSession.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/LlmChatDurableStreamEventTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/LlmChatPersistenceIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/LlmChatsApiPostgreSqlIntegrationTests.cs`

## UI Composition Contract

- N/A — persistence/SSE backend only.

## Deliverables

- One coherent bounded replay snapshot implementation.
- Event-rooted row-bounded indexed retention query and backlog scheduling semantics.
- Race-safe lazy idle/profile eviction for signal/schedule state using durable polling as fallback authority.
- Updated retention/options/docs semantics.

## Dependency Impact

- Blocks capacity/transfer and final SSE checkpoint because stale/inconsistent replay invalidates client behavior.

## Validation Depth

- Proof tier: `Behavioral`.
- Test solutions: Unit and Integration lanes.
- Filters: exact new cases in `LlmChatDurableStreamEventTests`, `LlmChatOperationEventJournalIntegrationTests`, and `LlmChatsApiPostgreSqlIntegrationTests`.
- Selection reason: transient race behavior plus real PostgreSQL isolation/deletion/indexed paging.
- Expected named cases: `Replay_page_observes_terminal_operation_and_event_from_one_snapshot`, `Replay_page_never_exposes_terminal_event_with_missing_result_metadata`, `Cleanup_batch_counts_event_rows_not_operations`, `Cleanup_skips_empty_old_operations_and_reaches_newer_events`, `Cleanup_never_deletes_active_operation_events`, `Cleanup_drains_multiple_bounded_batches_without_interval_starvation`, `Signal_state_evicts_many_completed_operations_without_lost_terminal_replay`, `Retention_schedule_evicts_old_profile_generations`, `Eviction_racing_wait_and_publish_remains_poll_correct`, and `Full_retention_emits_gap_with_durable_high_water_then_closes_terminal` (10 cases).
- Invalidation keys: event repository isolation/query/index, retention option meaning, schedule, signal, session polling/gap mapping.
- Broad-gate decision: deferred to SB10 for shared persistence/schema/SSE changes.

## Implementation Steps

1. Add a deterministic terminal-commit barrier between replay reads to reproduce inconsistent read-committed state.
2. Implement one short repeatable-read snapshot or equivalent single statement for the bounded replay page.
3. Replace operation-root cleanup with event-row selection ordered by eligible terminal completion/sequence and cap the delete itself.
4. Make due scheduling continue bounded drain/retry without a full-interval blind spot.
5. Add time/reference-aware lazy eviction to both singleton maps; prove no disposed primitive/lost correctness path.
6. Prove partial/full retention, active exclusion, high-water gap, and terminal close on PostgreSQL/Web host.
7. Inspect query plan/index use for the cleanup selection; build Core/Persistence/Web and list/run exact cases.

## C# Architecture Impact

- Repository isolation/query and singleton lifecycle change inside existing boundaries.

## Boundary Ownership

- Persistence owns snapshot/delete; Core owns schedule/signal/session semantics; Web only emits mapped results.

## Dependency Direction

- No cache/provider/Web dependency enters the event repository or Core signal.

## Pattern Decision

- PSR-06 and PSR-07; database journal is correctness authority.

## Testability Contract

- Real PostgreSQL for isolation/cleanup, fake time and deterministic barriers for eviction; no sleep-only race proof.

## Partial Class Policy

- No partials; small repository query helpers are allowed inside the same owner.

## Architecture Proof Required

- Transaction/isolation review, bounded SQL/query-plan evidence, memory-state bound evidence, and source assertion that canonical transcript/audit are untouched.

## Scope Exceptions

- Event retention deletes replay rows only; terminal operation retention itself remains outside this change unless an existing policy already owns it.

## Do Not Do

- Do not count operations as a batch, full-scan/delete unbounded rows, or make process-local signal state durable authority.
- Do not hold a replay transaction while waiting for live events.

## Acceptance Checklist

- [ ] Ten named cases discover and pass.
- [ ] Cleanup never exceeds row batch and reaches newer data.
- [ ] Replay snapshot is coherent and short-lived.
- [ ] Transient maps remain bounded under stress/profile switches.
- [ ] Changed project Release builds pass.

## Proof Required

- Failing-first/passing transcripts, exact discovery, transaction timeline, SQL/query plan and row counts, stress/eviction metrics, retained/gap SSE samples, and builds under `proof/SB06`.

## Browser Validation Logging

- N/A — no rendered UI.

## Progression Gate

- SB07 starts only after coherent replay and bounded cleanup/eviction proof pass.

## Reopen Triggers

- Any later event repository, high-water, retention/options, signal/schedule/session, or SSE gap mapping change reopens SB06-SB10.

## Suggested Agent Prompt

```text
Execute SB06 only. Reproduce the snapshot and cleanup failures first, then make database work row-bounded and transient state safely evictable. Preserve polling/journal authority and stop on an unbounded query or timing-only proof.
```
