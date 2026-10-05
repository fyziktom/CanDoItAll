namespace CanDoItAll.Modules.Workbench;

/// <summary>
/// Unit in which a task's expected effort is entered and displayed. In HTTP request bodies it is a JSON integer:
/// 0 Hours, 1 ManDays. Inside a node's <c>metadataJson</c> string it appears as the text tokens <c>hours</c> and
/// <c>manDays</c>. The unit never changes the stored quantity, which is always in hours.
/// </summary>
public enum ProjectWorkItemEffortUnit
{
    Hours,
    ManDays
}

/// <summary>
/// Expected effort and expected monetary cost of a canonical project task. Effort is always stored in hours, whatever
/// unit is selected, and a null amount means unknown, not zero. The task owner validates and normalizes an estimate
/// before comparing or storing it.
/// </summary>
/// <param name="ExpectedEffortHours">
/// Expected effort in hours, or null when no effort estimate is recorded. A supplied value must be greater than zero.
/// It stays in hours when <c>expectedEffortUnit</c> is ManDays (one man-day is 8 hours by default), so never send an
/// unconverted man-day quantity here.
/// </param>
/// <param name="ExpectedEffortUnit">
/// Unit selected for entering and displaying the effort, as a JSON integer: 0 Hours, 1 ManDays. It does not change the
/// unit of <c>expectedEffortHours</c>.
/// </param>
/// <param name="ExpectedCostAmount">
/// Expected total monetary cost of the task, or null when no cost is recorded. A supplied amount must be from 0
/// through 1,000,000,000,000,000. It is a total estimate, not a rate.
/// </param>
/// <param name="ExpectedCostCurrencyCode">
/// Currency of <c>expectedCostAmount</c>. When an amount is supplied the code is trimmed and upper-cased and must then
/// be exactly three ASCII letters, for example <c>EUR</c>; this is a format check, not a lookup in a currency
/// registry. When the amount is null the owner stores an empty code, so send an empty string in that case.
/// </param>
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
    public const decimal MaximumExpectedCostAmount = 1_000_000_000_000_000m;

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

        if (estimate.ExpectedCostAmount > MaximumExpectedCostAmount)
        {
            throw new InvalidOperationException("Expected task cost exceeds the supported amount range.");
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
