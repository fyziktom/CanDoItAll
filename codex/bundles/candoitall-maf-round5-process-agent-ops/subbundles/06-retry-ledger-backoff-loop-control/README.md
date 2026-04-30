# 06 Retry Ledger, Backoff, and Loop Control

## Goal

Prevent infinite or wasteful agent retry loops.

## Tasks

1. Add durable retry ledger entries with attempt, mode, provider/model, context strategy, reason, proof validity, outcome, and timestamps.
2. Implement backoff for repeated automation/outbox retries.
3. Detect repeated ineffective attempts with same failure category and no artifact/proof delta.
4. Escalate when retry budget is exhausted or repeated loop is detected.
5. Show retry ledger in UI attempt timeline.
6. Tests: retry budget exhaustion creates escalation; repeated same failure escalates; successful rework resets relevant loop state.

## Acceptance criteria

- Automation does not spin indefinitely.
- Operators can see why retries happened and why they stopped.
