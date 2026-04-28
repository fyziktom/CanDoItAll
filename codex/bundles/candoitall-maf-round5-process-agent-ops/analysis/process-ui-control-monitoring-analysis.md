# Process UI Control and Monitoring Analysis

## Current strengths

The process workspace has a useful split between canonical process state and technical execution state: Launch, Activity, Execution, Coordination, and Evidence tabs. The lifecycle section shows health metrics and basic operator actions.

## Current gaps

### Manual rerun is too blunt

The UI calls `RerunAgentStepAsync(...)` with a fixed reason. Operators cannot specify exact repair instructions, expected delta, QA findings to address, artifacts to inspect, proof receipts to reuse or invalidate, severity/SLA, or assignment/owner.

### Approval controls are not first-class in process UI

Execution approvals are displayed, but the UI does not provide a direct approve/reject/changes-requested flow in the process workspace.

### Escalations are not first-class

Blocked/refused/failed transitions create decision records and observations, but there is no operator queue with owner, status, severity, SLA/overdue, related execution run/tool receipts, resolution action, or start-rework action.

### Monitoring lacks attempt-level comparison

Operators need an attempt timeline with attempt number, provider/model, session strategy, recovery mode, finalizer status, structured output validation status, tools called, proof fingerprints, artifacts changed, invalidation reason, elapsed time, and token usage.

## Recommended UI additions

1. Process Control Center.
2. Step Attempt Timeline.
3. Rework Console.
4. Approval Console.
5. Escalation Queue.
