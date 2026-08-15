using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectFileFilterProjectionTests
{
    [Fact]
    public void Create_applies_one_typed_projection_with_deterministic_order_and_fingerprint()
    {
        Guid alphaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid betaId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        DateTimeOffset updated = new(2026, 7, 13, 8, 0, 0, TimeSpan.Zero);
        ProjectSummary alpha = CreateProject(
            alphaId,
            "Alpha",
            ProjectStatus.Active,
            updated,
            [new ProjectPortfolioPartyItem(ProjectPartyPortfolioCategory.Customer, "Customer", "Contoso", true)]);
        ProjectSummary beta = CreateProject(
            betaId,
            "Beta",
            ProjectStatus.Active,
            updated,
            [new ProjectPortfolioPartyItem(ProjectPartyPortfolioCategory.Customer, "Customer", "Contoso", true)]);
        var filter = new ProjectFileFilter(
            search: "discovery",
            status: ProjectStatus.Active,
            relatedPartyCategory: ProjectPartyPortfolioCategory.Customer,
            relatedPartyValue: "contoso");

        ProjectFileFilterProjection first = ProjectFileFilterProjection.Create(
            [beta, alpha],
            [],
            filter);
        ProjectFileFilterProjection second = ProjectFileFilterProjection.Create(
            [alpha, beta],
            [],
            filter);

        Assert.Equal([alphaId, betaId], first.OrderedProjectIds);
        Assert.Equal(first.OrderedProjectIds, second.OrderedProjectIds);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(["Contoso"], first.AvailableRelatedPartyValues);
    }

    [Fact]
    public void Create_builds_cycle_safe_recursive_hierarchy_closure()
    {
        Guid rootId = Guid.NewGuid();
        Guid childId = Guid.NewGuid();
        Guid grandchildId = Guid.NewGuid();
        ProjectSummary root = CreateProject(rootId, "Root", parentCount: 1, childCount: 1);
        ProjectSummary child = CreateProject(childId, "Child", parentCount: 1, childCount: 1);
        ProjectSummary grandchild = CreateProject(grandchildId, "Grandchild", parentCount: 1, childCount: 1);
        ProjectHierarchyLinkSummary[] links =
        [
            Link(rootId, childId),
            Link(childId, grandchildId),
            Link(grandchildId, rootId)
        ];

        ProjectFileFilterProjection projection = ProjectFileFilterProjection.Create(
            [root, child, grandchild],
            links,
            new ProjectFileFilter(hierarchyProjectId: rootId, includeSubprojects: true));

        Assert.Equal(3, projection.Projects.Count);
        Assert.Equal([rootId, childId, grandchildId], projection.OrderedProjectIds.ToHashSet());
    }

    [Fact]
    public void Create_include_subprojects_changes_cards_files_scope_and_fingerprint_together()
    {
        Guid rootId = Guid.NewGuid();
        Guid childId = Guid.NewGuid();
        ProjectSummary root = CreateProject(rootId, "Root", parentCount: 0, childCount: 1);
        ProjectSummary child = CreateProject(childId, "Child", parentCount: 1);
        ProjectHierarchyLinkSummary[] links = [Link(rootId, childId)];

        ProjectFileFilterProjection aggregate = ProjectFileFilterProjection.Create(
            [root, child],
            links,
            new ProjectFileFilter(hierarchyProjectId: rootId, includeSubprojects: true));
        ProjectFileFilterProjection selectedOnly = ProjectFileFilterProjection.Create(
            [root, child],
            links,
            new ProjectFileFilter(hierarchyProjectId: rootId, includeSubprojects: false));

        Assert.Equal(2, aggregate.Projects.Count);
        Assert.Equal([rootId], selectedOnly.OrderedProjectIds);
        Assert.NotEqual(aggregate.Fingerprint, selectedOnly.Fingerprint);
    }

    [Fact]
    public void Create_returns_empty_for_a_stale_hierarchy_selection()
    {
        ProjectSummary project = CreateProject(Guid.NewGuid(), "Current");

        ProjectFileFilterProjection projection = ProjectFileFilterProjection.Create(
            [project],
            [],
            new ProjectFileFilter(hierarchyProjectId: Guid.NewGuid()));

        Assert.Empty(projection.Projects);
    }

    private static ProjectSummary CreateProject(
        Guid id,
        string name,
        ProjectStatus status = ProjectStatus.Active,
        DateTimeOffset? updatedAtUtc = null,
        IReadOnlyList<ProjectPortfolioPartyItem>? relatedParties = null,
        int parentCount = 0,
        int childCount = 0)
        => new(
            id,
            name,
            status,
            "Discovery",
            PhaseCount: 1,
            parentCount,
            childCount,
            updatedAtUtc ?? DateTimeOffset.UtcNow,
            RelatedParties: relatedParties,
            RelatedPartySearchText: string.Join(' ', relatedParties?.Select(party => party.DisplayName) ?? []));

    private static ProjectHierarchyLinkSummary Link(Guid parentId, Guid childId)
        => new(parentId, childId, DateTimeOffset.UtcNow);
}
