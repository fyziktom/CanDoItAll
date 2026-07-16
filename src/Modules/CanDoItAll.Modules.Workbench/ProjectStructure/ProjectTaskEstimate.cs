namespace CanDoItAll.Modules.Workbench;

public enum ProjectWorkItemEffortUnit
{
    Hours,
    ManDays
}

public sealed record ProjectTaskEstimate(
    decimal? ExpectedEffortHours,
    ProjectWorkItemEffortUnit ExpectedEffortUnit,
    decimal? ExpectedCostAmount,
    string ExpectedCostCurrencyCode)
{
    public static ProjectTaskEstimate Empty(string currencyCode = "")
        => new(null, ProjectWorkItemEffortUnit.Hours, null, currencyCode);
}

public static class ProjectTaskEstimateInputKeys
{
    public const string ExpectedEffortValue = "expectedEffortValue";
    public const string ExpectedEffortUnit = "expectedEffortUnit";
    public const string ExpectedCostAmount = "expectedCostAmount";
    public const string ExpectedCostCurrencyCode = "expectedCostCurrencyCode";
}

public static class ProjectTaskEstimatePolicy
{
    public const decimal DefaultHoursPerManDay = 8m;

    private static readonly decimal MaximumExpectedEffortHours = (decimal)TimeSpan.MaxValue.TotalHours;

    public static ProjectTaskEstimate Create(
        decimal? expectedEffortValue,
        ProjectWorkItemEffortUnit expectedEffortUnit,
        decimal? expectedCostAmount,
        string? expectedCostCurrencyCode,
        decimal hoursPerManDay = DefaultHoursPerManDay)
    {
        EnsureHoursPerManDay(hoursPerManDay);
        decimal? expectedEffortHours = expectedEffortValue.HasValue
            ? ToHours(expectedEffortValue.Value, expectedEffortUnit, hoursPerManDay)
            : null;
        return ValidateAndNormalize(
            new ProjectTaskEstimate(
                expectedEffortHours,
                expectedEffortUnit,
                expectedCostAmount,
                expectedCostCurrencyCode ?? string.Empty),
            hoursPerManDay);
    }

    public static ProjectTaskEstimate ValidateAndNormalize(
        ProjectTaskEstimate estimate,
        decimal hoursPerManDay = DefaultHoursPerManDay)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        EnsureHoursPerManDay(hoursPerManDay);
        EnsureDefinedUnit(estimate.ExpectedEffortUnit);

        if (estimate.ExpectedEffortHours is <= 0m)
        {
            throw new InvalidOperationException("Expected task effort must be greater than zero.");
        }

        if (estimate.ExpectedEffortHours > MaximumExpectedEffortHours)
        {
            throw new InvalidOperationException("Expected task effort exceeds the supported duration range.");
        }

        if (estimate.ExpectedCostAmount is < 0m)
        {
            throw new InvalidOperationException("Expected task cost cannot be negative.");
        }

        var normalizedCurrencyCode = NormalizeCurrencyCode(
            estimate.ExpectedCostAmount,
            estimate.ExpectedCostCurrencyCode);
        return estimate with
        {
            ExpectedCostCurrencyCode = normalizedCurrencyCode
        };
    }

    public static decimal ToHours(
        decimal value,
        ProjectWorkItemEffortUnit unit,
        decimal hoursPerManDay = DefaultHoursPerManDay)
    {
        EnsureHoursPerManDay(hoursPerManDay);
        EnsureDefinedUnit(unit);
        return unit switch
        {
            ProjectWorkItemEffortUnit.Hours => value,
            ProjectWorkItemEffortUnit.ManDays => checked(value * hoursPerManDay),
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported task effort unit.")
        };
    }

    public static decimal FromHours(
        decimal hours,
        ProjectWorkItemEffortUnit unit,
        decimal hoursPerManDay = DefaultHoursPerManDay)
    {
        EnsureHoursPerManDay(hoursPerManDay);
        EnsureDefinedUnit(unit);
        return unit switch
        {
            ProjectWorkItemEffortUnit.Hours => hours,
            ProjectWorkItemEffortUnit.ManDays => hours / hoursPerManDay,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported task effort unit.")
        };
    }

    public static decimal? ToInputValue(
        ProjectTaskEstimate estimate,
        decimal hoursPerManDay = DefaultHoursPerManDay)
    {
        var normalized = ValidateAndNormalize(estimate, hoursPerManDay);
        return normalized.ExpectedEffortHours.HasValue
            ? FromHours(normalized.ExpectedEffortHours.Value, normalized.ExpectedEffortUnit, hoursPerManDay)
            : null;
    }

    private static string NormalizeCurrencyCode(decimal? expectedCostAmount, string? currencyCode)
    {
        if (!expectedCostAmount.HasValue)
        {
            return string.Empty;
        }

        var normalized = currencyCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length != 3 || normalized.Any(character => character is < 'A' or > 'Z'))
        {
            throw new InvalidOperationException("Expected task cost requires a three-letter currency code.");
        }

        return normalized;
    }

    private static void EnsureHoursPerManDay(decimal hoursPerManDay)
    {
        if (hoursPerManDay <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hoursPerManDay),
                hoursPerManDay,
                "Hours per man-day must be greater than zero.");
        }
    }

    private static void EnsureDefinedUnit(ProjectWorkItemEffortUnit unit)
    {
        if (!Enum.IsDefined(unit))
        {
            throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported task effort unit.");
        }
    }
}
