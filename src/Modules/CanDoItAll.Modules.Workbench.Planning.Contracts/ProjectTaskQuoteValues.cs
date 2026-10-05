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

