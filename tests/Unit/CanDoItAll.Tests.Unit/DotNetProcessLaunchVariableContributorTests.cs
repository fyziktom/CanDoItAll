using System.Text.Json;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit;

public sealed class DotNetProcessLaunchVariableContributorTests
{
    [Fact]
    public void Enrich_does_not_create_dotnet_facts_when_no_driver_is_declared()
    {
        var contributor = new DotNetProcessLaunchVariableContributor();
        var context = new ProcessLaunchPreparationContext(
            "generic-process",
            IsSubprocess: false,
            Source("Build a Blazor PWA with xUnit tests in src and tests."));
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = @"C:\output\AnyProduct",
            ["ProjectStructureContextSummary"] = "Blazor PWA net10 xUnit src tests"
        };

        contributor.Enrich(context, variables);

        Assert.DoesNotContain(variables.Keys, key => key.StartsWith("DotNet", StringComparison.Ordinal));
    }

    [Fact]
    public void Enrich_rejects_solution_setup_without_the_declared_solution_context_binding()
    {
        var contributor = new DotNetProcessLaunchVariableContributor();
        var context = new ProcessLaunchPreparationContext(
            "dotnet-solution-setup",
            IsSubprocess: true,
            Source("Blazor PWA net10 xUnit src tests"))
        {
            DriverActivations =
            [
                new ProcessLaunchDriverActivation(
                    "dotnet.launch-contract",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Mode"] = "solution-setup"
                    })
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => contributor.Enrich(
            context,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProductRoot"] = @"C:\output\AnyProduct"
            }));

        Assert.Contains("input artifact binding", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Preparation_service_does_not_activate_the_dotnet_driver_for_root_delivery_templates()
    {
        var service = new ProcessLaunchVariablePreparationService(
            [new DotNetProcessLaunchVariableContributor()],
            new ProcessTemplatePackLoader());
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = @"C:\output\AnyProduct",
            ["ProjectStructureContextSummary"] = "Blazor PWA net10 xUnit src tests"
        };

        service.Enrich(
            new ProcessLaunchPreparationContext("software-delivery", IsSubprocess: false, Source("Blazor PWA net10 xUnit src tests")),
            variables);

        Assert.DoesNotContain(variables.Keys, key => key.StartsWith("DotNet", StringComparison.Ordinal));
    }

    [Fact]
    public void Template_declares_one_explicit_solution_context_binding_for_solution_setup()
    {
        var definition = new ProcessTemplatePackLoader().LoadDefinition("dotnet-solution-setup");
        var activation = Assert.Single(definition.LaunchDriverActivations);
        var binding = Assert.Single(activation.InputArtifactBindings);

        Assert.Equal("dotnet.launch-contract", activation.DriverKey);
        Assert.Equal("solution-setup", activation.Settings["Mode"]);
        Assert.Equal("solution-context", binding.BindingKey);
        Assert.Equal("slice-architecture-check", binding.SourceStepKey);
        Assert.Equal("dotnet-solution-context", binding.ArtifactExpectationKey);
        Assert.Equal(DotNetSolutionContextParser.Schema, binding.PayloadSchema);

        var configuredActivation = new ProcessLaunchDriverActivation(
            activation.DriverKey,
            activation.Settings);
        Assert.True(
            DotNetSolutionSetupTemplatePolicyBindings.TryParse(
                configuredActivation,
                out _,
                out var issue),
            issue);
    }

    [Fact]
    public void Template_owned_setup_policy_accepts_arbitrary_step_keys_without_driver_choreography()
    {
        var activation = new ProcessLaunchDriverActivation(
            "dotnet.launch-contract",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [DotNetSolutionSetupTemplatePolicyBindings.RequiredPathsSettingKey] =
                    """{"establish-custom-baseline":["${DotNetSolutionFileForwardSlash}"]}""",
                [DotNetSolutionSetupTemplatePolicyBindings.RequiredToolReceiptsSettingKey] =
                    """{"establish-custom-baseline":["workspace_pwsh_run_script"]}""",
                [DotNetSolutionSetupTemplatePolicyBindings.RequiredFileContentChecksSettingKey] =
                    """{"establish-custom-baseline":[{"pathCandidates":["${DotNetSolutionFileForwardSlash}"],"requiredTextAnyGroups":[["src/Custom/Custom.csproj"]]}]}""",
                [DotNetSolutionSetupTemplatePolicyBindings.ScopedLaunchVariablePrefixesSettingKey] =
                    """{"establish-custom-baseline":["DotNetCustom"]}"""
            });

        var parsed = DotNetSolutionSetupTemplatePolicyBindings.TryParse(
            activation,
            out var bindings,
            out var issue);

        Assert.True(parsed, issue);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DotNetSolutionFileForwardSlash"] = "C:/output/Custom/Custom.slnx"
        };
        bindings.ApplyTo(variables);
        var resolved = new LaunchVariableTemplateResolver().Resolve(variables);

        using var document = JsonDocument.Parse(
            resolved.Variables["ProductCompletionRequiredPathsByStep"]);
        var pathMap = document.RootElement;
        Assert.True(pathMap.TryGetProperty("establish-custom-baseline", out var paths));
        Assert.Equal(1, paths.GetArrayLength());
        Assert.Equal("C:/output/Custom/Custom.slnx", paths[0].GetString());
        Assert.False(pathMap.TryGetProperty("create-dotnet-project", out _));
    }

    [Fact]
    public void Template_owned_setup_policy_resolves_windows_paths_before_serializing_json()
    {
        var activation = new ProcessLaunchDriverActivation(
            "dotnet.launch-contract",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [DotNetSolutionSetupTemplatePolicyBindings.RequiredPathsSettingKey] =
                    """{"add-test-project":["${DotNetTestProjectFileForwardSlash}"]}""",
                [DotNetSolutionSetupTemplatePolicyBindings.RequiredToolReceiptsSettingKey] =
                    """{"add-test-project":["workspace_pwsh_run_script"]}""",
                [DotNetSolutionSetupTemplatePolicyBindings.RequiredFileContentChecksSettingKey] =
                    """{"add-test-project":[{"pathCandidates":["${DotNetTestProjectFileForwardSlash}"],"requiredTextAnyGroups":[["${DotNetAppProjectReferenceRelativePath}","${DotNetAppProjectReferenceRelativePathWindows}"]]}]}""",
                [DotNetSolutionSetupTemplatePolicyBindings.ScopedLaunchVariablePrefixesSettingKey] =
                    """{"add-test-project":["DotNetAddTestProject"]}"""
            });
        Assert.True(
            DotNetSolutionSetupTemplatePolicyBindings.TryParse(
                activation,
                out var bindings,
                out var issue),
            issue);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DotNetTestProjectFileForwardSlash"] = "verification/TetrisGame.Tests/TetrisGame.Tests.csproj",
            ["DotNetAppProjectReferenceRelativePath"] = "../../app/TetrisGame/TetrisGame.csproj",
            ["DotNetAppProjectReferenceRelativePathWindows"] = @"..\..\app\TetrisGame\TetrisGame.csproj"
        };

        bindings.ApplyTo(variables);
        var resolved = new LaunchVariableTemplateResolver().Resolve(variables);

        Assert.DoesNotContain(resolved.Diagnostics, diagnostic => diagnostic.IsBlocking);
        using var document = JsonDocument.Parse(
            resolved.Variables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep]);
        var referenceGroup = document.RootElement
            .GetProperty("add-test-project")[0]
            .GetProperty("requiredTextAnyGroups")[0]
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
        Assert.Equal(
            [
                "../../app/TetrisGame/TetrisGame.csproj",
                @"..\..\app\TetrisGame\TetrisGame.csproj"
            ],
            referenceGroup);
    }

    [Fact]
    public void Template_owned_setup_policy_rejects_missing_required_map()
    {
        var activation = new ProcessLaunchDriverActivation(
            "dotnet.launch-contract",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [DotNetSolutionSetupTemplatePolicyBindings.RequiredPathsSettingKey] = "{}"
            });

        var parsed = DotNetSolutionSetupTemplatePolicyBindings.TryParse(
            activation,
            out _,
            out var issue);

        Assert.False(parsed);
        Assert.Contains(
            DotNetSolutionSetupTemplatePolicyBindings.RequiredPathsSettingKey,
            issue,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Launch_driver_contains_no_prose_inference_or_legacy_contract_builder()
    {
        var root = FindRepositoryRoot();
        var driverDirectory = Path.Combine(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.Processes",
            "Services",
            "RuntimeIntegration",
            "Drivers",
            "DotNet");
        var factory = File.ReadAllText(Path.Combine(driverDirectory, "DotNetProcessLaunchContractFactory.cs"));
        var contributor = File.ReadAllText(Path.Combine(driverDirectory, "DotNetProcessLaunchVariableContributor.cs"));

        Assert.False(File.Exists(Path.Combine(driverDirectory, "DotNetProcessLaunchContractBuilder.cs")));
        Assert.False(File.Exists(Path.Combine(driverDirectory, "DotNetSolutionSetupReadbackPolicyFactory.cs")));
        Assert.DoesNotContain("ProjectStructureContextSummary", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("context.Source", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("Regex", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("Blazor", factory, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("xunit", factory, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("net10", factory, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WorkspaceDotnetNewTemplateCatalog", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("software-delivery", contributor, StringComparison.Ordinal);
        Assert.DoesNotContain("blazor-", contributor, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Template_owned_target_intent_contract_is_explicit_and_product_agnostic()
    {
        var root = FindRepositoryRoot();
        var processDirectory = Path.Combine(
            root,
            "Templates",
            "Processes",
            "processes",
            "dotnet-development-slice");
        var intake = File.ReadAllText(Path.Combine(processDirectory, "steps", "slice-intake.md"));
        var architecture = File.ReadAllText(Path.Combine(processDirectory, "steps", "slice-architecture-check.md"));
        var definition = File.ReadAllText(Path.Combine(processDirectory, "definition.json"));

        Assert.Contains("ProductTargetState", intake, StringComparison.Ordinal);
        Assert.Contains("ProductTargetFilesystemState", intake, StringComparison.Ordinal);
        Assert.Contains("greenfield", architecture, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("verify-existing", architecture, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("initialization", architecture, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dotnet.solution-context/v1", definition, StringComparison.Ordinal);
        Assert.Contains("external-target/...", architecture, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ProductTargetState", definition, StringComparison.Ordinal);
        Assert.DoesNotContain("Tetris", intake, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Calculator", architecture, StringComparison.OrdinalIgnoreCase);
    }

    private static ProcessLaunchSourceSnapshot Source(string notes)
    {
        var item = new ProcessLaunchSourceItem(
            "source",
            "Ignored source title",
            string.Empty,
            notes,
            string.Empty,
            string.Empty,
            [],
            ProcessLaunchSourceItemKind.Other,
            IsIncludedInProcessContext: true);
        return new ProcessLaunchSourceSnapshot(Guid.NewGuid(), "Ignored project", item, [item], notes);
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
