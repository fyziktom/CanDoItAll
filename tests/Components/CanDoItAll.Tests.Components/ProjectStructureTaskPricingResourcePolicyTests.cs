using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectStructureTaskPricingResourcePolicyTests
{
    private const string TaskNodeId = "custom:task-a";

    [Fact]
    public void Removing_direct_assignee_preserves_workflow_cost_basis()
    {
        var workflowId = Guid.NewGuid();
        var workflowVersionId = Guid.NewGuid();
        var resolution = Resolve(CreateAssignment(
            ProjectPartyType.Person,
            isPrimary: true));
        var basis = CreateBasis(
            ProjectStructureTaskResourceKind.Workflow,
            workflowId,
            workflowVersionId,
            ProjectStructureTaskResourceCostSource.WorkflowRunHistory);

        var resource = ProjectStructureTaskPricingResourcePolicy.Resolve(
            directAssigneeChanged: true,
            proposedAssignee: null,
            resolution,
            basis);

        Assert.Equal(
            new ProjectStructureTaskResourceSelection(
                ProjectStructureTaskResourceKind.Workflow,
                workflowId,
                workflowVersionId),
            resource);
    }

    [Fact]
    public void Removing_authoritative_person_returns_no_pricing_resource()
    {
        var person = CreateAssignment(
            ProjectPartyType.Person,
            isPrimary: true);
        var resolution = Resolve(person);
        var basis = CreateBasis(
            ProjectStructureTaskResourceKind.Person,
            person.PartyId,
            null,
            ProjectStructureTaskResourceCostSource.CrmWorkforceRate);

        var resource = ProjectStructureTaskPricingResourcePolicy.Resolve(
            directAssigneeChanged: true,
            proposedAssignee: null,
            resolution,
            basis);

        Assert.Null(resource);
    }

    [Fact]
    public void Removing_legacy_direct_assignee_without_basis_returns_no_pricing_resource()
    {
        var resolution = Resolve(CreateAssignment(
            ProjectPartyType.AiAgent,
            isPrimary: true));

        var resource = ProjectStructureTaskPricingResourcePolicy.Resolve(
            directAssigneeChanged: true,
            proposedAssignee: null,
            resolution,
            costBasis: null);

        Assert.Null(resource);
    }

    [Fact]
    public void Unchanged_mixed_assignment_honors_matching_agent_cost_basis()
    {
        var person = CreateAssignment(
            ProjectPartyType.Person,
            isPrimary: true);
        var agent = CreateAssignment(
            ProjectPartyType.AiAgent,
            isPrimary: false);
        var resolution = Resolve(person, agent);
        var basis = CreateBasis(
            ProjectStructureTaskResourceKind.Agent,
            agent.PartyId,
            null,
            ProjectStructureTaskResourceCostSource.AgentRunHistory);

        var resource = ProjectStructureTaskPricingResourcePolicy.Resolve(
            directAssigneeChanged: false,
            proposedAssignee: resolution.Representative,
            resolution,
            basis);

        Assert.Equal(
            new ProjectStructureTaskResourceSelection(
                ProjectStructureTaskResourceKind.Agent,
                agent.PartyId),
            resource);
    }

    [Fact]
    public void Unchanged_stale_direct_basis_uses_current_representative()
    {
        var person = CreateAssignment(
            ProjectPartyType.Person,
            isPrimary: true);
        var resolution = Resolve(person);
        var staleBasis = CreateBasis(
            ProjectStructureTaskResourceKind.Agent,
            Guid.NewGuid(),
            null,
            ProjectStructureTaskResourceCostSource.AgentRunHistory);

        var resource = ProjectStructureTaskPricingResourcePolicy.Resolve(
            directAssigneeChanged: false,
            proposedAssignee: resolution.Representative,
            resolution,
            staleBasis);

        Assert.Equal(resolution.Representative, resource);
    }

    private static ProjectStructureTaskAssigneeSelectionResult Resolve(
        params ProjectPartyAssignmentDetail[] assignments)
        => ProjectStructureTaskAssigneeSelectionPolicy.Resolve(
            assignments,
            TaskNodeId);

    private static ProjectPartyAssignmentDetail CreateAssignment(
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
            TaskNodeId,
            isPrimary,
            null,
            null,
            null,
            string.Empty,
            string.Empty);

    private static ProjectTaskExpectedCostBasis CreateBasis(
        ProjectStructureTaskResourceKind kind,
        Guid resourceId,
        Guid? versionId,
        ProjectStructureTaskResourceCostSource source)
        => new()
        {
            ResourceKind = kind,
            ResourceId = resourceId,
            ResourceVersionId = versionId,
            Source = source,
            CalculatedAtUtc = DateTimeOffset.Parse("2026-07-23T12:00:00Z")
        };
}
