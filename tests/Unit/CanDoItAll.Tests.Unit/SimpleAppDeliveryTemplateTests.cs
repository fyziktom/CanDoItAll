using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit;

public sealed class SimpleAppDeliveryTemplateTests
{
    [Fact]
    public void Template_pack_loads_compact_generic_simple_app_lane()
    {
        var loader = new ProcessTemplatePackLoader(
            Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        var pack = loader.Load();
        var summary = Assert.Single(pack.Definitions, definition =>
            definition.Key == "simple-app-delivery");
        var definition = loader.LoadDefinition(summary.Key);

        Assert.Equal("Simple application delivery", definition.DisplayName);
        Assert.Equal(4, definition.RoleUsages.Count);
        Assert.Equal(9, definition.Steps.Count);
        Assert.All(definition.Steps, step => Assert.NotEmpty(step.ResolvedExecutionGuidance));

        Assert.Equal(
            ["implement-application", "repair-findings"],
            definition.Steps
                .Where(step =>
                    step.AllowedOperations.Contains(
                        ProcessOperationContractNames.MutateProductTarget,
                        StringComparer.Ordinal))
                .Select(step => step.Key)
                .ToArray());
        Assert.All(
            definition.Steps.Where(step =>
                step.AllowedOperations.Contains(
                    ProcessOperationContractNames.MutateProductTarget,
                    StringComparer.Ordinal)),
            step => Assert.Equal("simple-app-engineer", step.DecisionRoleKey));

        var contract = definition.Steps.Single(step => step.Key == "resolve-app-profile");
        Assert.Equal(
            ["simple-supported", "specialist-review-required"],
            contract.BranchOutcomes.Select(outcome => outcome.Key).ToArray());
        Assert.Contains(
            "applicationKind as UI, WebApi, Console, or Library",
            Assert.Single(contract.ArtifactExpectations).ValidationRequirementSummary,
            StringComparison.Ordinal);

        AssertValidationStep(
            definition.Steps.Single(step => step.Key == "validate-application"),
            "repair-required");
        AssertValidationStep(
            definition.Steps.Single(step => step.Key == "revalidate-repair"),
            "manager-resolution-required");

        var unresolved = definition.Steps.Single(step => step.Key == "manage-unresolved-delivery");
        Assert.Equal("End", unresolved.StepKind);
        Assert.True(unresolved.RequiresDecisionRecord);
        Assert.True(unresolved.AllowsCompletedOutcomeWithOpenIssues);
        Assert.Equal("simple-app-manager", unresolved.DecisionRoleKey);

        var unsupported = definition.Steps.Single(step => step.Key == "route-unsupported-risk");
        Assert.Equal("specialist-review-required", unsupported.DependsOnBranchOutcomeKey);
        Assert.Equal("simple-app-manager", unsupported.DecisionRoleKey);
        Assert.DoesNotContain(
            ProcessOperationContractNames.MutateProductTarget,
            unsupported.AllowedOperations);
    }

    private static void AssertValidationStep(
        ProcessTemplateDefinitionStepDocument step,
        string nonAcceptanceBranch)
    {
        Assert.Equal("simple-app-qa", step.DecisionRoleKey);
        Assert.Equal(
            ["quality-accepted", nonAcceptanceBranch],
            step.BranchOutcomes.Select(outcome => outcome.Key).ToArray());
        Assert.NotEmpty(step.CapabilityScope.RequiredReceipts);
        Assert.All(
            step.CapabilityScope.RequiredReceipts,
            receipt =>
            {
                Assert.Equal(
                    ProcessRequiredToolReceiptActivation.WhenLaunchContextDeclaresTool,
                    receipt.Activation);
                Assert.Equal(
                    ["quality-accepted"],
                    receipt.ApplicableBranchOutcomeKeys);
            });
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Templates", "Processes")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be located.");
    }
}
