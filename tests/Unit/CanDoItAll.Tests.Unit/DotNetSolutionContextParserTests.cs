using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit;

public sealed class DotNetSolutionContextParserTests
{
    [Fact]
    public void TryParse_accepts_an_explicit_initialize_context()
    {
        var parser = new DotNetSolutionContextParser();

        var parsed = parser.TryParse(CreateInitializeArtifact(), out var context, out var issue);

        Assert.True(parsed, issue);
        Assert.Equal(DotNetSolutionProvisioningMode.Initialize, context.ProvisioningMode);
        Assert.Equal("TimeTracker.slnx", context.SolutionFile);
        Assert.Equal(["client/TimeTracker/TimeTracker.csproj", "verification/TimeTracker.Specs/TimeTracker.Specs.csproj"], context.RequiredProjectFiles);
        Assert.NotNull(context.Initialization);
        Assert.Equal("blazorwasm", context.Initialization!.Application.ApplicationTemplate);
        Assert.Equal(["--pwa"], context.Initialization.Application.ApplicationTemplateOptions);
    }

    [Fact]
    public void TryParse_accepts_nonconventional_existing_solution_with_multiple_projects_and_no_tests()
    {
        var parser = new DotNetSolutionContextParser();

        var parsed = parser.TryParse(CreateVerifyExistingArtifact(), out var context, out var issue);

        Assert.True(parsed, issue);
        Assert.Equal(DotNetSolutionProvisioningMode.VerifyExisting, context.ProvisioningMode);
        Assert.Equal("build/EnterpriseSuite.sln", context.SolutionFile);
        Assert.Equal(["modules/Portal/Portal.csproj", "shared/Contracts/Contracts.csproj"], context.RequiredProjectFiles);
        Assert.Empty(context.TestProjectFiles);
        Assert.Null(context.Initialization);
    }

    [Fact]
    public void TryParse_rejects_an_initialization_plan_for_verify_existing()
    {
        var parser = new DotNetSolutionContextParser();

        var parsed = parser.TryParse(
            CreateVerifyExistingArtifact().Replace(
                "\"testProjectFiles\": []",
                "\"testProjectFiles\": [], \"initialization\": {}",
                StringComparison.Ordinal),
            out _,
            out var issue);

        Assert.False(parsed);
        Assert.Contains("must not declare an initialization", issue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParse_rejects_multiple_json_blocks()
    {
        var parser = new DotNetSolutionContextParser();

        var parsed = parser.TryParse($"{CreateInitializeArtifact()}\n```json\n{{}}\n```", out _, out var issue);

        Assert.False(parsed);
        Assert.Contains("exactly one", issue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParse_rejects_a_schema_other_than_the_declared_contract()
    {
        var parser = new DotNetSolutionContextParser();

        var parsed = parser.TryParse(
            CreateInitializeArtifact().Replace("dotnet.solution-context/v1", "other/v1", StringComparison.Ordinal),
            out _,
            out var issue);

        Assert.False(parsed);
        Assert.Contains("schema", issue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Initialization_contract_factory_uses_explicit_nonconventional_layout_without_source_text()
    {
        var parser = new DotNetSolutionContextParser();
        Assert.True(parser.TryParse(CreateInitializeArtifact(), out var context, out var parseIssue), parseIssue);
        var factory = CreateInitializationContractFactory();
        var root = Path.Combine(Path.GetTempPath(), $"CanDoItAll.Decision.{Guid.NewGuid():N}");
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = root,
            ["ProjectStructureContextSummary"] = "Blazor PWA net10 xUnit src tests calculator tetris"
        };

        var created = factory.TryCreate(context, variables, out var contract, out var issue);

        Assert.True(created, issue);
        Assert.Equal(Path.Combine(root, "client", "TimeTracker", "TimeTracker.csproj"), contract.AppProjectFile);
        Assert.Equal(Path.Combine(root, "verification", "TimeTracker.Specs", "TimeTracker.Specs.csproj"), contract.TestProjectFile);
        Assert.Equal("blazorwasm", contract.AppArchetype.Template);
        Assert.Equal("--pwa", contract.AppArchetype.TemplateOptionsText);
        Assert.DoesNotContain("src", contract.AppProjectDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tests", contract.TestProjectDirectory, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Initialization_contract_factory_derives_supported_solution_format_alternatives()
    {
        var parser = new DotNetSolutionContextParser();
        var artifact = CreateInitializeArtifact().Replace(
            """["TimeTracker.slnx", "TimeTracker.sln"]""",
            """["TimeTracker.slnx"]""",
            StringComparison.Ordinal);
        Assert.True(parser.TryParse(artifact, out var context, out var parseIssue), parseIssue);
        var root = Path.Combine(Path.GetTempPath(), $"CanDoItAll.Decision.{Guid.NewGuid():N}");
        var factory = CreateInitializationContractFactory();

        var created = factory.TryCreate(
            context,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProductRoot"] = root
            },
            out var contract,
            out var issue);

        Assert.True(created, issue);
        Assert.Equal(
            [
                Path.Combine(root, "TimeTracker.slnx"),
                Path.Combine(root, "TimeTracker.sln")
            ],
            contract.SolutionCandidatePaths);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Initialization_contract_factory_accepts_application_at_product_root(
        bool productRootHasTrailingSeparator)
    {
        var parser = new DotNetSolutionContextParser();
        var artifact = CreateInitializeArtifact()
            .Replace("client/TimeTracker/TimeTracker.csproj", "TimeTracker.csproj", StringComparison.Ordinal)
            .Replace("\"directory\": \"client/TimeTracker\"", "\"directory\": \".\"", StringComparison.Ordinal);
        Assert.True(parser.TryParse(artifact, out var context, out var parseIssue), parseIssue);
        var factory = CreateInitializationContractFactory();
        var canonicalRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.Decision.{Guid.NewGuid():N}",
            "TimeTracker");
        var root = productRootHasTrailingSeparator
            ? canonicalRoot + Path.DirectorySeparatorChar
            : canonicalRoot;

        var created = factory.TryCreate(
            context,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProductRoot"] = root
            },
            out var contract,
            out var issue);

        Assert.True(created, issue);
        Assert.Equal(Path.GetFullPath(canonicalRoot), contract.AppProjectDirectory);
        Assert.Equal(Path.Combine(canonicalRoot, "TimeTracker.csproj"), contract.AppProjectFile);
    }

    [Theory]
    [InlineData("Blazor WebAssembly App", "xunit", "initialization.application.template")]
    [InlineData("blazorwasm", "xUnit test project", "initialization.tests.template")]
    public void Initialization_contract_factory_rejects_template_display_labels_before_child_launch(
        string applicationTemplate,
        string testTemplate,
        string expectedField)
    {
        var parser = new DotNetSolutionContextParser();
        var artifact = CreateInitializeArtifact()
            .Replace("\"template\": \"blazorwasm\"", $"\"template\": \"{applicationTemplate}\"", StringComparison.Ordinal)
            .Replace("\"template\": \"xunit\"", $"\"template\": \"{testTemplate}\"", StringComparison.Ordinal);
        Assert.True(parser.TryParse(artifact, out var context, out var parseIssue), parseIssue);
        var factory = CreateInitializationContractFactory();

        var created = factory.TryCreate(
            context,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProductRoot"] = Path.Combine(Path.GetTempPath(), $"CanDoItAll.Decision.{Guid.NewGuid():N}")
            },
            out _,
            out var issue);

        Assert.False(created);
        Assert.Contains(expectedField, issue, StringComparison.Ordinal);
        Assert.Contains("approved dotnet new template identifier", issue, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("sln", "xunit", "initialization.application.template")]
    [InlineData("xunit", "xunit", "initialization.application.template")]
    [InlineData("blazorwasm", "sln", "initialization.tests.template")]
    [InlineData("blazorwasm", "console", "initialization.tests.template")]
    public void Initialization_contract_factory_rejects_templates_for_the_wrong_solution_topology_role(
        string applicationTemplate,
        string testTemplate,
        string expectedField)
    {
        var parser = new DotNetSolutionContextParser();
        var artifact = CreateInitializeArtifact()
            .Replace("\"template\": \"blazorwasm\"", $"\"template\": \"{applicationTemplate}\"", StringComparison.Ordinal)
            .Replace("\"template\": \"xunit\"", $"\"template\": \"{testTemplate}\"", StringComparison.Ordinal);
        Assert.True(parser.TryParse(artifact, out var context, out var parseIssue), parseIssue);
        var factory = CreateInitializationContractFactory();

        var created = factory.TryCreate(
            context,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProductRoot"] = Path.Combine(Path.GetTempPath(), $"CanDoItAll.Decision.{Guid.NewGuid():N}")
            },
            out _,
            out var issue);

        Assert.False(created);
        Assert.Contains(expectedField, issue, StringComparison.Ordinal);
        Assert.Contains("approved dotnet new template identifier", issue, StringComparison.Ordinal);
    }

    [Fact]
    public void Initialization_contract_factory_rejects_an_option_not_supported_by_the_selected_template()
    {
        var parser = new DotNetSolutionContextParser();
        var artifact = CreateInitializeArtifact().Replace(
            "\"template\": \"blazorwasm\"",
            "\"template\": \"console\"",
            StringComparison.Ordinal);
        Assert.True(parser.TryParse(artifact, out var context, out var parseIssue), parseIssue);
        var factory = CreateInitializationContractFactory();

        var created = factory.TryCreate(
            context,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProductRoot"] = Path.Combine(Path.GetTempPath(), $"CanDoItAll.Decision.{Guid.NewGuid():N}")
            },
            out _,
            out var issue);

        Assert.False(created);
        Assert.Contains("initialization plan application.templateOptions", issue, StringComparison.Ordinal);
        Assert.Contains("does not support", issue, StringComparison.Ordinal);
    }

    [Fact]
    public void Initialization_contract_factory_rejects_an_invalid_target_framework_before_child_launch()
    {
        var parser = new DotNetSolutionContextParser();
        var artifact = CreateInitializeArtifact().Replace(
            "\"targetFramework\": \"net10.0\"",
            "\"targetFramework\": \"not-a-target-framework\"",
            StringComparison.Ordinal);
        Assert.True(parser.TryParse(artifact, out var context, out var parseIssue), parseIssue);
        var factory = CreateInitializationContractFactory();

        var created = factory.TryCreate(
            context,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProductRoot"] = Path.Combine(Path.GetTempPath(), $"CanDoItAll.Decision.{Guid.NewGuid():N}")
            },
            out _,
            out var issue);

        Assert.False(created);
        Assert.Contains("initialization.targetFramework", issue, StringComparison.Ordinal);
        Assert.Contains("supported target-framework", issue, StringComparison.Ordinal);
    }

    [Fact]
    public void Existing_solution_contract_factory_accepts_explicit_multiple_projects_without_tests()
    {
        var parser = new DotNetSolutionContextParser();
        Assert.True(parser.TryParse(CreateVerifyExistingArtifact(), out var context, out var parseIssue), parseIssue);
        var root = Path.Combine(Path.GetTempPath(), $"CanDoItAll.Decision.{Guid.NewGuid():N}");
        var factory = new DotNetExistingSolutionVerificationContractFactory(CreatePathResolver());

        var created = factory.TryCreate(
            context,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["ProductRoot"] = root },
            out var contract,
            out var issue);

        Assert.True(created, issue);
        Assert.Equal(Path.Combine(root, "build", "EnterpriseSuite.sln"), contract.SolutionFile);
        Assert.Equal(
            [Path.Combine(root, "modules", "Portal", "Portal.csproj"), Path.Combine(root, "shared", "Contracts", "Contracts.csproj")],
            contract.RequiredProjectFiles);
        Assert.Empty(contract.TestProjectFiles);
    }

    [Fact]
    public void Context_path_resolver_rejects_product_path_escape()
    {
        var parser = new DotNetSolutionContextParser();
        var artifact = CreateVerifyExistingArtifact().Replace(
            "modules/Portal/Portal.csproj",
            "../outside/Portal.csproj",
            StringComparison.Ordinal);
        Assert.True(parser.TryParse(artifact, out var context, out var parseIssue), parseIssue);
        var factory = new DotNetExistingSolutionVerificationContractFactory(CreatePathResolver());

        var created = factory.TryCreate(
            context,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProductRoot"] = Path.Combine(Path.GetTempPath(), $"CanDoItAll.Decision.{Guid.NewGuid():N}")
            },
            out _,
            out var issue);

        Assert.False(created);
        Assert.Contains("escapes", issue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Context_path_resolver_rejects_foreign_host_product_root()
    {
        var parser = new DotNetSolutionContextParser();
        Assert.True(parser.TryParse(CreateVerifyExistingArtifact(), out var context, out var parseIssue), parseIssue);
        var foreignRoot = OperatingSystem.IsWindows()
            ? "/var/lib/candoitall/product"
            : @"C:\products\CanDoItAll";
        var factory = new DotNetExistingSolutionVerificationContractFactory(CreatePathResolver());

        var created = factory.TryCreate(
            context,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProductRoot"] = foreignRoot
            },
            out _,
            out var issue);

        Assert.False(created);
        Assert.Contains("product root", issue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Context_path_resolver_uses_case_sensitive_product_containment_on_unix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var parser = new DotNetSolutionContextParser();
        var artifact = CreateVerifyExistingArtifact().Replace(
            "modules/Portal/Portal.csproj",
            "../product/modules/Portal/Portal.csproj",
            StringComparison.Ordinal);
        Assert.True(parser.TryParse(artifact, out var context, out var parseIssue), parseIssue);
        var productRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.Decision.{Guid.NewGuid():N}", "Product");
        var factory = new DotNetExistingSolutionVerificationContractFactory(CreatePathResolver());

        var created = factory.TryCreate(
            context,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProductRoot"] = productRoot
            },
            out _,
            out var issue);

        Assert.False(created);
        Assert.Contains("escapes", issue, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("build/EnterpriseSuite.sln", "external-target/C/output/EnterpriseSuite.sln")]
    [InlineData("modules/Portal/Portal.csproj", "external-target\\C\\output\\Portal.csproj")]
    public void Context_path_resolver_rejects_external_target_aliases(
        string existingPath,
        string externalTargetAlias)
    {
        var parser = new DotNetSolutionContextParser();
        var artifact = CreateVerifyExistingArtifact().Replace(
            existingPath,
            externalTargetAlias.Replace("\\", "\\\\", StringComparison.Ordinal),
            StringComparison.Ordinal);
        Assert.True(parser.TryParse(artifact, out var context, out var parseIssue), parseIssue);
        var factory = new DotNetExistingSolutionVerificationContractFactory(CreatePathResolver());

        var created = factory.TryCreate(
            context,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProductRoot"] = Path.Combine(Path.GetTempPath(), $"CanDoItAll.Decision.{Guid.NewGuid():N}")
            },
            out _,
            out var issue);

        Assert.False(created);
        Assert.Contains("external-target alias", issue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Authoritative_launch_variable_writer_rejects_conflicting_preexisting_value()
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DotNetAppProjectFile"] = @"C:\product\unexpected\Unexpected.csproj"
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            DotNetProcessLaunchVariableWriter.SetAuthoritative(
                variables,
                "DotNetAppProjectFile",
                @"C:\product\client\TimeTracker\TimeTracker.csproj"));

        Assert.Contains("conflicts", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Contributor_uses_only_the_declared_parent_solution_context_binding()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.Decision.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        try
        {
            var workspaceFiles = TestWorkspaceServices.CreateFileService(workspaceRoot);
            const string artifactRef = "artifacts/process-runs/parent/steps/solution-context.md";
            Assert.True(workspaceFiles.WriteTextFile(artifactRef, CreateInitializeArtifact()).Succeeded);
            var contributor = new DotNetProcessLaunchVariableContributor(
                TestExternalTargetPathRegistry.Create(),
                TestWorkspaceServices.PhysicalPathPolicyFactory,
                workspaceFiles);
            var context = new ProcessLaunchPreparationContext(
                "dotnet-solution-setup",
                IsSubprocess: true,
                EmptySource())
            {
                DriverActivations =
                [
                    new ProcessLaunchDriverActivation(
                        "dotnet.launch-contract",
                        CreateSolutionSetupSettings())
                    {
                        InputArtifactBindings =
                        [
                            new ProcessLaunchDriverArtifactBinding(
                                "solution-context",
                                "slice-architecture-check",
                                "dotnet-solution-context",
                                DotNetSolutionContextParser.Schema)
                        ]
                    }
                ]
            };
            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProductRoot"] = Path.Combine(workspaceRoot, "product"),
                [ProcessRuntimeLaunchVariables.ParentRequiredArtifactBindings] =
                    ProcessRuntimeLaunchVariables.SerializeParentRequiredArtifactBindings(
                    [
                        new ProcessParentArtifactBindingRef(
                            "slice-architecture-check",
                            "dotnet-solution-context",
                            artifactRef)
                    ])
            };

            contributor.Enrich(context, variables);

            Assert.Equal("initialize", variables["DotNetProvisioningMode"]);
            Assert.Equal("blazorwasm", variables["DotNetAppTemplate"]);
            Assert.Equal("--pwa", variables["DotNetAppTemplateOptions"]);
            Assert.Equal(Path.Combine(workspaceRoot, "product", "client", "TimeTracker", "TimeTracker.csproj"), variables["DotNetAppProjectFile"]);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    private static DotNetProcessLaunchContractFactory CreateInitializationContractFactory()
    {
        var externalTargetPathRegistry = TestExternalTargetPathRegistry.Create();
        return new DotNetProcessLaunchContractFactory(
            new DotNetSolutionContextPathResolver(
                externalTargetPathRegistry,
                TestWorkspaceServices.PhysicalPathPolicyFactory),
            externalTargetPathRegistry);
    }

    private static DotNetSolutionContextPathResolver CreatePathResolver()
        => new(
            TestExternalTargetPathRegistry.Create(),
            TestWorkspaceServices.PhysicalPathPolicyFactory);

    private static ProcessLaunchSourceSnapshot EmptySource()
    {
        var item = new ProcessLaunchSourceItem(
            "test",
            "Source content must not be consulted",
            string.Empty,
            "Blazor PWA net10 xUnit src tests",
            string.Empty,
            string.Empty,
            [],
            ProcessLaunchSourceItemKind.Other,
            IsIncludedInProcessContext: true);
        return new ProcessLaunchSourceSnapshot(Guid.NewGuid(), "Ignored", item, [item], item.Notes);
    }

    private static IReadOnlyDictionary<string, string> CreateSolutionSetupSettings()
    {
        var activation = Assert.Single(
            new ProcessTemplatePackLoader()
                .LoadDefinition("dotnet-solution-setup")
                .LaunchDriverActivations);
        return new Dictionary<string, string>(activation.Settings, StringComparer.OrdinalIgnoreCase);
    }

    private static string CreateInitializeArtifact()
        => """
           Solution context

           ```json
           {
             "schema": "dotnet.solution-context/v1",
             "provisioningMode": "initialize",
             "solution": {
               "file": "TimeTracker.slnx",
               "candidateFiles": ["TimeTracker.slnx", "TimeTracker.sln"]
             },
             "requiredProjectFiles": [
               "client/TimeTracker/TimeTracker.csproj",
               "verification/TimeTracker.Specs/TimeTracker.Specs.csproj"
             ],
             "testProjectFiles": ["verification/TimeTracker.Specs/TimeTracker.Specs.csproj"],
             "initialization": {
               "solutionName": "TimeTracker",
               "application": {
                 "name": "TimeTracker",
                 "directory": "client/TimeTracker",
                 "file": "client/TimeTracker/TimeTracker.csproj",
                 "template": "blazorwasm",
                 "templateOptions": ["--pwa"],
                 "archetype": "offline time tracking application"
               },
               "tests": {
                 "name": "TimeTracker.Specs",
                 "directory": "verification/TimeTracker.Specs",
                 "file": "verification/TimeTracker.Specs/TimeTracker.Specs.csproj",
                 "template": "xunit",
                 "frameworkPreference": "xUnit"
               },
               "targetFramework": "net10.0"
             }
           }
           ```
           """;

    private static string CreateVerifyExistingArtifact()
        => """
           Existing solution context

           ```json
           {
             "schema": "dotnet.solution-context/v1",
             "provisioningMode": "verify-existing",
             "solution": {
               "file": "build/EnterpriseSuite.sln"
             },
             "requiredProjectFiles": [
               "modules/Portal/Portal.csproj",
               "shared/Contracts/Contracts.csproj"
             ],
             "testProjectFiles": []
           }
           ```
           """;
}
