using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureTaskAssigneeSelectionPolicyTests
{
    private const string TaskNodeId = "custom:task-a";

    [Fact]
    public void No_direct_assignment_is_editable_and_has_no_representative()
    {
        var unrelatedAssignment = CreateAssignment(
            "custom:task-b",
            ProjectPartyType.Person,
            isPrimary: true);

        var result = ProjectStructureTaskAssigneeSelectionPolicy.Resolve(
            [unrelatedAssignment],
            TaskNodeId);

        Assert.Equal(ProjectStructureTaskAssigneeSelectionStatus.None, result.Status);
        Assert.Null(result.Representative);
        Assert.Empty(result.DirectAssignments);
        Assert.True(result.CanChangeDirectAssignee);
    }

    [Theory]
    [InlineData(ProjectPartyType.Person, ProjectStructureTaskResourceKind.Person)]
    [InlineData(ProjectPartyType.AiAgent, ProjectStructureTaskResourceKind.Agent)]
    public void One_direct_assignment_is_the_editable_representative(
        ProjectPartyType partyType,
        ProjectStructureTaskResourceKind expectedKind)
    {
        var assignment = CreateAssignment(TaskNodeId, partyType, isPrimary: false);

        var result = ProjectStructureTaskAssigneeSelectionPolicy.Resolve(
            [assignment],
            TaskNodeId);

        Assert.Equal(ProjectStructureTaskAssigneeSelectionStatus.Single, result.Status);
        Assert.Equal(
            new ProjectStructureTaskResourceSelection(expectedKind, assignment.PartyId),
            result.Representative);
        Assert.Single(result.DirectAssignments);
        Assert.True(result.CanChangeDirectAssignee);
    }

    [Fact]
    public void Mixed_assignments_choose_the_unique_primary_independent_of_input_order()
    {
        var person = CreateAssignment(
            TaskNodeId,
            ProjectPartyType.Person,
            isPrimary: false);
        var primaryAgent = CreateAssignment(
            TaskNodeId,
            ProjectPartyType.AiAgent,
            isPrimary: true);

        var forward = ProjectStructureTaskAssigneeSelectionPolicy.Resolve(
            [person, primaryAgent],
            TaskNodeId);
        var reverse = ProjectStructureTaskAssigneeSelectionPolicy.Resolve(
            [primaryAgent, person],
            TaskNodeId);

        Assert.Equal(ProjectStructureTaskAssigneeSelectionStatus.MultipleWithPrimary, forward.Status);
        Assert.Equal(
            new ProjectStructureTaskResourceSelection(
                ProjectStructureTaskResourceKind.Agent,
                primaryAgent.PartyId),
            forward.Representative);
        Assert.Equal(forward.Representative, reverse.Representative);
        Assert.False(forward.CanChangeDirectAssignee);
        Assert.False(reverse.CanChangeDirectAssignee);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Mixed_assignments_without_exactly_one_primary_are_ambiguous(
        bool personIsPrimary,
        bool agentIsPrimary)
    {
        var person = CreateAssignment(
            TaskNodeId,
            ProjectPartyType.Person,
            personIsPrimary);
        var agent = CreateAssignment(
            TaskNodeId,
            ProjectPartyType.AiAgent,
            agentIsPrimary);

        var result = ProjectStructureTaskAssigneeSelectionPolicy.Resolve(
            [person, agent],
            TaskNodeId);

        Assert.Equal(ProjectStructureTaskAssigneeSelectionStatus.Ambiguous, result.Status);
        Assert.Null(result.Representative);
        Assert.Equal(2, result.DirectAssignments.Count);
        Assert.False(result.CanChangeDirectAssignee);
    }

    private static ProjectPartyAssignmentDetail CreateAssignment(
        string nodeKey,
        ProjectPartyType partyType,
        bool isPrimary)
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ProjectPartyAssignmentRole.WorkItemAssignee,
            partyType == ProjectPartyType.Person ? "Joe Doe" : "Delivery agent",
            partyType == ProjectPartyType.Person ? "Person" : "AI agent",
            partyType,
            nodeKey,
            isPrimary,
            null,
            null,
            null,
            string.Empty,
            string.Empty);
}
