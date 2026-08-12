using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRuntimeToolPreflightServiceTests
{
    private static readonly ProcessHostCapabilityId[] AllHostCapabilityIds =
    [
        ProcessHostCapabilityIds.DirectExecution,
        ProcessHostCapabilityIds.ManagedProcessAdapter,
        ProcessHostCapabilityIds.PowerShellScript,
        ProcessHostCapabilityIds.PosixScript,
        ProcessHostCapabilityIds.DotNetRuntime,
        ProcessHostCapabilityIds.PythonRuntime,
        ProcessHostCapabilityIds.NodeRuntime,
        ProcessHostCapabilityIds.NodePackageManager,
        ProcessHostCapabilityIds.Docker,
        ProcessHostCapabilityIds.LocalStdioMcp,
        ProcessHostCapabilityIds.DesktopOpen,
        ProcessHostCapabilityIds.InteractiveTerminal
    ];

    private static readonly IProcessHostCapabilitySnapshotProvider AvailableHostCapabilities =
        new StaticProcessHostCapabilitySnapshotProvider(
            new ProcessHostCapabilitySnapshot(
                new ProcessHostProfileId("test"),
                AllHostCapabilityIds
                    .Select(id => new ProcessHostCapabilityFact(
                        id,
                        ProcessHostCapabilityAvailability.Available,
                        ProcessHostCapabilityReason.Ready,
                        ResolveExecutionPort(id)))
                    .ToArray()));

    public static TheoryData<string, ProcessHostCapabilityId> ProcessStartingToolCapabilityRoutes => new()
    {
        { ToolContractCatalog.WorkspaceDotNetNew, ProcessHostCapabilityIds.DotNetRuntime },
        { ToolContractCatalog.WorkspaceDotNetRestore, ProcessHostCapabilityIds.DotNetRuntime },
        { ToolContractCatalog.WorkspaceDotNetBuild, ProcessHostCapabilityIds.DotNetRuntime },
        { ToolContractCatalog.WorkspaceDotNetTest, ProcessHostCapabilityIds.DotNetRuntime },
        { ToolContractCatalog.WorkspaceDotNetRun, ProcessHostCapabilityIds.DotNetRuntime },
        { ToolContractCatalog.WorkspaceDotNetStop, ProcessHostCapabilityIds.DirectExecution },
        { ToolContractCatalog.WorkspacePowerShellRunScript, ProcessHostCapabilityIds.PowerShellScript },
        { ToolContractCatalog.WorkspacePythonRunFile, ProcessHostCapabilityIds.PythonRuntime },
        { ToolContractCatalog.WorkspaceInspectSpreadsheet, ProcessHostCapabilityIds.PythonRuntime },
        { ToolContractCatalog.WorkspaceCommandRun, ProcessHostCapabilityIds.DirectExecution },
        { ToolContractCatalog.WorkspaceGitStatus, ProcessHostCapabilityIds.DirectExecution },
        { ToolContractCatalog.WorkspaceGitDiff, ProcessHostCapabilityIds.DirectExecution },
        { ToolContractCatalog.WorkspaceGitLog, ProcessHostCapabilityIds.DirectExecution },
        { ToolContractCatalog.WorkspaceGitShow, ProcessHostCapabilityIds.DirectExecution },
        { ToolContractCatalog.WorkspaceGitAdd, ProcessHostCapabilityIds.DirectExecution },
        { ToolContractCatalog.WorkspaceGitUnstage, ProcessHostCapabilityIds.DirectExecution },
        { ToolContractCatalog.WorkspaceGitCommit, ProcessHostCapabilityIds.DirectExecution },
        { ToolContractCatalog.WorkspaceGitBranchCreate, ProcessHostCapabilityIds.DirectExecution },
        { ToolContractCatalog.WorkspaceGitSwitch, ProcessHostCapabilityIds.DirectExecution },
        { ToolContractCatalog.LocalMcpLaunch, ProcessHostCapabilityIds.LocalStdioMcp },
        { AgentToolInvocationPolicyMetadata.RunSkillScript, ProcessHostCapabilityIds.DirectExecution }
    };

    [Theory]
    [MemberData(nameof(ProcessStartingToolCapabilityRoutes))]
    public void Process_starting_tool_contracts_have_deterministic_host_routes(
        string toolName,
        ProcessHostCapabilityId expectedCapability)
    {
        Assert.Equal(expectedCapability, ProcessRuntimeToolHostCapabilityPolicy.Resolve(toolName));
    }

    [Fact]
    public async Task Step_host_gate_rejects_effective_capability_union_over_32_before_snapshot_evaluation()
    {
        var declaredCapabilities = Enumerable.Range(0, ProcessHostCapabilitySnapshot.MaximumCapabilities)
            .Select(index => new ProcessHostCapabilityId($"host.test.declared-{index:D2}"))
            .ToArray();
        var service = CreatePreflightService();

        var result = await service.EvaluateStepHostCapabilitiesAsync(
            [ToolContractCatalog.WorkspacePythonRunFile],
            [ToolContractCatalog.WorkspacePythonRunFile],
            declaredCapabilities,
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        Assert.Equal(["invalid-host-capability-contract"], result.MissingToolNames);
        Assert.Null(result.HostCapabilityEvidence);
    }

    [Fact]
    public async Task EvaluateAsync_composes_capability_bound_provider_from_resolved_attached_catalog()
    {
        var capability = CreateCatalogCapability(WorkflowAgentCapabilityKeys.DefinitionsList);
        var agent = CreateAgent(
            AgentWorkspaceToolProfileKind.ArchitectureReview,
            [CreateAssignment(capability)]);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.ReadProcessContext],
            ProcessOperationContractNames.ManagedProcessArtifactsOnly);
        var service = new ProcessRuntimeToolPreflightService(
            [new CapabilityBoundRuntimeToolProvider(
                AgentToolInvocationPolicyMetadata.WorkflowsDefinitionsList,
                capability.Id)],
            [new DotNetSolutionSetupRuntimeToolPlanGuard(TestWorkspaceServices.PhysicalPathPolicyFactory)],
            ProcessRuntimeToolPreflightContributionCatalog.Empty,
            AvailableHostCapabilities);
        var resolverCalls = 0;

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                [AgentToolInvocationPolicyMetadata.WorkflowsDefinitionsList],
                CapabilityCatalogResolver: _ =>
                {
                    resolverCalls++;
                    return ValueTask.FromResult<IReadOnlyList<CapabilityCatalogItem>>([capability]);
                }),
            CancellationToken.None);

        Assert.True(result.IsSatisfied, result.Summary);
        Assert.Equal(1, resolverCalls);
        Assert.Empty(result.MissingToolNames);
        Assert.Empty(result.CapabilityDiagnostics);
    }

    [Fact]
    public async Task EvaluateAsync_skips_provider_that_does_not_support_governed_process_execution()
    {
        var capability = CreateCatalogCapability(WorkflowAgentCapabilityKeys.DefinitionsList);
        var agent = CreateAgent(
            AgentWorkspaceToolProfileKind.ArchitectureReview,
            [CreateAssignment(capability)]);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.ReadProcessContext],
            ProcessOperationContractNames.ManagedProcessArtifactsOnly);
        var descriptor = new AgentRuntimeToolProviderDescriptor(
            "tests.interactive-only-preflight-provider",
            "Interactive-only preflight provider",
            "Tests process preflight purpose enforcement.",
            ["tests"],
            [AgentRuntimeToolProviderPurpose.InteractiveChat]);
        var runtimeProvider = new CapabilityBoundRuntimeToolProvider(
            AgentToolInvocationPolicyMetadata.WorkflowsDefinitionsList,
            capability.Id,
            descriptor);
        var service = new ProcessRuntimeToolPreflightService(
            [runtimeProvider],
            [new DotNetSolutionSetupRuntimeToolPlanGuard(TestWorkspaceServices.PhysicalPathPolicyFactory)],
            ProcessRuntimeToolPreflightContributionCatalog.Empty,
            AvailableHostCapabilities);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                [AgentToolInvocationPolicyMetadata.WorkflowsDefinitionsList],
                [capability]),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        Assert.Equal(
            [AgentToolInvocationPolicyMetadata.WorkflowsDefinitionsList],
            result.MissingToolNames);
        Assert.Equal(0, runtimeProvider.InvocationCount);
    }

    [Fact]
    public async Task EvaluateAsync_reports_exact_workflow_capability_diagnostic_when_catalog_attachment_is_missing()
    {
        var capability = CreateCatalogCapability(WorkflowAgentCapabilityKeys.DefinitionsList);
        var agent = CreateAgent(
            AgentWorkspaceToolProfileKind.ArchitectureReview,
            [CreateAssignment(capability)]);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.ReadProcessContext],
            ProcessOperationContractNames.ManagedProcessArtifactsOnly);
        var service = new ProcessRuntimeToolPreflightService(
            [],
            [new DotNetSolutionSetupRuntimeToolPlanGuard(TestWorkspaceServices.PhysicalPathPolicyFactory)],
            ProcessRuntimeToolPreflightContributionCatalog.Empty,
            AvailableHostCapabilities);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                [AgentToolInvocationPolicyMetadata.WorkflowsDefinitionsList],
                CapabilityCatalog: []),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        Assert.Empty(result.MissingToolNames);
        var diagnostic = Assert.Single(result.CapabilityDiagnostics);
        Assert.Equal(WorkflowAgentCapabilityKeys.DefinitionsList, diagnostic.CapabilityKey);
    }

    [Fact]
    public async Task EvaluateAsync_filters_workflow_launch_without_launch_runtime_operation()
    {
        var capability = CreateCatalogCapability(WorkflowAgentCapabilityKeys.RunStart);
        var agent = CreateAgent(
            AgentWorkspaceToolProfileKind.ArchitectureReview,
            [CreateAssignment(capability)]);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.ReadProcessContext],
            ProcessOperationContractNames.ManagedProcessArtifactsOnly);
        var service = new ProcessRuntimeToolPreflightService(
            [new CapabilityBoundRuntimeToolProvider(
                AgentToolInvocationPolicyMetadata.WorkflowsRunStart,
                capability.Id)],
            [new DotNetSolutionSetupRuntimeToolPlanGuard(TestWorkspaceServices.PhysicalPathPolicyFactory)],
            ProcessRuntimeToolPreflightContributionCatalog.Empty,
            AvailableHostCapabilities);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                [AgentToolInvocationPolicyMetadata.WorkflowsRunStart],
                [capability]),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        Assert.Equal(
            [AgentToolInvocationPolicyMetadata.WorkflowsRunStart],
            result.MissingToolNames);
        Assert.Empty(result.CapabilityDiagnostics);
    }

    [Fact]
    public async Task EvaluateAsync_rejects_workspace_script_when_profile_can_expose_tool_but_agent_lacks_capability()
    {
        var agent = CreateAgent(AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.MutateProductTarget],
            ProcessOperationContractNames.ExternalProductTargetMutable);
        var service = CreatePreflightService();

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
        var service = CreatePreflightService();
        var resolverCalls = 0;

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["workspace_pwsh_run_script"],
                CapabilityCatalogResolver: _ =>
                {
                    resolverCalls++;
                    return ValueTask.FromResult<IReadOnlyList<CapabilityCatalogItem>>([]);
                }),
            CancellationToken.None);

        Assert.True(result.IsSatisfied, result.Summary);
        Assert.Equal(0, resolverCalls);
        Assert.Empty(result.MissingToolNames);
        Assert.Empty(result.CapabilityDiagnostics);
    }

    [Fact]
    public async Task EvaluateAsync_rejects_missing_host_capability_before_capability_catalog_resolution()
    {
        var agent = CreateAgent(
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            [CreateCapability("workspace-pwsh-run-script", CapabilityKind.Tool)]);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.MutateProductTarget],
            ProcessOperationContractNames.ExternalProductTargetMutable);
        var hostCapabilities = new StaticProcessHostCapabilitySnapshotProvider(
            new ProcessHostCapabilitySnapshot(
                new ProcessHostProfileId("linux"),
                [
                    new ProcessHostCapabilityFact(
                        ProcessHostCapabilityIds.PowerShellScript,
                        ProcessHostCapabilityAvailability.Unavailable,
                        ProcessHostCapabilityReason.DependencyMissing,
                        ProcessHostExecutionPort.None)
                ]));
        var service = new ProcessRuntimeToolPreflightService(
            [],
            [new DotNetSolutionSetupRuntimeToolPlanGuard(TestWorkspaceServices.PhysicalPathPolicyFactory)],
            ProcessRuntimeToolPreflightContributionCatalog.Empty,
            hostCapabilities);
        var resolverCalls = 0;

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["workspace_pwsh_run_script"],
                CapabilityCatalogResolver: _ =>
                {
                    resolverCalls++;
                    return ValueTask.FromResult<IReadOnlyList<CapabilityCatalogItem>>([]);
                }),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        Assert.Equal(0, resolverCalls);
        var finding = Assert.Single(result.HostCapabilityFindings);
        Assert.Equal(ProcessHostCapabilityIds.PowerShellScript, finding.CapabilityId);
        Assert.Equal(ProcessHostCapabilityReason.DependencyMissing, finding.Reason);
        Assert.Equal(new ProcessHostProfileId("linux"), finding.ProfileId);
    }

    [Fact]
    public async Task Spreadsheet_inspection_requires_python_host_capability()
    {
        var hostCapabilities = new StaticProcessHostCapabilitySnapshotProvider(
            new ProcessHostCapabilitySnapshot(
                new ProcessHostProfileId("linux"),
                [
                    new ProcessHostCapabilityFact(
                        ProcessHostCapabilityIds.PythonRuntime,
                        ProcessHostCapabilityAvailability.Unavailable,
                        ProcessHostCapabilityReason.DependencyMissing,
                        ProcessHostExecutionPort.None)
                ]));
        var service = new ProcessRuntimeToolPreflightService(
            [],
            [],
            ProcessRuntimeToolPreflightContributionCatalog.Empty,
            hostCapabilities);

        var result = await service.EvaluateHostCapabilitiesAsync(
            [ToolContractCatalog.WorkspaceInspectSpreadsheet],
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        var finding = Assert.Single(result.HostCapabilityFindings);
        Assert.Equal(ToolContractCatalog.WorkspaceInspectSpreadsheet, finding.RuntimeToolName);
        Assert.Equal(ProcessHostCapabilityIds.PythonRuntime, finding.CapabilityId);
        Assert.Equal(ProcessHostCapabilityReason.DependencyMissing, finding.Reason);
    }

    [Fact]
    public async Task Dotnet_stop_requires_owned_process_host_but_not_current_dotnet_runtime()
    {
        var hostCapabilities = new StaticProcessHostCapabilitySnapshotProvider(
            new ProcessHostCapabilitySnapshot(
                new ProcessHostProfileId("linux"),
                [
                    new ProcessHostCapabilityFact(
                        ProcessHostCapabilityIds.DirectExecution,
                        ProcessHostCapabilityAvailability.Available,
                        ProcessHostCapabilityReason.Ready,
                        ProcessHostExecutionPort.ManagedProcessHost),
                    new ProcessHostCapabilityFact(
                        ProcessHostCapabilityIds.DotNetRuntime,
                        ProcessHostCapabilityAvailability.Unavailable,
                        ProcessHostCapabilityReason.DependencyMissing,
                        ProcessHostExecutionPort.None)
                ]));
        var service = new ProcessRuntimeToolPreflightService(
            [],
            [],
            ProcessRuntimeToolPreflightContributionCatalog.Empty,
            hostCapabilities);

        var result = await service.EvaluateHostCapabilitiesAsync(
            [ToolContractCatalog.WorkspaceDotNetStop],
            CancellationToken.None);

        Assert.True(result.IsSatisfied, result.Summary);
        Assert.Empty(result.HostCapabilityFindings);
        var fact = Assert.Single(result.HostCapabilityEvidence!.Capabilities);
        Assert.Equal(ProcessHostCapabilityIds.DirectExecution, fact.Id);
        Assert.Equal(ProcessHostCapabilityAvailability.Available, fact.Availability);
    }

    [Fact]
    public async Task EvaluateAsync_fails_closed_when_mapped_tool_has_no_host_capability_provider()
    {
        var agent = CreateAgent(
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            [CreateCapability("workspace-pwsh-run-script", CapabilityKind.Tool)]);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.MutateProductTarget],
            ProcessOperationContractNames.ExternalProductTargetMutable);
        var service = new ProcessRuntimeToolPreflightService(
            [],
            [new DotNetSolutionSetupRuntimeToolPlanGuard(TestWorkspaceServices.PhysicalPathPolicyFactory)],
            ProcessRuntimeToolPreflightContributionCatalog.Empty);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["workspace_pwsh_run_script"]),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        var finding = Assert.Single(result.HostCapabilityFindings);
        Assert.Equal(new ProcessHostProfileId("unknown"), finding.ProfileId);
        Assert.Equal(ProcessHostCapabilityAvailability.Unavailable, finding.Availability);
        Assert.Equal(ProcessHostCapabilityReason.NotRegistered, finding.Reason);
        var evidenceFact = Assert.Single(result.HostCapabilityEvidence!.Capabilities);
        Assert.Equal(ProcessHostCapabilityIds.PowerShellScript, evidenceFact.Id);
        Assert.Equal(ProcessHostCapabilityReason.NotRegistered, evidenceFact.Reason);
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
        var service = CreatePreflightService();

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
        var service = CreatePreflightService();

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
        var service = CreatePreflightService();

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["workspace_write_file"]),
            CancellationToken.None);

        Assert.True(result.IsSatisfied, result.Summary);
        Assert.Empty(result.MissingToolNames);
        Assert.Empty(result.CapabilityDiagnostics);
    }

    [Fact]
    public async Task EvaluateAsync_satisfies_browser_tools_from_assigned_playwright_mcp_capability()
    {
        var capability = CreateMcpCatalogCapability("playwright-local-mcp", "stdio");
        var agent = CreateAgent(
            AgentWorkspaceToolProfileKind.QualityValidation,
            [CreateAssignment(capability)]);
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
                ],
                CapabilityCatalog: [capability]),
            CancellationToken.None);

        Assert.True(result.IsSatisfied);
        Assert.Empty(result.MissingToolNames);
        Assert.Empty(result.CapabilityDiagnostics);
        Assert.NotNull(result.HostCapabilityEvidence);
        Assert.Contains(
            result.HostCapabilityEvidence.Capabilities,
            fact => fact.Id == ProcessHostCapabilityIds.LocalStdioMcp);
        Assert.Contains(
            result.HostCapabilityEvidence.Capabilities,
            fact => fact.Id == ProcessHostCapabilityIds.NodeRuntime);
        Assert.Contains(
            result.HostCapabilityEvidence.Capabilities,
            fact => fact.Id == ProcessHostCapabilityIds.NodePackageManager);
    }

    [Fact]
    public async Task EvaluateAsync_evaluates_mapped_and_browser_host_requirements_from_one_snapshot()
    {
        var capability = CreateMcpCatalogCapability("playwright-local-mcp", "stdio");
        var spreadsheetCapability = CreateCatalogCapability("workspace-inspect-spreadsheet");
        var agent = CreateAgent(
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            [CreateAssignment(capability), CreateAssignment(spreadsheetCapability)]);
        var assignment = CreateAssignment(
            agent.Id,
            [
                ProcessOperationContractNames.CaptureRuntimeProof,
                ProcessOperationContractNames.LaunchRuntime,
                ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ],
            ProcessOperationContractNames.ExternalProductTargetReadOnly);
        var firstSnapshot = new ProcessHostCapabilitySnapshot(
            new ProcessHostProfileId("linux-first"),
            [
                new ProcessHostCapabilityFact(
                    ProcessHostCapabilityIds.PythonRuntime,
                    ProcessHostCapabilityAvailability.Available,
                    ProcessHostCapabilityReason.Ready,
                    ProcessHostExecutionPort.ManagedProcessHost),
                new ProcessHostCapabilityFact(
                    ProcessHostCapabilityIds.LocalStdioMcp,
                    ProcessHostCapabilityAvailability.Available,
                    ProcessHostCapabilityReason.Ready,
                    ProcessHostExecutionPort.LocalStdioMcpClient),
                new ProcessHostCapabilityFact(
                    ProcessHostCapabilityIds.NodeRuntime,
                    ProcessHostCapabilityAvailability.Available,
                    ProcessHostCapabilityReason.Ready,
                    ProcessHostExecutionPort.ManagedProcessHost),
                new ProcessHostCapabilityFact(
                    ProcessHostCapabilityIds.NodePackageManager,
                    ProcessHostCapabilityAvailability.Available,
                    ProcessHostCapabilityReason.Ready,
                    ProcessHostExecutionPort.ManagedProcessHost)
            ]);
        var secondSnapshot = new ProcessHostCapabilitySnapshot(
            new ProcessHostProfileId("linux-second"),
            [
                new ProcessHostCapabilityFact(
                    ProcessHostCapabilityIds.PythonRuntime,
                    ProcessHostCapabilityAvailability.Unavailable,
                    ProcessHostCapabilityReason.ProbePending,
                    ProcessHostExecutionPort.None),
                new ProcessHostCapabilityFact(
                    ProcessHostCapabilityIds.LocalStdioMcp,
                    ProcessHostCapabilityAvailability.Unavailable,
                    ProcessHostCapabilityReason.ProbePending,
                    ProcessHostExecutionPort.None),
                new ProcessHostCapabilityFact(
                    ProcessHostCapabilityIds.NodeRuntime,
                    ProcessHostCapabilityAvailability.Unavailable,
                    ProcessHostCapabilityReason.ProbePending,
                    ProcessHostExecutionPort.None),
                new ProcessHostCapabilityFact(
                    ProcessHostCapabilityIds.NodePackageManager,
                    ProcessHostCapabilityAvailability.Unavailable,
                    ProcessHostCapabilityReason.ProbePending,
                    ProcessHostExecutionPort.None)
            ]);
        var snapshotProvider = new SequentialProcessHostCapabilitySnapshotProvider(
            firstSnapshot,
            secondSnapshot);
        var service = new ProcessRuntimeToolPreflightService(
            [new CapabilityBoundRuntimeToolProvider(
                ToolContractCatalog.WorkspaceInspectSpreadsheet,
                spreadsheetCapability.Id)],
            [new DotNetSolutionSetupRuntimeToolPlanGuard(TestWorkspaceServices.PhysicalPathPolicyFactory)],
            new ProcessRuntimeToolPreflightContributionCatalog(
            [
                new BrowserRuntimeToolPreflightContribution()
            ]),
            snapshotProvider);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                [ToolContractCatalog.WorkspaceInspectSpreadsheet, "browser_snapshot"],
                CapabilityCatalog: [capability, spreadsheetCapability]),
            CancellationToken.None);

        Assert.True(result.IsSatisfied, result.Summary);
        Assert.Equal(1, snapshotProvider.CallCount);
        Assert.NotNull(result.HostCapabilityEvidence);
        Assert.Equal(firstSnapshot.ProfileId, result.HostCapabilityEvidence.ProfileId);
        Assert.Equal(
            [
                ProcessHostCapabilityIds.LocalStdioMcp,
                ProcessHostCapabilityIds.NodeRuntime,
                ProcessHostCapabilityIds.NodePackageManager,
                ProcessHostCapabilityIds.PythonRuntime
            ],
            result.HostCapabilityEvidence.Capabilities.Select(fact => fact.Id).ToArray());
    }

    [Fact]
    public async Task EvaluateAsync_blocks_local_browser_mcp_before_composition_when_host_port_is_unavailable()
    {
        var capability = CreateMcpCatalogCapability("playwright-local-mcp", "stdio");
        var agent = CreateAgent(
            AgentWorkspaceToolProfileKind.QualityValidation,
            [CreateAssignment(capability)]);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.CaptureRuntimeProof],
            ProcessOperationContractNames.ExternalProductTargetReadOnly);
        var unavailableHost = new StaticProcessHostCapabilitySnapshotProvider(
            new ProcessHostCapabilitySnapshot(
                new ProcessHostProfileId("linux"),
                [
                    new ProcessHostCapabilityFact(
                        ProcessHostCapabilityIds.LocalStdioMcp,
                        ProcessHostCapabilityAvailability.Unavailable,
                        ProcessHostCapabilityReason.NotRegistered,
                        ProcessHostExecutionPort.None),
                    new ProcessHostCapabilityFact(
                        ProcessHostCapabilityIds.NodeRuntime,
                        ProcessHostCapabilityAvailability.Available,
                        ProcessHostCapabilityReason.Ready,
                        ProcessHostExecutionPort.ManagedProcessHost),
                    new ProcessHostCapabilityFact(
                        ProcessHostCapabilityIds.NodePackageManager,
                        ProcessHostCapabilityAvailability.Available,
                        ProcessHostCapabilityReason.Ready,
                        ProcessHostExecutionPort.ManagedProcessHost)
                ]));
        var service = CreateBrowserPreflightService(unavailableHost);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["browser_snapshot"],
                CapabilityCatalog: [capability]),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        var finding = Assert.Single(result.HostCapabilityFindings);
        Assert.Equal(ProcessHostCapabilityIds.LocalStdioMcp, finding.CapabilityId);
        Assert.Equal(ProcessHostCapabilityReason.NotRegistered, finding.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_blocks_local_playwright_mcp_when_node_runtime_is_unavailable()
    {
        var capability = CreateMcpCatalogCapability("playwright-local-mcp", "stdio");
        var agent = CreateAgent(
            AgentWorkspaceToolProfileKind.QualityValidation,
            [CreateAssignment(capability)]);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.CaptureRuntimeProof],
            ProcessOperationContractNames.ExternalProductTargetReadOnly);
        var unavailableHost = new StaticProcessHostCapabilitySnapshotProvider(
            new ProcessHostCapabilitySnapshot(
                new ProcessHostProfileId("linux"),
                [
                    new ProcessHostCapabilityFact(
                        ProcessHostCapabilityIds.LocalStdioMcp,
                        ProcessHostCapabilityAvailability.Available,
                        ProcessHostCapabilityReason.Ready,
                        ProcessHostExecutionPort.LocalStdioMcpClient),
                    new ProcessHostCapabilityFact(
                        ProcessHostCapabilityIds.NodeRuntime,
                        ProcessHostCapabilityAvailability.Unavailable,
                        ProcessHostCapabilityReason.DependencyMissing,
                        ProcessHostExecutionPort.None),
                    new ProcessHostCapabilityFact(
                        ProcessHostCapabilityIds.NodePackageManager,
                        ProcessHostCapabilityAvailability.Available,
                        ProcessHostCapabilityReason.Ready,
                        ProcessHostExecutionPort.ManagedProcessHost)
                ]));
        var service = CreateBrowserPreflightService(unavailableHost);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["browser_snapshot"],
                CapabilityCatalog: [capability]),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        var finding = Assert.Single(result.HostCapabilityFindings);
        Assert.Equal(ProcessHostCapabilityIds.NodeRuntime, finding.CapabilityId);
        Assert.Equal(ProcessHostCapabilityReason.DependencyMissing, finding.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_blocks_local_playwright_mcp_when_node_package_manager_is_unavailable()
    {
        var capability = CreateMcpCatalogCapability("playwright-local-mcp", "stdio");
        var agent = CreateAgent(
            AgentWorkspaceToolProfileKind.QualityValidation,
            [CreateAssignment(capability)]);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.CaptureRuntimeProof],
            ProcessOperationContractNames.ExternalProductTargetReadOnly);
        var unavailableHost = new StaticProcessHostCapabilitySnapshotProvider(
            new ProcessHostCapabilitySnapshot(
                new ProcessHostProfileId("linux"),
                [
                    new ProcessHostCapabilityFact(
                        ProcessHostCapabilityIds.LocalStdioMcp,
                        ProcessHostCapabilityAvailability.Available,
                        ProcessHostCapabilityReason.Ready,
                        ProcessHostExecutionPort.LocalStdioMcpClient),
                    new ProcessHostCapabilityFact(
                        ProcessHostCapabilityIds.NodeRuntime,
                        ProcessHostCapabilityAvailability.Available,
                        ProcessHostCapabilityReason.Ready,
                        ProcessHostExecutionPort.ManagedProcessHost),
                    new ProcessHostCapabilityFact(
                        ProcessHostCapabilityIds.NodePackageManager,
                        ProcessHostCapabilityAvailability.Unavailable,
                        ProcessHostCapabilityReason.DependencyMissing,
                        ProcessHostExecutionPort.None)
                ]));
        var service = CreateBrowserPreflightService(unavailableHost);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["browser_snapshot"],
                CapabilityCatalog: [capability]),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        var finding = Assert.Single(result.HostCapabilityFindings);
        Assert.Equal(ProcessHostCapabilityIds.NodePackageManager, finding.CapabilityId);
        Assert.Equal(ProcessHostCapabilityReason.DependencyMissing, finding.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_allows_remote_browser_mcp_without_local_stdio_host_port()
    {
        var capability = CreateMcpCatalogCapability("playwright-remote-mcp", "http");
        var agent = CreateAgent(
            AgentWorkspaceToolProfileKind.QualityValidation,
            [CreateAssignment(capability)]);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.CaptureRuntimeProof],
            ProcessOperationContractNames.ExternalProductTargetReadOnly);
        var unavailableHost = new StaticProcessHostCapabilitySnapshotProvider(
            new ProcessHostCapabilitySnapshot(
                new ProcessHostProfileId("linux"),
                [
                    new ProcessHostCapabilityFact(
                        ProcessHostCapabilityIds.LocalStdioMcp,
                        ProcessHostCapabilityAvailability.Unavailable,
                        ProcessHostCapabilityReason.NotRegistered,
                        ProcessHostExecutionPort.None)
                ]));
        var service = CreateBrowserPreflightService(unavailableHost);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["browser_snapshot"],
                CapabilityCatalog: [capability]),
            CancellationToken.None);

        Assert.True(result.IsSatisfied);
        Assert.Empty(result.HostCapabilityFindings);
    }

    [Theory]
    [InlineData("custom-browser-host")]
    [InlineData("npx")]
    public async Task EvaluateAsync_rejects_ambiguous_mixed_remote_and_local_browser_mcp_assignments(
        string localCommand)
    {
        var remoteCapability = CreateMcpCatalogCapability("playwright-remote-mcp", "http");
        var localCapability = CreateMcpCatalogCapability(
            "playwright-local-mcp",
            "stdio",
            localCommand);
        var agent = CreateAgent(
            AgentWorkspaceToolProfileKind.QualityValidation,
            [CreateAssignment(remoteCapability), CreateAssignment(localCapability)]);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.CaptureRuntimeProof],
            ProcessOperationContractNames.ExternalProductTargetReadOnly);
        var unavailableHost = new StaticProcessHostCapabilitySnapshotProvider(
            new ProcessHostCapabilitySnapshot(
                new ProcessHostProfileId("linux"),
                [
                    new ProcessHostCapabilityFact(
                        ProcessHostCapabilityIds.LocalStdioMcp,
                        ProcessHostCapabilityAvailability.Unavailable,
                        ProcessHostCapabilityReason.NotRegistered,
                        ProcessHostExecutionPort.None),
                    new ProcessHostCapabilityFact(
                        ProcessHostCapabilityIds.NodeRuntime,
                        ProcessHostCapabilityAvailability.Unavailable,
                        ProcessHostCapabilityReason.DependencyMissing,
                        ProcessHostExecutionPort.None)
                ]));
        var service = CreateBrowserPreflightService(unavailableHost);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["browser_snapshot"],
                CapabilityCatalog: [remoteCapability, localCapability]),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        Assert.Equal(["invalid-browser-mcp-transport-contract"], result.MissingToolNames);
        Assert.Empty(result.HostCapabilityFindings);
    }

    [Fact]
    public async Task EvaluateAsync_does_not_satisfy_browser_tools_without_runtime_proof_operation()
    {
        var capability = CreateMcpCatalogCapability("playwright-local-mcp", "stdio");
        var agent = CreateAgent(
            AgentWorkspaceToolProfileKind.QualityValidation,
            [CreateAssignment(capability)]);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.RunValidation],
            ProcessOperationContractNames.ExternalProductTargetReadOnly);
        var service = CreateBrowserPreflightService();

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["browser_snapshot"],
                CapabilityCatalog: [capability]),
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
        var service = CreatePreflightService();

        var resolverCalls = 0;
        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                [],
                CapabilityCatalogResolver: _ =>
                {
                    resolverCalls++;
                    return ValueTask.FromResult<IReadOnlyList<CapabilityCatalogItem>>([]);
                }),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        Assert.Equal(0, resolverCalls);
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
        var service = CreatePreflightService();

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
        var service = CreatePreflightService();

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
        var service = CreatePreflightService();

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
        var service = CreatePreflightService();

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
        var service = CreatePreflightService();

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
        var service = CreatePreflightService();

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                []),
            CancellationToken.None);

        Assert.True(result.IsSatisfied, result.Summary);
        Assert.Empty(result.PlanIssues);
    }

    private static ProcessRuntimeToolPreflightService CreateBrowserPreflightService(
        IProcessHostCapabilitySnapshotProvider? hostCapabilities = null)
    {
        return new ProcessRuntimeToolPreflightService(
            [],
            [new DotNetSolutionSetupRuntimeToolPlanGuard(TestWorkspaceServices.PhysicalPathPolicyFactory)],
            new ProcessRuntimeToolPreflightContributionCatalog(
            [
                new BrowserRuntimeToolPreflightContribution()
            ]),
            hostCapabilities ?? AvailableHostCapabilities);
    }

    private static ProcessRuntimeToolPreflightService CreatePreflightService()
    {
        return new ProcessRuntimeToolPreflightService(
            [],
            [new DotNetSolutionSetupRuntimeToolPlanGuard(TestWorkspaceServices.PhysicalPathPolicyFactory)],
            ProcessRuntimeToolPreflightContributionCatalog.Empty,
            AvailableHostCapabilities);
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

    private sealed class StaticProcessHostCapabilitySnapshotProvider(
        ProcessHostCapabilitySnapshot snapshot) : IProcessHostCapabilitySnapshotProvider
    {
        public ValueTask<ProcessHostCapabilitySnapshot> GetAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(snapshot);
        }
    }

    private sealed class SequentialProcessHostCapabilitySnapshotProvider(
        params ProcessHostCapabilitySnapshot[] snapshots) : IProcessHostCapabilitySnapshotProvider
    {
        private int callCount;

        public int CallCount => Volatile.Read(ref callCount);

        public ValueTask<ProcessHostCapabilitySnapshot> GetAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var index = Interlocked.Increment(ref callCount) - 1;
            return ValueTask.FromResult(snapshots[Math.Min(index, snapshots.Length - 1)]);
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

    private static CapabilityCatalogItem CreateCatalogCapability(string capabilityKey)
    {
        return new CapabilityCatalogItem(
            Guid.NewGuid(),
            CapabilityKind.Tool,
            capabilityKey,
            capabilityKey,
            string.Empty,
            string.Empty,
            string.Empty,
            CapabilityProofStatus.Verified,
            string.Empty,
            LastVerifiedAtUtc: null,
            IsBuiltIn: true);
    }

    private static CapabilityCatalogItem CreateMcpCatalogCapability(
        string capabilityKey,
        string transport,
        string? command = null)
    {
        return new CapabilityCatalogItem(
            Guid.NewGuid(),
            CapabilityKind.McpServer,
            capabilityKey,
            capabilityKey,
            string.Empty,
            string.Empty,
            JsonSerializer.Serialize(new
            {
                transport,
                command = string.Equals(transport, "stdio", StringComparison.OrdinalIgnoreCase)
                    ? command ?? "npx"
                    : null
            }),
            CapabilityProofStatus.Verified,
            string.Empty,
            LastVerifiedAtUtc: null,
            IsBuiltIn: true);
    }

    private static AgentCapabilityAssignment CreateAssignment(CapabilityCatalogItem capability)
    {
        return new AgentCapabilityAssignment(
            capability.Id,
            capability.Key,
            capability.Kind,
            capability.ProofStatus,
            capability.LastVerifiedAtUtc,
            capability.ProofNotes);
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

    private static ProcessHostExecutionPort ResolveExecutionPort(ProcessHostCapabilityId capabilityId)
    {
        if (capabilityId == ProcessHostCapabilityIds.ManagedProcessAdapter)
        {
            return ProcessHostExecutionPort.ManagedProcessAdapter;
        }

        if (capabilityId == ProcessHostCapabilityIds.Docker)
        {
            return ProcessHostExecutionPort.DockerHostTool;
        }

        if (capabilityId == ProcessHostCapabilityIds.LocalStdioMcp)
        {
            return ProcessHostExecutionPort.LocalStdioMcpClient;
        }

        if (capabilityId == ProcessHostCapabilityIds.DesktopOpen)
        {
            return ProcessHostExecutionPort.DesktopLauncher;
        }

        if (capabilityId == ProcessHostCapabilityIds.InteractiveTerminal)
        {
            return ProcessHostExecutionPort.InteractiveTerminal;
        }

        return ProcessHostExecutionPort.ManagedProcessHost;
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
            ["DotNetSolutionFile"] = solutionFile,
            ["DotNetSolutionFileCandidates"] = solutionFile,
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

    private sealed class CapabilityBoundRuntimeToolProvider(
        string toolName,
        Guid requiredCapabilityId,
        AgentRuntimeToolProviderDescriptor? descriptor = null) : IAgentRuntimeToolProvider
    {
        public int Order => 1;

        public AgentRuntimeToolProviderDescriptor? Descriptor => descriptor;

        public int InvocationCount { get; private set; }

        public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(
            AgentRuntimeToolProviderContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InvocationCount++;
            return context.Capabilities.Count(capability => capability.Id == requiredCapabilityId) == 1
                ? ValueTask.FromResult<IReadOnlyList<AITool>>(
                [
                    AIFunctionFactory.Create(
                        () => "available",
                        toolName,
                        "Test capability-bound runtime tool.")
                ])
                : ValueTask.FromResult<IReadOnlyList<AITool>>([]);
        }
    }
}
