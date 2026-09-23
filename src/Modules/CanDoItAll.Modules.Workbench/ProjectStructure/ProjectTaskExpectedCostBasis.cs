namespace CanDoItAll.Modules.Workbench;

/// <summary>
/// Owner-calculated basis of a task's expected cost: which resource was priced, from which cost source and when.
/// Clients copy it unchanged from a read; they never compose or edit it.
/// </summary>
public sealed record ProjectTaskExpectedCostBasis
{
    /// <summary>Kind of the priced resource, as a JSON integer: 0 Person, 1 Agent, 2 Workflow, 3 Process.</summary>
    public ProjectStructureTaskResourceKind ResourceKind { get; set; }

    /// <summary>Identifier of the priced person, agent, workflow or process definition.</summary>
    public Guid ResourceId { get; set; }

    /// <summary>
    /// Exact workflow version that was priced. Present only when <c>resourceKind</c> is Workflow; null otherwise.
    /// </summary>
    public Guid? ResourceVersionId { get; set; }

    /// <summary>
    /// Cost source the owner used, as a JSON integer: 1 CrmWorkforceRate, 2 AgentRunHistory, 3 WorkflowRunHistory,
    /// 4 ProcessRunHistory. 0 Unknown is never valid in a stored basis.
    /// </summary>
    public ProjectStructureTaskResourceCostSource Source { get; set; }

    /// <summary>Instant the owner calculated the expected cost. Always present in a stored basis.</summary>
    public DateTimeOffset? CalculatedAtUtc { get; set; }
}

public static class ProjectTaskExpectedCostBasisPolicy
{
    public static void Validate(ProjectTaskExpectedCostBasis? basis)
    {
        if (basis is null)
        {
            return;
        }

        if (!Enum.IsDefined(basis.ResourceKind))
        {
            throw new InvalidOperationException(
                $"Task expected-cost resource kind '{basis.ResourceKind}' is not defined.");
        }

        if (basis.ResourceId == Guid.Empty)
        {
            throw new InvalidOperationException("Task expected-cost basis requires a resource id.");
        }

        if (basis.ResourceVersionId == Guid.Empty)
        {
            throw new InvalidOperationException("Task expected-cost resource version id cannot be empty.");
        }

        if (basis.ResourceKind != ProjectStructureTaskResourceKind.Workflow &&
            basis.ResourceVersionId.HasValue)
        {
            throw new InvalidOperationException(
                $"Task expected-cost resource kind '{basis.ResourceKind}' does not support a version id.");
        }

        if (basis.ResourceKind == ProjectStructureTaskResourceKind.Workflow &&
            !basis.ResourceVersionId.HasValue)
        {
            throw new InvalidOperationException(
                "Task expected-cost workflow basis requires an exact resource version id.");
        }

        if (!Enum.IsDefined(basis.Source) ||
            basis.Source == ProjectStructureTaskResourceCostSource.Unknown)
        {
            throw new InvalidOperationException("Task expected-cost basis requires a known cost source.");
        }

        if (!basis.CalculatedAtUtc.HasValue)
        {
            throw new InvalidOperationException("Task expected-cost basis requires a calculation timestamp.");
        }

        ProjectStructureTaskResourceCostSourcePolicy.Validate(
            basis.ResourceKind,
            basis.Source);
    }

    public static ProjectTaskExpectedCostBasis Create(
        ProjectStructureTaskResourceSelection resource,
        ProjectStructureTaskResourceCostQuote quote)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(quote);
        ProjectStructureTaskResourceSelectionPolicy.Validate(resource);
        var basis = new ProjectTaskExpectedCostBasis
        {
            ResourceKind = resource.Kind,
            ResourceId = resource.ResourceId,
            ResourceVersionId = resource.VersionId,
            Source = quote.SourceKind,
            CalculatedAtUtc = quote.CalculatedAtUtc
        };
        Validate(basis);
        return basis;
    }

    public static ProjectStructureTaskResourceSelection ToResource(
        ProjectTaskExpectedCostBasis basis)
    {
        ArgumentNullException.ThrowIfNull(basis);
        Validate(basis);
        return new ProjectStructureTaskResourceSelection(
            basis.ResourceKind,
            basis.ResourceId,
            basis.ResourceVersionId);
    }
}
