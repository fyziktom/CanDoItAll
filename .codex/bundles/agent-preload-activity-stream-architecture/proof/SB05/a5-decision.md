# SB05 A5 Backend-to-UI Decision

## Decision

`GO with three P2 follow-ups`

Date: `2026-07-27`

Independent review disposition: `GO`

## Why UI work may proceed

- Typed `Accepted` activity is synchronously observable before catalog, provider,
  persistence, and runtime work.
- The final four-scenario matrix ran five times. All 20 executions preserved the
  required milestone order and operation counts.
- Duplicate catalog/provider/session/summary/run-detail work was removed or replaced
  by one immutable snapshot/acquisition/atomic-start boundary.
- Three scenario medians improved and warm/new was effectively unchanged.
- The wall-clock comparison is descriptive only because the start milestone changed,
  the sample is small, and p95 is the maximum of five noisy local runs.
- Provider query counts are bounded at 0/1/3 for synthetic/warm/changed cases.
- Process enrichment batches selected runs and keeps shared scoped EF work sequential.
- File admission reads remain 11/15 at both 4 and 96 historical runs.
- Generic WAL recovery passed 6/6 and the combined recovery/regression matrix passed
  33/33.
- Activity, process/redaction, and storage groups passed 11/11, 18/18, and 10/10.
- The full serial solution build completed with 0 errors and 166 warnings.
- CodeAnalytics snapshot `snap-20260727233256-654bc9d9` shows an acyclic project graph
  and the prepared dependency direction.

## P2 follow-ups

1. A blocked subscriber can delay the synchronous database-switch notification.
2. WAL proof does not include physical `flushToDisk`/directory durability under power
   loss.
3. Final provider revision validation has an unavoidable current in-memory
   cross-host race without a distributed lease/transaction.

These risks are recorded, bounded, and do not contradict the measured gate. They do
not authorize claims of power-loss durability, globally atomic provider validation,
or non-blocking control-plane notifications.

## Progression and reopen rule

SB06 UI work is authorized. SB05 reopens if UI/browser evidence shows pre-activity
stalling, a real provider run loses correlation, the final snapshot reveals a new
project dependency reversal, or any P2 condition manifests as stale/corrupt user
behavior.
