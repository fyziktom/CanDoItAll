# Session handoff — SB08

State: **Ready**

## Entry checklist

- [x] Root bundle status read
- [x] Dependencies complete and proof trusted
- [x] Actual repository/branch/head recorded
- [x] Current source and nearby tests inspected
- [x] Test budget understood
- [x] Database/dependency mode recorded

## Work performed

- Added typed immutable state, attempt, and text-delta operation events with stable redacted failures.
- Added an append-only PostgreSQL journal with operation-row sequence locking, bounded replay, migration,
  transfer schema v5, and terminal-only retention.
- Extended the scoped unit of work with post-commit callbacks so local wakes cannot precede commit or
  survive rollback.
- Added a profile/operation-keyed local signal used only as a latency accelerator.
- Connected admission, lease, recovery, cancellation, provider attempt audit, transcript success, and
  terminal failure state to same-transaction event appends.
- Replaced executor use of the completed-only engine path with the provider-neutral stream pipeline and
  removed the concrete `InvokeTurnAsync` bypass.
- Added execution-lease-checked text appends, UTF-8-safe size/natural-boundary/time coalescing, response
  and event-count bounds, partial-output replay, and success-only canonical finalization.
- Added production PostgreSQL and direct Unit proof for sequence concurrency, rollback notification,
  second-context replay, retention, transfer, time flush, redaction, and failure compensation.

## Files changed

Production changes are scoped to LlmChats product/application ports and events, LlmChats.Persistence
runtime/EF/transfer owners, and the PostgreSQL migration assembly. Focused Unit and Integration tests
were updated. Governed proof is under `proof/SB08`.

## Commands and results

- final current-head LLM Chat Unit union: 61 passed, 0 failed, 0 skipped;
- final PostgreSQL journal/turn/transfer union: 7 passed, 0 failed, 0 skipped;
- EF migration model check: no pending model changes;
- CodeAnalytics `snap-20260815060048-09276cd1`: 2 projects, 0 cycles, 0 diagnostics,
  0 blocking/error findings, 0 open questions; one recorded nonblocking pre-existing large-file warning;
- source guards: no completed-only engine/executor path, Web dependency, production partial expansion,
  or stub marker.

Exact commands and results are recorded in `proof/SB08/transcripts` and `proof-manifest.json`.

## Bugs discovered and resolved

- Small deltas initially flushed by elapsed time only when the next provider update arrived. The pipeline
  now races provider enumeration with the coalescing deadline and persists buffered text during pauses.
- Text appends initially tolerated a missing execution lease. The journal now requires and verifies the
  current owner/epoch/expiry under the operation lock.
- The concrete engine still exposed a public completed-only invocation method after the interface and
  executor moved to streaming. The method and constructor dependency were removed; diagnostic tests now
  use the streaming seam.
- `RecoveryRequired` was initially treated as terminal by the event usage invariant. It is nonterminal;
  attempt events retain known usage and actual terminal states carry aggregate usage.
- PostgreSQL fixture seeding initially omitted the transcript parent and used non-hex fingerprints;
  those invalid fixtures were corrected before final proof.

## Deviations

- The normal four focused test-command and three affected-build-command budgets were substantially
  exceeded. Nineteen filtered test attempts were used while resolving compile errors, stale `--no-build`
  output, invalid PostgreSQL fixtures, the time-window implementation, removal of the completed-only
  bypass, and the `RecoveryRequired` invariant found by the final broad LLM Chat filter. No unfiltered
  Unit/Integration or solution-wide test lane ran.
- Four affected project builds were used; the fourth was required to generate and compile the EF
  migration. Final Unit and Integration commands rebuilt all affected source with no reported warnings
  or errors.
- The EF CLI reported that tool version 10.0.3 is older than runtime 10.0.4, but the command completed
  successfully and found no pending model changes. No tool/package version was changed in this bundle.

## Acceptance result

- [x] Every operation event has a unique monotonic sequence within its operation.
- [x] State-transition events commit in the same transaction as their state.
- [x] Text chunks are coalesced and bounded rather than one row per token.
- [x] Partial output is replayable but never canonical unless finalization succeeds.
- [x] A second instance reads all committed events without first-instance memory.
- [x] Event payloads contain no system prompt, user prompt, credential, or raw provider error.

## Architecture result

- [x] Owner moved or strengthened as planned
- [x] Old shallow path removed/unreachable
- [x] Direct tests target the new owner
- [x] No forbidden reference/cycle/partial expansion
- [x] Architecture record updated if design changed

## Progression

Ready. SB09 is unlocked to expose asynchronous admission and SSE replay over the durable event journal.
SB09 must keep database paging authoritative and prove Last-Event-ID, gaps, heartbeats, disconnects,
terminal close, and the shared SSE writer.
