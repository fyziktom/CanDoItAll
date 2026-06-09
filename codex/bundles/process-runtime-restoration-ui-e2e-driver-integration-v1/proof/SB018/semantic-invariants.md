# SB018 Semantic Invariants

Status: Passed.

## Shallow-Pass Trap

The gate is not satisfied by a successful `StartRunAsync` return value alone. The positive test must verify persisted database rows and read models: `ProcessRun`, `ProcessStepRun`, `ProcessWorkBrief`, `ProcessJournalEntry`, `ProcessOutboxRecord`, `ListRunsAsync`, and `GetRunDetailsAsync`.

## Adversarial Negative Proof

The proof would fail if any of these regressions were introduced:

- run creation succeeds for missing or unpublished definitions;
- a draft launch plan can be executed before approval/readiness;
- an already executed launch plan can create a second runtime run;
- project association is dropped from the persisted run;
- project-structure context is not serialized into the trigger reason or cannot be parsed back;
- root/dependent step statuses are not persisted as `Ready` and `Pending`;
- work briefs stop carrying project-structure context;
- dispatch outbox records are not written for a newly created run.

## Semantic Positive Proof

`bundle://proof/SB018/transcripts/focused-process-run-creation-persistence-tests.txt` proves real integration tests pass against the current application services and database. The tests validate both direct run start and launch-plan guarded execution.

## Anti-Stub Proof

`bundle://proof/SB018/transcripts/anti-stub-process-run-creation-tests.txt` proves the new methods use a real `TestApplication`, DI-resolved services, `ProcessesService`, and `AppDbContext` reads, with no mocks, stubs, test-server shortcuts, bundle paths, or sleeps.

## Raw-Note Closure

- RN-003 is partially closed further: SB015 proves the large-screen `/processes` UI start path; SB018 proves service-level project-context run creation persistence and duplicate launch guardrails.
- RN-007 remains open for dispatch/claim/finalizer, MAF/workflow, scheduler/workflow launch readiness, and runtime host roadmap phases.

## Production Behavior Artifact Matrix

No new production signals were introduced in SB016-SB018. Existing process run, launch plan, journal, work brief, outbox, and read-model behavior is covered by focused integration tests and source assertions.
