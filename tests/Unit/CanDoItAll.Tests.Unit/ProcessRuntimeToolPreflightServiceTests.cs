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
    public async Task EvaluateAsync_satisfies_workspace_script_from_software_development_profile_without_registered_provider()
    {
        var agent = CreateAgent(AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.MutateProductTarget],
            ProcessOperationContractNames.ExternalProductTargetMutable);
        var service = new ProcessRuntimeToolPreflightService([]);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["workspace_pwsh_run_script"]),
            CancellationToken.None);

        Assert.True(result.IsSatisfied);
        Assert.Empty(result.MissingToolNames);
    }

    [Fact]
    public async Task EvaluateAsync_missing_workspace_script_when_agent_profile_disables_local_scripts()
    {
        var agent = CreateAgent(AgentWorkspaceToolProfileKind.ArchitectureReview);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.MutateProductTarget],
            ProcessOperationContractNames.ExternalProductTargetMutable);
        var service = new ProcessRuntimeToolPreflightService([]);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["workspace_pwsh_run_script"]),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        Assert.Equal(["workspace_pwsh_run_script"], result.MissingToolNames);
    }

    [Fact]
    public async Task EvaluateAsync_missing_workspace_script_when_operation_contract_disallows_scripts()
    {
        var agent = CreateAgent(AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.WriteManagedProcessArtifacts],
            ProcessOperationContractNames.ManagedProcessArtifactsOnly);
        var service = new ProcessRuntimeToolPreflightService([]);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["workspace_pwsh_run_script"]),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        Assert.Equal(["workspace_pwsh_run_script"], result.MissingToolNames);
    }

    [Fact]
    public async Task EvaluateAsync_satisfies_managed_artifact_write_without_product_mutation()
    {
        var agent = CreateAgent(AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.WriteManagedProcessArtifacts],
            ProcessOperationContractNames.ManagedProcessArtifactsOnly);
        var service = new ProcessRuntimeToolPreflightService([]);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["workspace_write_file"]),
            CancellationToken.None);

        Assert.True(result.IsSatisfied);
        Assert.Empty(result.MissingToolNames);
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
        var service = new ProcessRuntimeToolPreflightService([]);

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
        var service = new ProcessRuntimeToolPreflightService([]);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["browser_snapshot"]),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        Assert.Equal(["browser_snapshot"], result.MissingToolNames);
    }

    [Fact]
    public async Task EvaluateAsync_missing_browser_tools_without_playwright_mcp_capability()
    {
        var agent = CreateAgent(AgentWorkspaceToolProfileKind.QualityValidation);
        var assignment = CreateAssignment(
            agent.Id,
            [ProcessOperationContractNames.CaptureRuntimeProof],
            ProcessOperationContractNames.ExternalProductTargetReadOnly);
        var service = new ProcessRuntimeToolPreflightService([]);

        var result = await service.EvaluateAsync(
            new ProcessRuntimeToolPreflightRequest(
                assignment,
                agent,
                ["browser_snapshot"]),
            CancellationToken.None);

        Assert.False(result.IsSatisfied);
        Assert.Equal(["browser_snapshot"], result.MissingToolNames);
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
}
