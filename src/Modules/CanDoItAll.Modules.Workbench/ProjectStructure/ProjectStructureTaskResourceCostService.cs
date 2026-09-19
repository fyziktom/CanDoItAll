using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

/// <summary>
/// Whether a price could be determined for a task resource, as a JSON integer: 0 Available, 1 Unavailable.
/// </summary>
public enum ProjectStructureTaskResourceCostQuoteStatus
{
    Available,
    Unavailable
}

/// <summary>
/// Source the owner used to price a task resource, as a JSON integer: 0 Unknown (no usable source), 1 CrmWorkforceRate
/// (a CRM/HR person's rate), 2 AgentRunHistory, 3 WorkflowRunHistory and 4 ProcessRunHistory (recorded execution cost
/// of the agent, workflow or process).
/// </summary>
public enum ProjectStructureTaskResourceCostSource
{
    Unknown,
    CrmWorkforceRate,
    AgentRunHistory,
    WorkflowRunHistory,
    ProcessRunHistory
}

public static class ProjectStructureTaskResourceCostSourcePolicy
{
    public static ProjectStructureTaskResourceCostSource RequireFor(
        ProjectStructureTaskResourceKind resourceKind)
        => resourceKind switch
        {
            ProjectStructureTaskResourceKind.Person =>
                ProjectStructureTaskResourceCostSource.CrmWorkforceRate,
            ProjectStructureTaskResourceKind.Agent =>
                ProjectStructureTaskResourceCostSource.AgentRunHistory,
            ProjectStructureTaskResourceKind.Workflow =>
                ProjectStructureTaskResourceCostSource.WorkflowRunHistory,
            ProjectStructureTaskResourceKind.Process =>
                ProjectStructureTaskResourceCostSource.ProcessRunHistory,
            _ => throw new ArgumentOutOfRangeException(
                nameof(resourceKind),
                resourceKind,
                "Task resource kind is not defined.")
        };

    public static void Validate(
        ProjectStructureTaskResourceKind resourceKind,
        ProjectStructureTaskResourceCostSource source)
    {
        var expected = RequireFor(resourceKind);
        if (source != expected)
        {
            throw new InvalidOperationException(
                $"Task resource kind '{resourceKind}' requires cost source '{expected}'.");
        }
    }
}

public sealed record ProjectStructureTaskResourceCostRequest(
    Guid ProjectId,
    ProjectStructureTaskResourceSelection Resource,
    ProjectTaskEstimate Estimate);

/// <summary>
/// Price the owner determined for a task resource, used to set the task's expected cost.
/// </summary>
/// <param name="Status">Whether a price was determined, as a JSON integer: 0 Available, 1 Unavailable.</param>
/// <param name="Amount">Expected total cost for the task; null when no price is available.</param>
/// <param name="CurrencyCode">Currency of <c>amount</c>, for example <c>EUR</c>; may be empty when unavailable.</param>
/// <param name="Source">Human-readable description of where the price comes from.</param>
/// <param name="Summary">Human-readable explanation of the price or of why none is available.</param>
/// <param name="CalculatedAtUtc">Instant (with offset) when the price was calculated.</param>
/// <param name="SourceKind">
/// Source used, as a JSON integer: 0 Unknown, 1 CrmWorkforceRate, 2 AgentRunHistory, 3 WorkflowRunHistory,
/// 4 ProcessRunHistory.
/// </param>
public sealed record ProjectStructureTaskResourceCostQuote(
    ProjectStructureTaskResourceCostQuoteStatus Status,
    decimal? Amount,
    string CurrencyCode,
    string Source,
    string Summary,
    DateTimeOffset CalculatedAtUtc,
    ProjectStructureTaskResourceCostSource SourceKind)
{
    /// <summary>True when <c>status</c> is Available and <c>amount</c> has a value.</summary>
    public bool IsAvailable => Status == ProjectStructureTaskResourceCostQuoteStatus.Available && Amount.HasValue;

    public static ProjectStructureTaskResourceCostQuote Unavailable(
        string source,
        string summary,
        DateTimeOffset calculatedAtUtc,
        ProjectStructureTaskResourceCostSource sourceKind)
        => new(
            ProjectStructureTaskResourceCostQuoteStatus.Unavailable,
            null,
            string.Empty,
            source,
            summary,
            calculatedAtUtc,
            sourceKind);
}

public interface IProjectStructureTaskResourceCostStrategy
{
    ProjectStructureTaskResourceKind Kind { get; }

    Task<ProjectStructureTaskResourceCostQuote> GetQuoteAsync(
        ProjectStructureTaskResourceCostRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ProjectStructureTaskResourceCostService
{
    private readonly IReadOnlyDictionary<
        ProjectStructureTaskResourceKind,
        IProjectStructureTaskResourceCostStrategy> strategies;

    public ProjectStructureTaskResourceCostService(
        IEnumerable<IProjectStructureTaskResourceCostStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        this.strategies = BuildStrategyMap(strategies);
    }

    public async Task<ProjectStructureTaskResourceCostQuote> GetQuoteAsync(
        ProjectStructureTaskResourceCostRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProjectId == Guid.Empty)
        {
            throw new ArgumentException("A project is required to estimate task resource cost.", nameof(request));
        }

        ArgumentNullException.ThrowIfNull(request.Resource);
        ProjectStructureTaskResourceSelectionPolicy.Validate(request.Resource);

        var normalizedRequest = request with
        {
            Estimate = ProjectTaskEstimatePolicy.ValidateAndNormalize(request.Estimate)
        };
        if (!strategies.TryGetValue(request.Resource.Kind, out var strategy))
        {
            throw new InvalidOperationException(
                $"No task resource cost strategy is registered for resource kind '{request.Resource.Kind}'.");
        }

        var quote = await strategy.GetQuoteAsync(normalizedRequest, cancellationToken);
        ValidateQuote(normalizedRequest.Resource.Kind, quote);
        return quote;
    }

    private static IReadOnlyDictionary<
        ProjectStructureTaskResourceKind,
        IProjectStructureTaskResourceCostStrategy> BuildStrategyMap(
        IEnumerable<IProjectStructureTaskResourceCostStrategy> strategies)
    {
        var strategyMap = new Dictionary<
            ProjectStructureTaskResourceKind,
            IProjectStructureTaskResourceCostStrategy>();
        foreach (var strategy in strategies)
        {
            ArgumentNullException.ThrowIfNull(strategy);
            if (!Enum.IsDefined(strategy.Kind))
            {
                throw new InvalidOperationException(
                    $"Task resource cost strategy '{strategy.GetType().FullName}' declares unknown resource kind '{strategy.Kind}'.");
            }

            if (!strategyMap.TryAdd(strategy.Kind, strategy))
            {
                throw new InvalidOperationException(
                    $"Multiple task resource cost strategies are registered for resource kind '{strategy.Kind}'.");
            }
        }

        return strategyMap;
    }

    private static void ValidateQuote(
        ProjectStructureTaskResourceKind resourceKind,
        ProjectStructureTaskResourceCostQuote quote)
    {
        ArgumentNullException.ThrowIfNull(quote);
        if (!Enum.IsDefined(quote.Status))
        {
            throw new InvalidOperationException(
                $"Task resource cost strategy returned unknown quote status '{quote.Status}'.");
        }

        ProjectStructureTaskResourceCostSourcePolicy.Validate(resourceKind, quote.SourceKind);
        if (string.IsNullOrWhiteSpace(quote.Source) ||
            string.IsNullOrWhiteSpace(quote.Summary))
        {
            throw new InvalidOperationException(
                "Task resource cost strategy must identify and summarize its authoritative source.");
        }

        if (quote.Status == ProjectStructureTaskResourceCostQuoteStatus.Available &&
            (!quote.Amount.HasValue ||
             quote.Amount.Value < 0m ||
             string.IsNullOrWhiteSpace(quote.CurrencyCode)))
        {
            throw new InvalidOperationException(
                "An available task resource cost quote requires a non-negative amount and currency.");
        }

        if (quote.Status == ProjectStructureTaskResourceCostQuoteStatus.Unavailable &&
            (quote.Amount.HasValue || !string.IsNullOrWhiteSpace(quote.CurrencyCode)))
        {
            throw new InvalidOperationException(
                "An unavailable task resource cost quote cannot contain an amount or currency.");
        }
    }
}
