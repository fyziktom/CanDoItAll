# SB021 Semantic Invariants

Status: Passed.

## Shallow-Pass Trap

The gate is not satisfied by a report row, non-empty outbox, or direct service return value alone. The proof must demonstrate that durable outbox dispatch advances a launched process run through dispatcher eligibility, claim acquisition, claimed route execution, finalizer transition, artifact projection, decision recording, backing execution records, and final run completion.

## Adversarial Negative Proof

The proof would fail if any of these regressions were introduced:

- pending run outbox records cannot be claimed;
- dispatch candidates cannot be loaded or step dispatch claims cannot be acquired;
- claimed route execution stops before invoking the route handler pipeline;
- finalizer logic attempts to transition without the dispatch claim;
- required artifact records or branch decision records are not projected;
- deterministic backing execution runs are missing, incomplete, or failed;
- skipped branch steps are executed unexpectedly;
- any run outbox record dead-letters;
- the process run does not settle as `Completed`;
- the test driver mutates runtime state through raw SQL, direct update shortcuts, test-server shortcuts, sleeps, or bundle-path fixtures.

## Semantic Positive Proof

`bundle://proof/SB021/transcripts/focused-process-mock-durable-dispatch-e2e.txt` proves the deterministic process workflow completes end to end through durable outbox dispatch against the current application services and database.

## Anti-Stub Proof

`bundle://proof/SB021/transcripts/anti-stub-deterministic-dispatch-e2e.txt` proves the E2E method and outbox drain helper use real app services, DI, `ProcessesService`, `ProcessOutboxService`, workspace services, deterministic catalog setup, real launch execution, and real outbox processing, with no test-driver mutation shortcuts.

## Raw-Note Closure

- RN-004 remains partially open: SB021 proves generic deterministic process dispatch, claim, finalizer, and artifact projection plumbing, but the representative `.NET app` create/modify scenario remains planned by SB022-SB027.
- RN-007 is partially closed: SB021 proves the dispatch/claim/route/finalizer slice works through the durable outbox. Runtime host, registry, selector, DI registration, manager command, scheduler, and workflow-driver roadmap items remain planned by SB037-SB042 and SB050-SB054.

## Production Behavior Artifact Matrix

No new production signals were introduced in SB019-SB021. Existing process dispatch, claim, route execution, finalizer, artifact projection, decision record, execution run, and outbox behavior is covered by focused integration test proof and source assertions.
