# Codex task — PRM-F08

Implement **Execution timeline, audit journal, and replay** inside the uploaded CanDoItAll solution.

## Constraints

- Treat `CanDoItAll.Modules.Processes` as the canonical owner for process-management behavior.
- Do not create a new durable agent registry; use CRM-HR bindings when actors are involved.
- Do not add direct compile-time dependency on the uploaded AgentFramework repo in the first process-management implementation.
- Keep all code comments in English.
- Preserve buildability for the current solution layout.

## Required outputs

- Code changes for this feature
- Matching tests
- Migration updates if persistence changes
- A short implementation note describing what changed and how it was verified

## Done definition

This task is done when:

- Every run change emits a durable process event with actor and reason metadata.
- High-level process events appear on the shared activity stream.
- A replay API can reconstruct step order and handoff decisions from journaled events.
- Journal writes are separated from mutable current-state rows.

## Recommended first files to touch

- `src/CanDoItAll.Modules.Processes/ProcessEventJournal.cs (new)`
- `src/CanDoItAll.Modules.Activity/ActivityModels.cs`
- `src/CanDoItAll.SharedKernel/ActivityStream.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessJournalIntegrationTests.cs (new)`
- `tests/CanDoItAll.Tests.Unit/ProcessReplayTests.cs (new)`
