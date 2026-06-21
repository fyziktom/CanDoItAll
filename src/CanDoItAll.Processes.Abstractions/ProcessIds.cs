namespace CanDoItAll.Processes.Abstractions;

public readonly record struct ProcessDefinitionId
{
    public ProcessDefinitionId(Guid value)
    {
        Value = ProcessIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ProcessDefinitionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ProcessDefinitionVersionId
{
    public ProcessDefinitionVersionId(Guid value)
    {
        Value = ProcessIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ProcessDefinitionVersionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ProcessInstancePlanId
{
    public ProcessInstancePlanId(Guid value)
    {
        Value = ProcessIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ProcessInstancePlanId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ProcessInstanceId
{
    public ProcessInstanceId(Guid value)
    {
        Value = ProcessIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ProcessInstanceId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ProcessRunId
{
    public ProcessRunId(Guid value)
    {
        Value = ProcessIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ProcessRunId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ProcessStepId
{
    public ProcessStepId(Guid value)
    {
        Value = ProcessIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ProcessStepId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ProcessStepDefinitionId
{
    public ProcessStepDefinitionId(Guid value)
    {
        Value = ProcessIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ProcessStepDefinitionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ProcessStepInstanceId
{
    public ProcessStepInstanceId(Guid value)
    {
        Value = ProcessIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ProcessStepInstanceId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ProcessArtifactId
{
    public ProcessArtifactId(Guid value)
    {
        Value = ProcessIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ProcessArtifactId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ArtifactDefinitionId
{
    public ArtifactDefinitionId(Guid value)
    {
        Value = ProcessIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ArtifactDefinitionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ArtifactSlotId
{
    public ArtifactSlotId(Guid value)
    {
        Value = ProcessIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ArtifactSlotId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ArtifactInstanceId
{
    public ArtifactInstanceId(Guid value)
    {
        Value = ProcessIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ArtifactInstanceId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct RuntimeEventId
{
    public RuntimeEventId(Guid value)
    {
        Value = ProcessIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static RuntimeEventId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ProcessTemplateId
{
    public ProcessTemplateId(Guid value)
    {
        Value = ProcessIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ProcessTemplateId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct TemplateComponentId
{
    public TemplateComponentId(Guid value)
    {
        Value = ProcessIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static TemplateComponentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct DriverId
{
    public DriverId(string value)
    {
        Value = ProcessIdentifierValidation.RequireToken(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct StrategyId
{
    public StrategyId(string value)
    {
        Value = ProcessIdentifierValidation.RequireToken(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct CapabilityTag
{
    public CapabilityTag(string value)
    {
        Value = ProcessIdentifierValidation.RequireToken(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct BranchFamilyId
{
    public BranchFamilyId(string value)
    {
        Value = ProcessIdentifierValidation.RequireToken(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct BranchOutcomeId
{
    public BranchOutcomeId(string value)
    {
        Value = ProcessIdentifierValidation.RequireToken(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct LoopFingerprintPolicyId
{
    public LoopFingerprintPolicyId(string value)
    {
        Value = ProcessIdentifierValidation.RequireToken(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ProcessCorrelationId
{
    public ProcessCorrelationId(string value)
    {
        Value = ProcessIdentifierValidation.RequireToken(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ProcessActorId
{
    public ProcessActorId(string value)
    {
        Value = ProcessIdentifierValidation.RequireToken(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ProcessEventType
{
    public ProcessEventType(string value)
    {
        Value = ProcessIdentifierValidation.RequireToken(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

internal static class ProcessIdentifierValidation
{
    public static Guid RequireGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Process identifier cannot be empty.", parameterName);
        }

        return value;
    }

    public static string RequireToken(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Process token cannot be empty.", parameterName);
        }

        return value.Trim();
    }
}
