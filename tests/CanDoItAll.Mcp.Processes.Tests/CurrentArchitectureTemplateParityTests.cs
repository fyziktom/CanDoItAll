using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Mcp.Processes.Tests;

public sealed class CurrentArchitectureTemplateParityTests
{
    [Fact]
    public void Branching_code_review_keeps_explicit_router_and_error_lane()
    {
        var pack = new ProcessTemplatePackLoader().Load();
        var process = pack.Processes["branching-code-review"];

        var router = process.Steps.Single(step => step.Key == "route-review-disposition");
        Assert.Equal("Route code review disposition", router.Title);
        Assert.Equal("review-lead", router.DecisionRoleKey);

        var branchKeys = router.BranchOutcomes.Select(item => item.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("__default__", branchKeys);
        Assert.Contains("__error__", branchKeys);
        Assert.Contains("qa-validation", branchKeys);

        var qaLane = process.Steps.Single(step => step.Key == "validate-qa-lane");
        Assert.Single(qaLane.ArtifactInputs);

        var directMerge = process.Steps.Single(step => step.Key == "approve-merge");
        Assert.Equal("Approve direct merge route", directMerge.Title);
        Assert.Single(directMerge.Dependencies);
        Assert.Equal("route-review-disposition", directMerge.Dependencies[0].DependsOnStepKey);
        Assert.Equal("ready-for-merge", directMerge.Dependencies[0].DependsOnBranchOutcomeKey);

        var qaMerge = process.Steps.Single(step => step.Key == "approve-merge-after-qa");
        Assert.Equal(2, qaMerge.Dependencies.Count);
        Assert.Contains(
            qaMerge.Dependencies,
            dependency => dependency.DependsOnStepKey == "route-review-disposition" &&
                dependency.DependsOnBranchOutcomeKey == "qa-validation");
        Assert.Contains(qaMerge.Dependencies, dependency => dependency.DependsOnStepKey == "validate-qa-lane");
        Assert.Equal(2, qaMerge.ArtifactInputs.Count);

        Assert.Contains(process.Steps, step => step.Key == "approve-merge-after-security");
        Assert.Contains(process.Steps, step => step.Key == "approve-merge-after-architecture");
        Assert.Contains(process.Steps, step => step.Key == "approve-merge-after-default");
    }

    [Fact]
    public void Hotfix_and_software_templates_match_current_dependency_and_artifact_input_expectations()
    {
        var pack = new ProcessTemplatePackLoader().Load();

        var software = pack.Processes["software-delivery"];
        var releaseApproval = software.Steps.Single(step => step.Key == "release-approval");
        Assert.Equal(3, releaseApproval.Dependencies.Count);
        Assert.Equal(3, releaseApproval.ArtifactInputs.Count);

        var hotfix = pack.Processes["hotfix-rollout"];
        var emergencyApproval = hotfix.Steps.Single(step => step.Key == "approve-emergency-release");
        Assert.Equal(2, emergencyApproval.Dependencies.Count);
        Assert.Equal(2, emergencyApproval.ArtifactInputs.Count);
    }

    [Fact]
    public void Ai_assisted_change_delivery_keeps_explicit_delegation_decision_authority()
    {
        var pack = new ProcessTemplatePackLoader().Load();
        var process = pack.Processes["ai-assisted-change-delivery"];

        var delegationDesign = process.Steps.Single(step => step.Key == "delegation-design");
        var safetyReview = process.Steps.Single(step => step.Key == "safety-and-security-review");

        Assert.Equal("solution-architect", delegationDesign.DecisionRoleKey);
        Assert.Equal(2, delegationDesign.BranchOutcomes.Count);
        Assert.Equal("release-approver", safetyReview.DecisionRoleKey);
        Assert.Equal(2, safetyReview.BranchOutcomes.Count);
    }

    [Fact]
    public void Branching_templates_require_explicit_decision_roles()
    {
        var pack = new ProcessTemplatePackLoader().Load();

        var offenders = pack.Processes.Values
            .SelectMany(process => process.Steps
                .Where(step => step.BranchOutcomes.Count > 0 && string.IsNullOrWhiteSpace(step.DecisionRoleKey))
                .Select(step => $"{process.Key}/{step.Key}"))
            .ToList();

        Assert.Empty(offenders);
    }
}
