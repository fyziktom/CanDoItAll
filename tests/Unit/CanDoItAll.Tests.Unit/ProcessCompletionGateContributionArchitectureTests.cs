namespace CanDoItAll.Tests.Unit;

public sealed class ProcessCompletionGateContributionArchitectureTests
{
    [Fact]
    public void Completion_gate_factory_composes_contributions_without_dotnet_source_inspection_dependency()
    {
        var root = FindRepositoryRoot();
        var factoryPath = Path.Combine(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.Processes",
            "Services",
            "RuntimeIntegration",
            "ProcessCompletionGateFactory.cs");
        var source = File.ReadAllText(factoryPath);

        Assert.Contains("IProcessCompletionGateContribution", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DotNet", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProductSourceInspection", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessProductCompletionPathGate", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessProductRootResolver", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeLifecycleReceipt", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Step_contract_prompt_builder_does_not_embed_domain_specific_source_inspection_policy()
    {
        var root = FindRepositoryRoot();
        var builderPath = Path.Combine(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.Processes",
            "Services",
            "RuntimeIntegration",
            "ProcessStepContractPromptBuilder.cs");
        var source = File.ReadAllText(builderPath);

        Assert.DoesNotContain("ProductSourceInspection", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DotNet", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Source_inspection_completion_policy_is_owned_by_workspace_driver()
    {
        var root = FindRepositoryRoot();
        var runtimeIntegrationRoot = Path.Combine(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.Processes",
            "Services",
            "RuntimeIntegration");
        var workspaceDriverPath = Path.Combine(
            runtimeIntegrationRoot,
            "Drivers",
            "Workspace",
            "WorkspaceProductSourceInspectionCompletionGateContribution.cs");
        var formerCompletionPath = Path.Combine(
            runtimeIntegrationRoot,
            "Completion",
            "ProcessProductSourceInspectionCompletionGateContribution.cs");

        Assert.True(File.Exists(workspaceDriverPath));
        Assert.False(File.Exists(formerCompletionPath));
        var source = File.ReadAllText(workspaceDriverPath);
        Assert.DoesNotContain("Tetris", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Calculator", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Blazor", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Dotnet_solution_context_validation_is_schema_activated_and_kept_in_its_driver()
    {
        var root = FindRepositoryRoot();
        var factoryPath = Path.Combine(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.Processes",
            "Services",
            "RuntimeIntegration",
            "ProcessCompletionGateFactory.cs");
        var driverPath = Path.Combine(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.Processes",
            "Services",
            "RuntimeIntegration",
            "Drivers",
            "DotNet",
            "DotNetSolutionContextCompletionGateContribution.cs");

        Assert.True(File.Exists(driverPath));
        Assert.DoesNotContain("DotNet", File.ReadAllText(factoryPath), StringComparison.Ordinal);
        var source = File.ReadAllText(driverPath);
        Assert.Contains("DotNetSolutionContextParser.Schema", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Tetris", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Calculator", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Blazor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Scaffold", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
