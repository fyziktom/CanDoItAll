using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Mcp.Processes.Tests;

public sealed class ProcessTemplateProjectionServiceTests
{
    [Fact]
    public void GetProjectedEnvelope_clears_definition_ids_and_preserves_current_architecture_counts()
    {
        var loader = new ProcessTemplatePackLoader();
        var projection = new ProcessTemplateProjectionService(loader);
        var process = loader.Load().Processes["software-delivery"];

        var projectId = Guid.NewGuid();
        var envelope = projection.GetProjectedEnvelope("software-delivery", projectId, "Projected software delivery");

        Assert.Null(envelope.Definition.Id);
        Assert.Null(envelope.Definition.WorkingVersionId);
        Assert.Equal(projectId, envelope.Definition.ProjectId);
        Assert.Equal("Projected software delivery", envelope.Definition.Name);
        Assert.Equal(process.RoleUsages.Count, envelope.Definition.Roles.Count);
        Assert.Equal(process.Steps.Count, envelope.Definition.Steps.Count);

        var releaseApproval = envelope.Definition.Steps.Single(step => step.Key == "release-approval");
        var releaseApprovalTemplate = process.Steps.Single(step => step.Key == "release-approval");
        Assert.Equal(releaseApprovalTemplate.Title, releaseApproval.Title);
        Assert.Equal(releaseApprovalTemplate.Dependencies.Count, releaseApproval.Dependencies.Count);
        Assert.Equal(releaseApprovalTemplate.ArtifactInputs.Count, releaseApproval.ArtifactInputs.Count);

        var architectureReview = envelope.Definition.Steps.Single(step => step.Key == "architecture-review");
        var qaValidation = envelope.Definition.Steps.Single(step => step.Key == "qa-validation");
        var securityReview = envelope.Definition.Steps.Single(step => step.Key == "security-review");
        var architectureReviewTemplate = process.Steps.Single(step => step.Key == "architecture-review");
        var qaValidationTemplate = process.Steps.Single(step => step.Key == "qa-validation");
        var securityReviewTemplate = process.Steps.Single(step => step.Key == "security-review");

        Assert.Equal(architectureReviewTemplate.ArtifactExpectations.Count, architectureReview.ArtifactExpectations.Count);
        Assert.All(architectureReview.ArtifactExpectations, artifact =>
            Assert.Equal(ProcessArtifactTrustRequirement.ReviewRequired, artifact.TrustRequirement));

        Assert.Equal(qaValidationTemplate.ArtifactExpectations.Count, qaValidation.ArtifactExpectations.Count);
        Assert.All(qaValidation.ArtifactExpectations, artifact =>
            Assert.Equal(ProcessArtifactTrustRequirement.ReviewRequired, artifact.TrustRequirement));

        Assert.Equal(securityReviewTemplate.ArtifactExpectations.Count, securityReview.ArtifactExpectations.Count);
        Assert.All(securityReview.ArtifactExpectations, artifact =>
            Assert.Equal(ProcessArtifactTrustRequirement.ReviewRequired, artifact.TrustRequirement));

        Assert.Equal(releaseApprovalTemplate.ArtifactExpectations.Count, releaseApproval.ArtifactExpectations.Count);
        Assert.All(releaseApproval.ArtifactExpectations, artifact =>
            Assert.Equal(ProcessArtifactTrustRequirement.HumanApproved, artifact.TrustRequirement));
    }

    [Fact]
    public void GetProjectedEnvelope_projects_branching_decision_roles_from_the_canonical_template()
    {
        var loader = new ProcessTemplatePackLoader();
        var projection = new ProcessTemplateProjectionService(loader);
        var process = loader.Load().Processes["ai-assisted-change-delivery"];

        var envelope = projection.GetProjectedEnvelope("ai-assisted-change-delivery");

        Assert.Equal(process.RoleUsages.Count, envelope.Definition.Roles.Count);
        Assert.Equal(process.Steps.Count, envelope.Definition.Steps.Count);

        var architect = envelope.Definition.Roles.Single(role => role.Key == "solution-architect");
        var delegationDesign = envelope.Definition.Steps.Single(step => step.Key == "delegation-design");

        Assert.Equal(architect.Id, delegationDesign.DecisionRoleRequirementId);
        Assert.Equal(2, delegationDesign.BranchOutcomes.Count);
    }

    [Fact]
    public void GetProjectedEnvelope_projects_business_plan_handoffs_and_approval_gate()
    {
        var loader = new ProcessTemplatePackLoader();
        var projection = new ProcessTemplateProjectionService(loader);
        var process = loader.Load().Processes["business-plan-development"];

        var envelope = projection.GetProjectedEnvelope("business-plan-development");

        Assert.Equal(process.RoleUsages.Count, envelope.Definition.Roles.Count);
        Assert.Equal(process.Steps.Count, envelope.Definition.Steps.Count);

        var financialModeling = envelope.Definition.Steps.Single(step => step.Key == "financial-modeling");
        Assert.Single(financialModeling.ArtifactInputs);

        var integratedReview = envelope.Definition.Steps.Single(step => step.Key == "integrated-review");
        Assert.True(integratedReview.RequiresApproval);
        Assert.NotNull(integratedReview.DecisionRoleRequirementId);
        Assert.Equal(3, integratedReview.ArtifactInputs.Count);
        Assert.Equal(2, integratedReview.BranchOutcomes.Count);

        var approvedHandoff = envelope.Definition.Steps.Single(step => step.Key == "approved-execution-handoff");
        var blockedCorrections = envelope.Definition.Steps.Single(step => step.Key == "blocked-correction-plan");
        Assert.Single(approvedHandoff.Dependencies);
        Assert.Single(blockedCorrections.Dependencies);
        Assert.NotNull(approvedHandoff.Dependencies[0].DependsOnBranchOutcomeId);
        Assert.NotNull(blockedCorrections.Dependencies[0].DependsOnBranchOutcomeId);
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
