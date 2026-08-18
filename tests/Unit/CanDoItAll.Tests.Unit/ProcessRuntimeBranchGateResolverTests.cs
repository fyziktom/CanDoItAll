using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessRuntimeBranchGateResolverTests
{
    [Fact]
    public void Resolve_rejects_multiple_distinct_branch_dependencies()
    {
        var step = NewStep(
            new ProcessTemplateDefinitionStepDependencyDocument
            {
                DependsOnStepKey = "first-decision",
                DependsOnBranchOutcomeKey = "accepted"
            },
            new ProcessTemplateDefinitionStepDependencyDocument
            {
                DependsOnStepKey = "second-decision",
                DependsOnBranchOutcomeKey = "approved"
            });

        var result = ProcessRuntimeBranchGateResolver.Resolve(step);

        Assert.False(result.IsSupported);
        Assert.Null(result.BranchGate);
        Assert.Contains("multiple branch-conditioned dependencies", result.Error, StringComparison.Ordinal);
        Assert.Contains("first-decision:accepted", result.Error, StringComparison.Ordinal);
        Assert.Contains("second-decision:approved", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_accepts_one_branch_gate_repeated_by_legacy_and_dependency_fields()
    {
        var step = NewStep(new ProcessTemplateDefinitionStepDependencyDocument
        {
            DependsOnStepKey = "quality-check",
            DependsOnBranchOutcomeKey = "repair-required"
        });
        step.DependsOnStepKey = "quality-check";
        step.DependsOnBranchOutcomeKey = "repair-required";

        var result = ProcessRuntimeBranchGateResolver.Resolve(step);

        Assert.True(result.IsSupported);
        Assert.Null(result.Error);
        Assert.Equal("quality-check", result.BranchGate?.SourceStepKey);
        Assert.Equal("repair-required", result.BranchGate?.RequiredOutcomeKey);
    }

    [Fact]
    public void Dotnet_feature_repair_path_uses_one_branch_gate_per_step()
    {
        var loader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        var definition = loader.LoadDefinition("dotnet-feature-function-implementation");

        Assert.All(definition.Steps, step =>
        {
            var distinctBranchDependencies = ProcessTemplateKernelBuilder.EnumerateDependencies(step)
                .Where(dependency => !string.IsNullOrWhiteSpace(dependency.BranchOutcomeKey))
                .DistinctBy(
                    dependency => $"{dependency.StepKey}\u001f{dependency.BranchOutcomeKey}",
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Assert.True(
                distinctBranchDependencies.Length <= 1,
                $"Step '{step.Key}' declares unsupported branch gates: {string.Join(", ", distinctBranchDependencies.Select(dependency => $"{dependency.StepKey}:{dependency.BranchOutcomeKey}"))}");
        });

        var targetedRecheck = Assert.Single(definition.Steps, step => step.Key == "targeted-recheck");
        var repairedHandoff = Assert.Single(definition.Steps, step => step.Key == "feature-handoff-after-repair");
        Assert.Equal("feature-repair", targetedRecheck.DependsOnStepKey);
        Assert.Equal("feature-repair-applied", targetedRecheck.DependsOnBranchOutcomeKey);
        Assert.Equal("targeted-recheck", repairedHandoff.DependsOnStepKey);
        Assert.Equal("feature-accepted", repairedHandoff.DependsOnBranchOutcomeKey);
    }

    [Fact]
    public void Shipped_process_templates_use_at_most_one_branch_gate_per_step()
    {
        var loader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));

        foreach (var template in loader.Load().Manifest.Processes)
        {
            var definition = loader.LoadDefinition(template.Key);
            Assert.All(definition.Steps, step =>
            {
                var distinctBranchDependencies = ProcessTemplateKernelBuilder.EnumerateDependencies(step)
                    .Where(dependency => !string.IsNullOrWhiteSpace(dependency.BranchOutcomeKey))
                    .DistinctBy(
                        dependency => $"{dependency.StepKey}\u001f{dependency.BranchOutcomeKey}",
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                Assert.True(
                    distinctBranchDependencies.Length <= 1,
                    $"Template '{definition.Key}' step '{step.Key}' declares unsupported branch gates: {string.Join(", ", distinctBranchDependencies.Select(dependency => $"{dependency.StepKey}:{dependency.BranchOutcomeKey}"))}");
            });
        }
    }

    [Fact]
    public void Development_slice_handoff_uses_transitive_accepted_branch_dependency()
    {
        var loader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        var definition = loader.LoadDefinition("dotnet-development-slice");
        var handoff = Assert.Single(definition.Steps, step => step.Key == "slice-handoff");
        var branchDependencies = ProcessTemplateKernelBuilder.EnumerateDependencies(handoff)
            .Where(dependency => !string.IsNullOrWhiteSpace(dependency.BranchOutcomeKey))
            .DistinctBy(
                dependency => $"{dependency.StepKey}\u001f{dependency.BranchOutcomeKey}",
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var branchDependency = Assert.Single(branchDependencies);
        Assert.Equal("add-tests-and-proof", branchDependency.StepKey);
        Assert.Equal("slice-accepted", branchDependency.BranchOutcomeKey);
        Assert.DoesNotContain(
            branchDependencies,
            dependency => dependency.StepKey == "implement-code-change" &&
                          dependency.BranchOutcomeKey == "implementation-ready");
    }

    private static ProcessTemplateDefinitionStepDocument NewStep(
        params ProcessTemplateDefinitionStepDependencyDocument[] dependencies)
        => new()
        {
            Key = "dependent-step",
            Dependencies = dependencies.ToList()
        };

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "Templates", "Processes")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
