using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

public interface ILlmChatProjectStructureReportStore
{
    Task<LlmChatProjectStructureReport> QueryProjectStructureReportAsync(
        LlmChatProjectStructureReportQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record LlmChatProjectStructureReportQuery
{
    public const int MaximumPageSize = 100;

    public LlmChatProjectStructureReportQuery(
        IReadOnlyList<Guid> projectIds,
        DateTimeOffset? activityFromUtc,
        DateTimeOffset activityToUtc,
        DateTimeOffset chartFromUtc,
        IReadOnlyList<LlmChatOperationStatus> statuses,
        int pageIndex = 0,
        int pageSize = 20,
        bool includeAggregate = true)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        ArgumentNullException.ThrowIfNull(statuses);
        if (projectIds.Count == 0)
        {
            throw new ArgumentException(
                "At least one project identifier is required for a project-structure Simple Chat report.",
                nameof(projectIds));
        }

        if (projectIds.Any(static projectId => projectId == Guid.Empty))
        {
            throw new ArgumentException(
                "Project-structure Simple Chat report identifiers cannot be empty.",
                nameof(projectIds));
        }

        if (statuses.Any(static status => !Enum.IsDefined(status)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(statuses),
                statuses,
                "The project-structure Simple Chat report contains an unsupported operation status.");
        }

        if (pageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageIndex),
                pageIndex,
                "The project-structure Simple Chat report page index cannot be negative.");
        }

        if (pageSize is < 1 or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                $"The project-structure Simple Chat report page size must be between 1 and {MaximumPageSize}.");
        }

        if (pageIndex > int.MaxValue / pageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageIndex),
                pageIndex,
                "The project-structure Simple Chat report page offset is too large.");
        }

        var normalizedActivityToUtc = activityToUtc.ToUniversalTime();
        var normalizedActivityFromUtc = activityFromUtc?.ToUniversalTime();
        var normalizedChartFromUtc = chartFromUtc.ToUniversalTime();
        if (normalizedActivityFromUtc > normalizedActivityToUtc)
        {
            throw new ArgumentException(
                "The project-structure Simple Chat report activity-from timestamp cannot be later than activity-to.",
                nameof(activityFromUtc));
        }

        if (normalizedChartFromUtc > normalizedActivityToUtc)
        {
            throw new ArgumentException(
                "The project-structure Simple Chat report chart-from timestamp cannot be later than activity-to.",
                nameof(chartFromUtc));
        }

        ProjectIds = projectIds.Distinct().ToArray();
        ActivityFromUtc = normalizedActivityFromUtc;
        ActivityToUtc = normalizedActivityToUtc;
        ChartFromUtc = normalizedChartFromUtc;
        Statuses = statuses.Distinct().ToArray();
        PageIndex = pageIndex;
        PageSize = pageSize;
        IncludeAggregate = includeAggregate;
    }

    public IReadOnlyList<Guid> ProjectIds { get; }

    public DateTimeOffset? ActivityFromUtc { get; }

    public DateTimeOffset ActivityToUtc { get; }

    public DateTimeOffset ChartFromUtc { get; }

    public IReadOnlyList<LlmChatOperationStatus> Statuses { get; }

    public int PageIndex { get; }

    public int PageSize { get; }

    public bool IncludeAggregate { get; }
}

public sealed record LlmChatProjectStructureReport(
    IReadOnlyList<LlmChatProjectStructureReportRun> Runs,
    int PageIndex,
    int PageSize,
    int TotalCount,
    decimal KnownCostUsd,
    int UnknownCostRunCount,
    long TotalDurationMilliseconds,
    IReadOnlyList<LlmChatProjectStructureDailyCost> DailyCost);

public sealed record LlmChatProjectStructureReportRun(
    LlmChatOperationId OperationId,
    LlmChatConversationId ConversationId,
    LlmChatDefinitionId DefinitionId,
    int DefinitionRevision,
    LlmChatOperationStatus Status,
    string ConversationTitle,
    string DefinitionName,
    string ProviderName,
    string Model,
    DateTimeOffset ActivityAtUtc,
    long DurationMilliseconds,
    decimal KnownCostUsd,
    bool HasUnknownCost);

public sealed record LlmChatProjectStructureDailyCost(
    DateOnly Date,
    decimal KnownCostUsd);
