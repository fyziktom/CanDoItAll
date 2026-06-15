namespace CanDoItAll.Processes.Drivers.Abstractions;

public readonly record struct DriverFacetKey
{
    public DriverFacetKey(string value)
    {
        Value = ProcessDriverTokenValidation.Require(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct StrategyBindingInputKey
{
    public StrategyBindingInputKey(string value)
    {
        Value = ProcessDriverTokenValidation.Require(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct StrategyDiagnosticCode
{
    public StrategyDiagnosticCode(string value)
    {
        Value = ProcessDriverTokenValidation.Require(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ManagerSignalCode
{
    public ManagerSignalCode(string value)
    {
        Value = ProcessDriverTokenValidation.Require(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ProcessTemplateFragmentKey
{
    public ProcessTemplateFragmentKey(string value)
    {
        Value = ProcessDriverTokenValidation.Require(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

internal static class ProcessDriverTokenValidation
{
    public static string Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Driver contract token cannot be empty.", parameterName);
        }

        return value.Trim();
    }
}
