using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Mcp.Processes.Tests;

public sealed class ProcessTemplateProjectionServiceTests
{
    [Fact]
    public void GetProjectedEnvelope_clears_definition_ids_and_preserves_current_architecture_counts()
    {
        var loader = new ProcessTemplatePackLoader();
        var projection = new ProcessTemplateProjectionService(loader);

        var projectId = Guid.NewGuid();
        var envelope = projection.GetProjectedEnvelope("software-delivery", projectId, "Projected software delivery");

        Assert.Null(envelope.Definition.Id);
        Assert.Null(envelope.Definition.WorkingVersionId);
        Assert.Equal(projectId, envelope.Definition.ProjectId);
        Assert.Equal("Projected software delivery", envelope.Definition.Name);
        Assert.Equal(7, envelope.Definition.Roles.Count);
        Assert.Equal(9, envelope.Definition.Steps.Count);

        var releaseApproval = envelope.Definition.Steps.Single(step => step.Key == "release-approval");
        Assert.Equal("Approve release readiness", releaseApproval.Title);
        Assert.Equal(4, releaseApproval.Dependencies.Count);
        Assert.Equal(4, releaseApproval.ArtifactInputs.Count);

        var architectureReview = envelope.Definition.Steps.Single(step => step.Key == "architecture-review");
        var qaValidation = envelope.Definition.Steps.Single(step => step.Key == "qa-validation");
        var securityReview = envelope.Definition.Steps.Single(step => step.Key == "security-review");

        Assert.Equal(
            ProcessArtifactTrustRequirement.ReviewRequired,
            architectureReview.ArtifactExpectations.Single(item => item.Title == "Architecture decision record").TrustRequirement);
        Assert.Equal(ProcessArtifactTrustRequirement.ReviewRequired, Assert.Single(qaValidation.ArtifactExpectations).TrustRequirement);
        Assert.Equal(ProcessArtifactTrustRequirement.ReviewRequired, Assert.Single(securityReview.ArtifactExpectations).TrustRequirement);
        Assert.Equal(ProcessArtifactTrustRequirement.HumanApproved, Assert.Single(releaseApproval.ArtifactExpectations).TrustRequirement);
    }

    [Fact]
    public void GetProjectedEnvelope_projects_branching_decision_roles_from_the_canonical_template()
    {
        var loader = new ProcessTemplatePackLoader();
        var projection = new ProcessTemplateProjectionService(loader);

        var envelope = projection.GetProjectedEnvelope("ai-assisted-change-delivery");

        Assert.Equal(9, envelope.Definition.Roles.Count);
        Assert.Equal(8, envelope.Definition.Steps.Count);

        var architect = envelope.Definition.Roles.Single(role => role.Key == "solution-architect");
        var delegationDesign = envelope.Definition.Steps.Single(step => step.Key == "delegation-design");

        Assert.Equal(architect.Id, delegationDesign.DecisionRoleRequirementId);
        Assert.Equal(2, delegationDesign.BranchOutcomes.Count);
    }

    [Fact]
    public void GetCompatibilityReportMarkdown_returns_current_architecture_report()
    {
        var loader = new ProcessTemplatePackLoader();
        var projection = new ProcessTemplateProjectionService(loader);

        var markdown = projection.GetCompatibilityReportMarkdown("branching-code-review");

        Assert.Contains("Compatibility report", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Current-architecture coverage", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sidecar-only fields", markdown, StringComparison.OrdinalIgnoreCase);
    }
}
