using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRuntimeToolPreflightServiceTests
{
    [Fact]
    public async Task EvaluateAsync_rejects_workspace_script_when_profile_can_expose_tool_but_agent_lacks_capability()
    {
        var agent = CreateAgent(AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.MutateProductTarget],
            ProcessOperationContractNames.ExternalProductTargetMutable);
        var service = new ProcessRuntimeToolPreflightService([], [new DotNetSolutionSetupRuntimeToolPlanGuard()], ProcessRuntimeToolPreflightContributionCatalog.Empty);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["workspace_pwsh_run_script"]),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        Assert.Empty(result.MissingToolNames);
        var diagnostic = Assert.Single(result.CapabilityDiagnostics);
        Assert.Equal(AgentCapabilityDiagnosticCode.MissingRequiredCapability, diagnostic.Code);
        Assert.Equal(CapabilityKind.Tool, diagnostic.Kind);
        Assert.Equal("workspace-pwsh-run-script", diagnostic.CapabilityKey);
        Assert.Contains("capability", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_satisfies_workspace_script_from_assigned_tool_capability()
    {
        var agent = CreateAgent(
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            [CreateCapability("workspace-pwsh-run-script", CapabilityKind.Tool)]);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.MutateProductTarget],
            ProcessOperationContractNames.ExternalProductTargetMutable);
        var service = new ProcessRuntimeToolPreflightService([], [new DotNetSolutionSetupRuntimeToolPlanGuard()], ProcessRuntimeToolPreflightContributionCatalog.Empty);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["workspace_pwsh_run_script"]),
            CancellationToken.None);

        Assert.True(result.IsSatisfied, result.Summary);
        Assert.Empty(result.MissingToolNames);
        Assert.Empty(result.CapabilityDiagnostics);
    }

    [Fact]
    public async Task EvaluateAsync_missing_workspace_script_when_agent_profile_disables_local_scripts()
    {
        var agent = CreateAgent(
            AgentWorkspaceToolProfileKind.ArchitectureReview,
            [CreateCapability("workspace-pwsh-run-script", CapabilityKind.Tool)]);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.MutateProductTarget],
            ProcessOperationContractNames.ExternalProductTargetMutable);
        var service = new ProcessRuntimeToolPreflightService([], [new DotNetSolutionSetupRuntimeToolPlanGuard()], ProcessRuntimeToolPreflightContributionCatalog.Empty);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["workspace_pwsh_run_script"]),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        Assert.Equal(["workspace_pwsh_run_script"], result.MissingToolNames);
        Assert.Empty(result.CapabilityDiagnostics);
    }

    [Fact]
    public async Task EvaluateAsync_missing_workspace_script_when_operation_contract_disallows_scripts()
    {
        var agent = CreateAgent(
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            [CreateCapability("workspace-pwsh-run-script", CapabilityKind.Tool)]);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.WriteManagedProcessArtifacts],
            ProcessOperationContractNames.ManagedProcessArtifactsOnly);
        var service = new ProcessRuntimeToolPreflightService([], [new DotNetSolutionSetupRuntimeToolPlanGuard()], ProcessRuntimeToolPreflightContributionCatalog.Empty);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["workspace_pwsh_run_script"]),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        Assert.Equal(["workspace_pwsh_run_script"], result.MissingToolNames);
        Assert.Empty(result.CapabilityDiagnostics);
    }

    [Fact]
    public async Task EvaluateAsync_satisfies_managed_artifact_write_without_product_mutation()
    {
        var agent = CreateAgent(
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            [CreateCapability("workspace-write-file", CapabilityKind.Tool)]);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.WriteManagedProcessArtifacts],
            ProcessOperationContractNames.ManagedProcessArtifactsOnly);
        var service = new ProcessRuntimeToolPreflightService([], [new DotNetSolutionSetupRuntimeToolPlanGuard()], ProcessRuntimeToolPreflightContributionCatalog.Empty);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["workspace_write_file"]),
            CancellationToken.None);

        Assert.True(result.IsSatisfied);
        Assert.Empty(result.MissingToolNames);
        Assert.Empty(result.CapabilityDiagnostics);
    }

    [Fact]
    public async Task EvaluateAsync_satisfies_browser_tools_from_assigned_playwright_mcp_capability()
    {
        var agent = CreateAgent(
            AgentWorkspaceToolProfileKind.QualityValidation,
            [CreateCapability("playwright-local-mcp", CapabilityKind.McpServer)]);
        var assignment = CreateAssignment(
            agent.Id,
            [
                ProcessOperationContractNames.CaptureRuntimeProof,
                ProcessOperationContractNames.LaunchRuntime,
                ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ],
            ProcessOperationContractNames.ExternalProductTargetReadOnly);
        var service = CreateBrowserPreflightService();

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                [
                    "browser_navigate",
                    "browser_snapshot",
                    "browser_take_screenshot",
                    "browser_console_messages"
                ]),
            CancellationToken.None);

        Assert.True(result.IsSatisfied);
        Assert.Empty(result.MissingToolNames);
        Assert.Empty(result.CapabilityDiagnostics);
    }

    [Fact]
    public async Task EvaluateAsync_does_not_satisfy_browser_tools_without_runtime_proof_operation()
    {
        var agent = CreateAgent(
            AgentWorkspaceToolProfileKind.QualityValidation,
            [CreateCapability("playwright-local-mcp", CapabilityKind.McpServer)]);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.RunValidation],
            ProcessOperationContractNames.ExternalProductTargetReadOnly);
        var service = CreateBrowserPreflightService();

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["browser_snapshot"]),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        Assert.Equal(["browser_snapshot"], result.MissingToolNames);
        Assert.Empty(result.CapabilityDiagnostics);
    }

    [Fact]
    public async Task EvaluateAsync_missing_browser_tools_without_playwright_mcp_capability()
    {
        var agent = CreateAgent(AgentWorkspaceToolProfileKind.QualityValidation);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.CaptureRuntimeProof],
            ProcessOperationContractNames.ExternalProductTargetReadOnly);
        var service = CreateBrowserPreflightService();

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["browser_snapshot"]),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        Assert.Empty(result.MissingToolNames);
        var diagnostic = Assert.Single(result.CapabilityDiagnostics);
        Assert.Equal(CapabilityKind.McpServer, diagnostic.Kind);
        Assert.Equal("playwright-local-mcp", diagnostic.CapabilityKey);
    }

    [Fact]
    public void Preflight_contribution_context_normalizes_and_limits_handled_tools_to_declared_requirements()
    {
        var context = CreatePreflightContributionContext(["test-runtime-tool"]);

        context.MarkToolHandled("test-runtime-tool");

        Assert.Equal(["test_runtime_tool"], context.RequiredToolNames);
        Assert.Contains("test_runtime_tool", context.HandledToolNames);
        var exception = Assert.Throws<InvalidOperationException>(() =>
            context.MarkToolHandled("workspace_read_file"));

        Assert.Contains("not required by the current process preflight request", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Preflight_contribution_context_requires_ownership_before_claiming_a_tool_as_composed()
    {
        var context = CreatePreflightContributionContext(["test_runtime_tool"]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            context.AddComposedToolName("test_runtime_tool"));

        Assert.Contains("must be marked as handled", exception.Message, StringComparison.Ordinal);

        context.MarkToolHandled("test_runtime_tool");
        context.AddComposedToolName("test-runtime-tool");

        Assert.Contains("test_runtime_tool", context.ComposedToolNames);
    }

    [Fact]
    public void Preflight_contribution_catalog_runs_contributions_by_order_then_stable_key()
    {
        var calls = new List<string>();
        var catalog = new ProcessRuntimeToolPreflightContributionCatalog(
        [
            new TrackingPreflightContribution("last", 200, _ => calls.Add("last")),
            new TrackingPreflightContribution("bravo", 100, _ => calls.Add("bravo")),
            new TrackingPreflightContribution("alpha", 100, _ => calls.Add("alpha"))
        ]);

        catalog.Contribute(CreatePreflightContributionContext(["test_runtime_tool"]));

        Assert.Equal(["alpha", "bravo", "last"], calls);
    }

    [Fact]
    public void Preflight_contribution_catalog_rejects_duplicate_keys_case_insensitively()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ProcessRuntimeToolPreflightContributionCatalog(
            [
                new TrackingPreflightContribution("test.contribution", 100, _ => { }),
                new TrackingPreflightContribution("TEST.CONTRIBUTION", 200, _ => { })
            ]));

        Assert.Contains("Duplicate process runtime tool preflight contribution key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_rejects_dotnet_create_plan_when_helper_receipt_is_missing()
    {
        var agent = CreateAgent(AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var assignment = CreateDotNetCreateProjectAssignment(
            agent.Id,
            CreateDotNetCreateProjectLaunchVariables(requiredReceipts:
            [
                "template=sln",
                "template=blazorwasm"
            ]));
        var service = new ProcessRuntimeToolPreflightService([], [new DotNetSolutionSetupRuntimeToolPlanGuard()], ProcessRuntimeToolPreflightContributionCatalog.Empty);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                []),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        Assert.Empty(result.MissingToolNames);
        var issue = Assert.Single(
            result.PlanIssues,
            issue => issue.Code == "dotnet.setup.plan.required_receipt_missing");
        Assert.Contains("workspace_pwsh_run_script", issue.SafeSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_rejects_dotnet_create_plan_with_unresolved_script_ref()
    {
        var agent = CreateAgent(AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var assignment = CreateDotNetCreateProjectAssignment(
            agent.Id,
            CreateDotNetCreateProjectLaunchVariables(
                scriptRef: "artifacts/process-runs/{CurrentProcessRunId}/scripts/create-dotnet-project.ps1"));
        var service = new ProcessRuntimeToolPreflightService([], [new DotNetSolutionSetupRuntimeToolPlanGuard()], ProcessRuntimeToolPreflightContributionCatalog.Empty);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["workspace_pwsh_run_script"]),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        Assert.Contains(
            result.PlanIssues,
            issue => issue.Code == "dotnet.setup.plan.script_ref_unresolved");
    }

    [Fact]
    public async Task EvaluateAsync_rejects_dotnet_create_plan_with_external_target_manifest_path()
    {
        var agent = CreateAgent(AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var manifest = JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["version"] = 1,
            ["mode"] = "ProductMutation",
            ["declaredReadPaths"] = new[] { "external-target/calculator/Calculator.slnx" },
            ["declaredWritePaths"] = new[] { "external-target/calculator/Calculator.slnx" },
            ["allowShellDelegation"] = true
        });
        var assignment = CreateDotNetCreateProjectAssignment(
            agent.Id,
            CreateDotNetCreateProjectLaunchVariables(sideEffectManifest: manifest));
        var service = new ProcessRuntimeToolPreflightService([], [new DotNetSolutionSetupRuntimeToolPlanGuard()], ProcessRuntimeToolPreflightContributionCatalog.Empty);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["workspace_pwsh_run_script"]),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        Assert.Contains(
            result.PlanIssues,
            issue => issue.Code == "dotnet.setup.plan.native_path_scope_invalid");
    }

    [Fact]
    public async Task EvaluateAsync_rejects_dotnet_create_plan_with_required_path_outside_product_root()
    {
        var agent = CreateAgent(AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var assignment = CreateDotNetCreateProjectAssignment(
            agent.Id,
            CreateDotNetCreateProjectLaunchVariables(requiredPaths:
            [
                @"C:\temp\Other\Calculator.slnx",
                @"C:\temp\CanDoItAll\Calculator\src\Calculator\Calculator.csproj"
            ]));
        var service = new ProcessRuntimeToolPreflightService([], [new DotNetSolutionSetupRuntimeToolPlanGuard()], ProcessRuntimeToolPreflightContributionCatalog.Empty);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["workspace_pwsh_run_script"]),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        Assert.Contains(
            result.PlanIssues,
            issue => issue.Code == "dotnet.setup.plan.path_outside_product_root");
    }

    [Fact]
    public async Task EvaluateAsync_satisfies_complete_dotnet_create_plan()
    {
        var agent = CreateAgent(
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            [CreateCapability("workspace-pwsh-run-script", CapabilityKind.Tool)]);
        var assignment = CreateDotNetCreateProjectAssignment(
            agent.Id,
            CreateDotNetCreateProjectLaunchVariables());
        var service = new ProcessRuntimeToolPreflightService([], [new DotNetSolutionSetupRuntimeToolPlanGuard()], ProcessRuntimeToolPreflightContributionCatalog.Empty);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["workspace_pwsh_run_script"]),
            CancellationToken.None);

        Assert.True(result.IsSatisfied, result.Summary);
        Assert.Empty(result.MissingToolNames);
        Assert.Empty(result.PlanIssues);
        Assert.Empty(result.CapabilityDiagnostics);
    }

    [Fact]
    public async Task EvaluateAsync_rejects_selected_dotnet_executor_with_mismatched_descriptor_plan_kind()
    {
        var agent = CreateAgent(AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var launchVariables = new Dictionary<string, string>(
            CreateDotNetCreateProjectLaunchVariables(),
            StringComparer.OrdinalIgnoreCase)
        {
            [ProcessRuntimeLaunchVariables.ProcessStepScriptHelperDescriptorJson] =
                ProcessRuntimeLaunchVariables.SerializeProcessStepScriptHelperDescriptor(
                    new ProcessRuntimeScriptHelperDescriptor(
                        "DotNetCreateProjectScript",
                        "DotNetCreateProjectScriptRef",
                        "DotNetCreateProjectSideEffectManifest",
                        "dotnet.create-project",
                        "DotNetSolutionAddTestProject",
                        "DotNetCreateProjectExecutionPlan"))
        };
        var assignment = CreateDotNetCreateProjectAssignment(agent.Id, launchVariables);
        var service = new ProcessRuntimeToolPreflightService([], [new DotNetSolutionSetupRuntimeToolPlanGuard()], ProcessRuntimeToolPreflightContributionCatalog.Empty);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                []),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        Assert.Contains(result.PlanIssues, issue => issue.Code == "dotnet.setup.plan.descriptor_invalid");
    }

    [Fact]
    public async Task EvaluateAsync_does_not_claim_a_dotnet_plan_owned_by_another_runtime_executor()
    {
        var agent = CreateAgent(AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.MutateProductTarget],
            ProcessOperationContractNames.ExternalProductTargetMutable) with
        {
            LaunchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProcessRuntimeLaunchVariables.ProcessStepRuntimeOwnedExecutorKey] = "other.runtime-owned",
                [ProcessRuntimeLaunchVariables.ProcessStepScriptHelperDescriptorJson] =
                    ProcessRuntimeLaunchVariables.SerializeProcessStepScriptHelperDescriptor(
                        new ProcessRuntimeScriptHelperDescriptor(
                            "OtherScript",
                            "OtherScriptRef",
                            "OtherManifest",
                            "dotnet.other-runtime-plan",
                            "OtherDotNetPlan",
                            "OtherExecutionPlan"))
            }
        };
        var service = new ProcessRuntimeToolPreflightService([], [new DotNetSolutionSetupRuntimeToolPlanGuard()], ProcessRuntimeToolPreflightContributionCatalog.Empty);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                []),
            CancellationToken.None);

        Assert.True(result.IsSatisfied, result.Summary);
        Assert.Empty(result.PlanIssues);
    }

    private static ProcessRuntimeToolPreflightService CreateBrowserPreflightService()
    {
        return new ProcessRuntimeToolPreflightService(
            [],
            [new DotNetSolutionSetupRuntimeToolPlanGuard()],
            new ProcessRuntimeToolPreflightContributionCatalog(
            [
                new BrowserRuntimeToolPreflightContribution()
            ]));
    }

    private static ProcessRuntimeToolPreflightContributionContext CreatePreflightContributionContext(
        IReadOnlyList<string> requiredToolNames)
    {
        var agent = CreateAgent(AgentWorkspaceToolProfileKind.QualityValidation);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.ReadProcessContext],
            ProcessOperationContractNames.ManagedProcessArtifactsOnly);

        return new ProcessRuntimeToolPreflightContributionContext(
            new ProcessRuntimeToolPreflightRequest(assignment, agent, requiredToolNames),
            requiredToolNames,
            ProcessRuntimeProviderContextFactory.Create(assignment));
    }

    private sealed class TrackingPreflightContribution(
        string contributionKey,
        int order,
        Action<ProcessRuntimeToolPreflightContributionContext> callback) : IProcessRuntimeToolPreflightContribution
    {
        public string ContributionKey => contributionKey;

        public int Order => order;

        public void Contribute(ProcessRuntimeToolPreflightContributionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            callback(context);
        }
    }

    private static AgentDefinition CreateAgent(
        AgentWorkspaceToolProfileKind toolProfile,
        IReadOnlyList<AgentCapabilityAssignment>? capabilities = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            ".NET Application Developer",
            ".NET developer",
            ".NET developer test agent.",
            "Test instructions.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: "test-model",
            AgentWorkloadKind.Programming,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            AgentWorkspaceToolAccessMetadata.Write(
                "{}",
                new AgentWorkspaceToolAccessSettings
                {
                    Profile = toolProfile
                }),
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            capabilities ?? [],
            ["dotnet", "developer"],
            now,
            now);
    }

    private static AgentCapabilityAssignment CreateCapability(
        string capabilityKey,
        CapabilityKind kind)
    {
        return new AgentCapabilityAssignment(
            Guid.NewGuid(),
            capabilityKey,
            kind,
            CapabilityProofStatus.Verified,
            LastVerifiedAtUtc: null,
            ProofNotes: string.Empty);
    }

    private static ProcessRuntimeStepAssignment CreateAssignment(
        Guid agentId,
        IReadOnlyList<string> allowedOperations,
        string operationTargetScope)
    {
        return new ProcessRuntimeStepAssignment(
            ProcessRunId.New(),
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "create-dotnet-project",
            "dotnet-developer",
            "dotnet-developer",
            ".NET developer",
            ProcessLaunchExecutorKinds.Agent,
            agentId.ToString("D"),
            ".NET Application Developer",
            "Implement the app in the configured output root.",
            "sha256:readiness",
            "Resolved from role fit.",
            [ArtifactSlotId.New()],
            [],
            allowedOperations,
            operationTargetScope,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            BranchGate: null,
            DateTimeOffset.UtcNow);
    }

    private static ProcessRuntimeStepAssignment CreateDotNetCreateProjectAssignment(
        Guid agentId,
        IReadOnlyDictionary<string, string> launchVariables)
    {
        return CreateAssignment(
            agentId,
            [ProcessOperationContractNames.MutateProductTarget],
            ProcessOperationContractNames.ExternalProductTargetMutable) with
        {
            LaunchVariables = new Dictionary<string, string>(launchVariables, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static IReadOnlyDictionary<string, string> CreateDotNetCreateProjectLaunchVariables(
        IReadOnlyList<string>? requiredReceipts = null,
        string? scriptRef = null,
        string? sideEffectManifest = null,
        IReadOnlyList<string>? requiredPaths = null)
    {
        var productRoot = @"C:\temp\CanDoItAll\Calculator";
        var solutionFile = $@"{productRoot}\Calculator.slnx";
        var appProjectFile = $@"{productRoot}\src\Calculator\Calculator.csproj";
        scriptRef ??= "artifacts/process-runs/11111111-2222-3333-4444-555555555555/scripts/create-dotnet-project.wire-solution.ps1";
        sideEffectManifest ??= JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["version"] = 1,
            ["mode"] = "ProductMutation",
            ["declaredReadPaths"] = new[] { solutionFile, appProjectFile },
            ["declaredWritePaths"] = new[] { solutionFile },
            ["allowShellDelegation"] = true
        });

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = productRoot,
            ["DotNetAppTemplate"] = "blazorwasm",
            ["DotNetCreateProjectScriptRef"] = scriptRef,
            ["DotNetCreateProjectScript"] = "dotnet sln $SolutionFile add $AppProjectFile; dotnet sln $SolutionFile list",
            ["DotNetCreateProjectSideEffectManifest"] = sideEffectManifest,
            ["DotNetCreateProjectExecutionPlan"] =
                JsonSerializer.Serialize(new
                {
                    PlanKey = "dotnet.create-project",
                    ScriptRef = scriptRef,
                    WorkspaceAlias = "external-target/calculator",
                    RequiresScaffold = true
                }),
            [ProcessRuntimeLaunchVariables.ProcessStepRuntimeOwnedExecutorKey] = "dotnet.solution-setup",
            [ProcessRuntimeLaunchVariables.ProcessStepScriptHelperDescriptorJson] =
                ProcessRuntimeLaunchVariables.SerializeProcessStepScriptHelperDescriptor(
                    new ProcessRuntimeScriptHelperDescriptor(
                        "DotNetCreateProjectScript",
                        "DotNetCreateProjectScriptRef",
                        "DotNetCreateProjectSideEffectManifest",
                        "dotnet.create-project",
                        "DotNetSolutionCreate",
                        "DotNetCreateProjectExecutionPlan")),
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] = JsonSerializer.Serialize(
                (requiredReceipts ??
                [
                    "template=sln",
                    "template=blazorwasm",
                    "workspace_pwsh_run_script"
                ]).ToArray()),
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths] = JsonSerializer.Serialize(
                (requiredPaths ??
                [
                    solutionFile,
                    appProjectFile
                ]).ToArray()),
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] = JsonSerializer.Serialize(
                new object[]
                {
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["pathCandidates"] = new[] { solutionFile },
                        ["requiredTextAnyGroups"] = new[] { new[] { "src/Calculator/Calculator.csproj" } }
                    }
                })
        };
    }
}
