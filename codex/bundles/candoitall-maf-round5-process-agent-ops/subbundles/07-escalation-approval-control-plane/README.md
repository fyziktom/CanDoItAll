# 07 Escalation and Approval Control Plane

## Goal

Make escalations and approvals first-class operational objects.

## Tasks

1. Add `ProcessEscalation` aggregate/entity or equivalent read/write model.
2. Include kind, severity, status, owner, due date, source run/step/execution/tool receipt, reason, resolution.
3. Map blocked/refused/failed/dead-letter/approval-required/retry-budget-exhausted/tool-policy-blocked events to escalations.
4. Add `IProcessEscalationService`.
5. Add approval control service for MAF tool approvals, process approvals, and launch approvals as a unified operator-facing model.
6. Tests: escalation creation, assignment, resolution, reopen, linked rework packet, approval continuation.

## Acceptance criteria

- Operators have a durable queue of required human actions.
- Escalation resolution is audited and can trigger rework or continuation.
