# 08 Process UI Operator Console

## Goal

Make process runs controllable and monitorable from UI.

## Tasks

1. Add a Process Control Center panel with run health, blocked/escalated counts, pending approvals, dead-lettered outbox, and stale attempts.
2. Add Escalation Queue with owner/severity/SLA/status and actions.
3. Add Approval Console with approve/reject/changes-requested and reason capture.
4. Add Rework Console with typed rework packet form.
5. Add Attempt Timeline showing recovery mode, provider/model, finalizer status, structured validation status, tool calls, proof fingerprints, artifacts changed, and retry outcome.
6. Add dead-letter recovery actions where safe.
7. Component tests for core operator flows.

## Acceptance criteria

- A user can understand why a process is stuck and perform the next safe action from UI.
