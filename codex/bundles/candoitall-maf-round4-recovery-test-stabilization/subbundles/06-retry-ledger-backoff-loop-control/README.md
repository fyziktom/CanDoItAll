# 06 — Retry Ledger, Backoff, and Loop Control


## Problem

The retry loop needs a durable ledger to prevent repeated ineffective attempts and to make recovery explainable.

## Tasks

1. Add `AgentAttemptLedger` or `ProcessStepRetryLedger` model.
2. Record attempt id, parent attempt, source execution run id, recovery mode, context strategy, provider id, prompt hash, tool signatures, proof fingerprints, validation errors, and outcome.
3. Implement retry budgets by failure category.
4. Implement backoff for provider/transient failures.
5. Detect repeated identical tool failures, repeated identical file writes, and repeated validation commands without changes.
6. Escalate to human when loop/budget thresholds are exceeded.

## Acceptance criteria

- Every retry/rework attempt has a ledger entry.
- Backoff is applied to transient/provider failures.
- Repeating the same failed command without changing relevant inputs triggers a loop warning or escalation.
- Ledger entries are queryable for audit and prompt construction.

