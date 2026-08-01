using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public sealed record WorkflowCatalogSearchQuery
{
    public const int DefaultPageSize = 25;
    public const int MaximumPageSize = 100;

    public WorkflowCatalogSearchQuery(
        string? text = null,
        WorkflowLifecycleStatus? status = null,
        int pageIndex = 0,
        int pageSize = DefaultPageSize)
    {
        if (pageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), "Page index cannot be negative.");
        }

        if (pageSize is < 1 or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                $"Page size must be between 1 and {MaximumPageSize}.");
        }

        if ((long)pageIndex * pageSize > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), "Page offset exceeds the supported range.");
        }

        if (status.HasValue && !Enum.IsDefined(status.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Workflow lifecycle status is not defined.");
        }

        Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        Status = status;
        PageIndex = pageIndex;
        PageSize = pageSize;
    }

    public string? Text { get; }

    public WorkflowLifecycleStatus? Status { get; }

    public int PageIndex { get; }

    public int PageSize { get; }

    public int Offset => PageIndex * PageSize;
}

public sealed record WorkflowCatalogSearchPage(
    IReadOnlyList<WorkflowCatalogItem> Items,
    int PageIndex,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : ((TotalCount - 1) / PageSize) + 1;
}

public interface IWorkflowCatalogSearchService
{
    Task<WorkflowCatalogSearchPage> SearchDefinitionsAsync(
        WorkflowCatalogSearchQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowCatalogLookupQuery
{
    public const int MaximumWorkflowCount = 100;

    public WorkflowCatalogLookupQuery(IReadOnlyCollection<WorkflowId> workflowIds)
    {
        ArgumentNullException.ThrowIfNull(workflowIds);
        var distinctWorkflowIds = workflowIds
            .Distinct()
            .ToArray();
        if (distinctWorkflowIds.Length > MaximumWorkflowCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(workflowIds),
                distinctWorkflowIds.Length,
                $"Workflow catalog lookup count cannot exceed {MaximumWorkflowCount}.");
        }

        WorkflowIds = distinctWorkflowIds;
    }

    public IReadOnlyList<WorkflowId> WorkflowIds { get; }
}

public interface IWorkflowCatalogLookupService
{
    Task<IReadOnlyList<WorkflowCatalogItem>> LookupDefinitionsAsync(
        WorkflowCatalogLookupQuery query,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowStableIdentityLookupService
{
    Task<WorkflowStableIdentityResolution> ResolveByTemplateKeyAsync(
        string templateKey,
        CancellationToken cancellationToken = default);

    Task<WorkflowStableIdentityResolution> ResolveByExternalKeyAsync(
        string externalNamespace,
        string externalKey,
        CancellationToken cancellationToken = default);
}
