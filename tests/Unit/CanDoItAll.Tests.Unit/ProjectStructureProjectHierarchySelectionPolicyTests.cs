using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureProjectHierarchySelectionPolicyTests
{
    [Fact]
    public void Attach_rejects_the_parent_itself()
    {
        var projectId = Guid.NewGuid();

        var canAttach = ProjectStructureProjectHierarchySelectionPolicy.CanAttachProjectAsSubproject(
            projectId,
            projectId,
            []);

        Assert.False(canAttach);
    }

    [Fact]
    public void Attach_rejects_an_existing_direct_child()
    {
        var parentProjectId = Guid.NewGuid();
        var childProjectId = Guid.NewGuid();

        var canAttach = ProjectStructureProjectHierarchySelectionPolicy.CanAttachProjectAsSubproject(
            parentProjectId,
            childProjectId,
            [Link(parentProjectId, childProjectId)]);

        Assert.False(canAttach);
    }

    [Fact]
    public void Attach_rejects_an_ancestor_that_would_create_a_cycle()
    {
        var grandparentProjectId = Guid.NewGuid();
        var intermediateProjectId = Guid.NewGuid();
        var parentProjectId = Guid.NewGuid();

        var canAttach = ProjectStructureProjectHierarchySelectionPolicy.CanAttachProjectAsSubproject(
            parentProjectId,
            grandparentProjectId,
            [
                Link(grandparentProjectId, intermediateProjectId),
                Link(intermediateProjectId, parentProjectId)
            ]);

        Assert.False(canAttach);
    }

    [Fact]
    public void Attach_accepts_an_unrelated_project()
    {
        var existingParentProjectId = Guid.NewGuid();
        var parentProjectId = Guid.NewGuid();
        var candidateChildProjectId = Guid.NewGuid();

        var canAttach = ProjectStructureProjectHierarchySelectionPolicy.CanAttachProjectAsSubproject(
            parentProjectId,
            candidateChildProjectId,
            [Link(existingParentProjectId, parentProjectId)]);

        Assert.True(canAttach);
    }

    [Fact]
    public void Attach_terminates_on_cyclic_input_and_preserves_candidate_rules()
    {
        var parentProjectId = Guid.NewGuid();
        var cyclicMiddleProjectId = Guid.NewGuid();
        var cyclicAncestorProjectId = Guid.NewGuid();
        var unrelatedProjectId = Guid.NewGuid();
        ProjectHierarchyLinkSummary[] cyclicLinks =
        [
            Link(cyclicAncestorProjectId, parentProjectId),
            Link(parentProjectId, cyclicMiddleProjectId),
            Link(cyclicMiddleProjectId, cyclicAncestorProjectId)
        ];

        Assert.False(
            ProjectStructureProjectHierarchySelectionPolicy.CanAttachProjectAsSubproject(
                parentProjectId,
                cyclicAncestorProjectId,
                cyclicLinks));
        Assert.True(
            ProjectStructureProjectHierarchySelectionPolicy.CanAttachProjectAsSubproject(
                parentProjectId,
                unrelatedProjectId,
                cyclicLinks));
    }

    [Fact]
    public void Reconnect_rejects_the_child_itself()
    {
        var projectId = Guid.NewGuid();

        var canReconnect = ProjectStructureProjectHierarchySelectionPolicy.CanReconnectProjectToParent(
            projectId,
            projectId,
            null,
            []);

        Assert.False(canReconnect);
    }

    [Fact]
    public void Reconnect_rejects_the_current_parent()
    {
        var childProjectId = Guid.NewGuid();
        var currentParentProjectId = Guid.NewGuid();

        var canReconnect = ProjectStructureProjectHierarchySelectionPolicy.CanReconnectProjectToParent(
            childProjectId,
            currentParentProjectId,
            currentParentProjectId,
            [Link(currentParentProjectId, childProjectId)]);

        Assert.False(canReconnect);
    }

    [Fact]
    public void Reconnect_rejects_a_descendant_that_would_create_a_cycle()
    {
        var childProjectId = Guid.NewGuid();
        var intermediateProjectId = Guid.NewGuid();
        var descendantProjectId = Guid.NewGuid();

        var canReconnect = ProjectStructureProjectHierarchySelectionPolicy.CanReconnectProjectToParent(
            childProjectId,
            descendantProjectId,
            null,
            [
                Link(childProjectId, intermediateProjectId),
                Link(intermediateProjectId, descendantProjectId)
            ]);

        Assert.False(canReconnect);
    }

    [Fact]
    public void Reconnect_accepts_an_unrelated_parent()
    {
        var childProjectId = Guid.NewGuid();
        var existingChildProjectId = Guid.NewGuid();
        var candidateParentProjectId = Guid.NewGuid();

        var canReconnect = ProjectStructureProjectHierarchySelectionPolicy.CanReconnectProjectToParent(
            childProjectId,
            candidateParentProjectId,
            null,
            [Link(childProjectId, existingChildProjectId)]);

        Assert.True(canReconnect);
    }

    [Fact]
    public void Reconnect_terminates_on_cyclic_input_and_preserves_candidate_rules()
    {
        var childProjectId = Guid.NewGuid();
        var cyclicMiddleProjectId = Guid.NewGuid();
        var cyclicDescendantProjectId = Guid.NewGuid();
        var unrelatedProjectId = Guid.NewGuid();
        ProjectHierarchyLinkSummary[] cyclicLinks =
        [
            Link(childProjectId, cyclicMiddleProjectId),
            Link(cyclicMiddleProjectId, cyclicDescendantProjectId),
            Link(cyclicDescendantProjectId, childProjectId)
        ];

        Assert.False(
            ProjectStructureProjectHierarchySelectionPolicy.CanReconnectProjectToParent(
                childProjectId,
                cyclicDescendantProjectId,
                null,
                cyclicLinks));
        Assert.True(
            ProjectStructureProjectHierarchySelectionPolicy.CanReconnectProjectToParent(
                childProjectId,
                unrelatedProjectId,
                null,
                cyclicLinks));
    }

    private static ProjectHierarchyLinkSummary Link(Guid parentProjectId, Guid childProjectId)
        => new(parentProjectId, childProjectId, DateTimeOffset.UnixEpoch);
}
