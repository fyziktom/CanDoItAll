using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectTaskExpectedCostBasisPolicyTests
{
    private static readonly Guid ResourceId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void Valid_person_basis_round_trips_to_resource_selection()
    {
        var basis = new ProjectTaskExpectedCostBasis
        {
            ResourceKind = ProjectStructureTaskResourceKind.Person,
            ResourceId = ResourceId,
            Source = ProjectStructureTaskResourceCostSource.CrmWorkforceRate,
            CalculatedAtUtc = DateTimeOffset.UnixEpoch
        };

        ProjectTaskExpectedCostBasisPolicy.Validate(basis);
        var resource = ProjectTaskExpectedCostBasisPolicy.ToResource(basis);

        Assert.Equal(ProjectStructureTaskResourceKind.Person, resource.Kind);
        Assert.Equal(ResourceId, resource.ResourceId);
        Assert.Null(resource.VersionId);
    }

    [Fact]
    public void Non_workflow_basis_rejects_version()
    {
        var basis = new ProjectTaskExpectedCostBasis
        {
            ResourceKind = ProjectStructureTaskResourceKind.Agent,
            ResourceId = ResourceId,
            ResourceVersionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Source = ProjectStructureTaskResourceCostSource.AgentRunHistory,
            CalculatedAtUtc = DateTimeOffset.UnixEpoch
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProjectTaskExpectedCostBasisPolicy.Validate(basis));

        Assert.Contains("does not support a version", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ProjectStructureTaskResourceCostSource.Unknown)]
    [InlineData((ProjectStructureTaskResourceCostSource)999)]
    public void Basis_rejects_unknown_cost_source(
        ProjectStructureTaskResourceCostSource source)
    {
        var basis = new ProjectTaskExpectedCostBasis
        {
            ResourceKind = ProjectStructureTaskResourceKind.Process,
            ResourceId = ResourceId,
            Source = source,
            CalculatedAtUtc = DateTimeOffset.UnixEpoch
        };

        Assert.Throws<InvalidOperationException>(() =>
            ProjectTaskExpectedCostBasisPolicy.Validate(basis));
    }

    [Fact]
    public void Person_basis_rejects_agent_history_source()
    {
        var basis = new ProjectTaskExpectedCostBasis
        {
            ResourceKind = ProjectStructureTaskResourceKind.Person,
            ResourceId = ResourceId,
            Source = ProjectStructureTaskResourceCostSource.AgentRunHistory,
            CalculatedAtUtc = DateTimeOffset.UnixEpoch
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProjectTaskExpectedCostBasisPolicy.Validate(basis));

        Assert.Contains("requires cost source", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
