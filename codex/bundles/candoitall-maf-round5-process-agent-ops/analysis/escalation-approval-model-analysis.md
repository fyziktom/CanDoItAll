# Escalation and Approval Model Analysis

## Current behavior

A step transition to `Blocked` is recorded as `ProcessDecisionKind.Escalation`, `ProcessDecisionOutcome.Escalated`, a journal entry, a conformance observation, and optionally an improvement candidate.

Launch approvals have a more explicit flow. MAF tool approvals are captured in technical execution runs and displayed in the Execution tab.

## Gaps

### Escalation is not a durable control-plane aggregate

Missing fields: escalation id, owner, severity, status, SLA/due date, acknowledgement time, resolution decision, linked execution runs, linked tool receipts, linked QA findings, linked rework packet, and audit trail of actions.

### Approval types are fragmented

There are launch approvals, process step waiting approval, and MAF tool approval requests. They need a common operator-facing model even if storage stays separated.

## Recommended target model

```csharp
public sealed class ProcessEscalation
{
    public required Guid Id { get; init; }
    public required Guid ProcessRunId { get; init; }
    public Guid? StepRunId { get; init; }
    public Guid? ExecutionRunId { get; init; }
    public required ProcessEscalationKind Kind { get; init; }
    public required ProcessEscalationSeverity Severity { get; init; }
    public required ProcessEscalationStatus Status { get; init; }
    public string? Owner { get; init; }
    public DateTimeOffset? DueAtUtc { get; init; }
    public required string Title { get; init; }
    public required string Reason { get; init; }
    public string? Resolution { get; init; }
}
```

Kinds: HumanApprovalRequired, ToolApprovalRequired, AgentOutputInvalid, ProofMissing, ProofFailed, QARejected, RetryBudgetExceeded, OutboxDeadLettered, ProviderUnavailable, PolicyBlocked, ManualOperatorEscalation.
