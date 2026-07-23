namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectTaskExpectedCostBasis
{
    public ProjectStructureTaskResourceKind ResourceKind { get; set; }

    public Guid ResourceId { get; set; }

    public Guid? ResourceVersionId { get; set; }

    public ProjectStructureTaskResourceCostSource Source { get; set; }

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
