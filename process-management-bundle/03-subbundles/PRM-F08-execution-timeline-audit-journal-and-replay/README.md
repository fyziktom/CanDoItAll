# PRM-F08 — Execution timeline, audit journal, and replay

## Objective

Persist append-oriented events for every important runtime change so project, manager, QA, and future training flows can replay what happened.

## Priority and wave

- Priority: **High**
- Planned wave: **Wave 2**
- Depends on: **PRM-F07**

## Why this feature exists

This feature is part of the first process-management bundle because the user explicitly wants process definitions, actor responsibility, handoffs, and interactive modeling to land **before** the intelligence lake and before deep runtime coupling to the AgentFramework overlay.

## In scope

- Every run change emits a durable process event with actor and reason metadata.
- High-level process events appear on the shared activity stream.
- A replay API can reconstruct step order and handoff decisions from journaled events.
- Journal writes are separated from mutable current-state rows.

## Non-goals

- Do not infer replay only from mutable current-state rows.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Processes/ProcessEventJournal.cs (new)`
- `src/CanDoItAll.Modules.Activity/ActivityModels.cs`
- `src/CanDoItAll.SharedKernel/ActivityStream.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessJournalIntegrationTests.cs (new)`
- `tests/CanDoItAll.Tests.Unit/ProcessReplayTests.cs (new)`
