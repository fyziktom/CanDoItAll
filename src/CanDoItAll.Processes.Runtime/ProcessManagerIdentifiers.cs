namespace CanDoItAll.Processes.Runtime;

public readonly record struct ProcessManagerWorkItemId
{
    public ProcessManagerWorkItemId(Guid value)
    {
        Value = RuntimeIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ProcessManagerWorkItemId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ProcessIncidentId
{
    public ProcessIncidentId(Guid value)
    {
        Value = RuntimeIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ProcessIncidentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ProcessDiagnosticReferenceId
{
    public ProcessDiagnosticReferenceId(Guid value)
    {
        Value = RuntimeIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ProcessDiagnosticReferenceId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ProcessRecoveryRequestId
{
    public ProcessRecoveryRequestId(Guid value)
    {
        Value = RuntimeIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ProcessRecoveryRequestId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ProcessBranchDecisionRequestId
{
    public ProcessBranchDecisionRequestId(Guid value)
    {
        Value = RuntimeIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ProcessBranchDecisionRequestId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ProcessBranchDecisionId
{
    public ProcessBranchDecisionId(Guid value)
    {
        Value = RuntimeIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ProcessBranchDecisionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ProcessSubprocessMessageId
{
    public ProcessSubprocessMessageId(Guid value)
    {
        Value = RuntimeIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ProcessSubprocessMessageId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ProcessManagerDecisionId
{
    public ProcessManagerDecisionId(Guid value)
    {
        Value = RuntimeIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ProcessManagerDecisionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ProcessManagerIdempotencyKey
{
    public ProcessManagerIdempotencyKey(string value)
    {
        Value = RuntimeIdentifierValidation.RequireToken(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ProcessLoopFingerprintId
{
    public ProcessLoopFingerprintId(string value)
    {
        Value = RuntimeIdentifierValidation.RequireToken(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ProcessEscalationOwnerId
{
    public ProcessEscalationOwnerId(string value)
    {
        Value = RuntimeIdentifierValidation.RequireToken(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}
