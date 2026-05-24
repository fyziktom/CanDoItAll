# Branch review

## What Codex fulfilled

Codex made meaningful progress over the prior iteration:

- SQLite is gone from typed provider/source enums.
- Snapshot runtime service was removed instead of kept as a deferred stub.
- The branch is no longer behind `development` in the GitHub compare.
- Normal `AppDbContext` construction uses a canonical startup profile with `AddPooledDbContextFactory`.
- Profile activation is restart-first and does not silently switch the current process database.
- PostgreSQL `FOR UPDATE SKIP LOCKED` claim primitives were added for key outbox/delivery paths.
- Process dispatch has durable claim fields on `ProcessStepRun`.

## What Codex still skipped or only partially solved

- It did not remove the old `DatabaseRuntimeState` switch/drain model; it only bypassed it for normal DbContext creation.
- It did not fully separate runtime canonical state from next-start persisted activation in the domain/API contract.
- It did not turn claimed-work batch claims into actual bounded parallel processing.
- It did not prove stale process dispatch claim loss prevents all transition/artifact commits.
- It did not reduce the heavy `LoadDispatchCandidateAsync` full-run scan before claim.
- It left significant proof/bundle artifacts in the branch diff. This may be acceptable if bundle artifacts are intentionally committed, but it needs explicit scope decision.
