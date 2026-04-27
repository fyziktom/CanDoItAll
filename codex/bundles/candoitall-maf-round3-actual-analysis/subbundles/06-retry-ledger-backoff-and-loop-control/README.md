# 06 - Retry Ledger, Backoff, and Loop Control

## Problem

Retries are bounded, but there is no durable, typed ledger explaining why attempts happened and when recovery workers should retry.

## Required implementation

Create a retry/recovery ledger keyed by process run id + step run id.

Track:

- attempt number;
- recovery mode;
- failure category;
- failure signature hash;
- source execution run id;
- provider/model id;
- provider fallback count;
- rework packet id;
- next attempt time;
- terminal escalation reason.

## Acceptance criteria

- Identical repeated failure signatures escalate rather than loop.
- Provider fallback budget is separate from normal attempts.
- Recovery worker respects `nextAttemptAtUtc`.
- UI/logs can show retry history.

## Tests

- Three identical failures escalate.
- Provider fallback count is bounded independently.
- Runtime recovery scan skips until next attempt time.

## Execution status

Completed. Recovery ledger entries track signatures, provider fallback budgets, packet refs, and backoff; recovery worker skips attempts while backoff is active.
