using System.Reflection;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Integration;

public sealed class AgentFrameworkExecutionCapabilityFilteringIntegrationTests
{
    [Fact]
    public void ResolveAttachedCapabilities_filters_retired_workspace_delivery_skill_from_execution_input()
    {
        var retainedCapability = new CapabilityCatalogItem(
            Guid.NewGuid(),
            CapabilityKind.Tool,
            "workspace-read-file",
            "Workspace Read File",
            "Reads a file from the current workspace.",
            string.Empty,
            """{"tool":"workspace_read_file"}""",
            CapabilityProofStatus.NotRun,
            string.Empty,
            null,
            true);
        var retiredCapability = new CapabilityCatalogItem(
            Guid.NewGuid(),
            CapabilityKind.Skill,
            "legacy-workspace-delivery",
            "Legacy Workspace Delivery",
            "Stale legacy capability imported from an old sandbox catalog.",
            string.Empty,
            """{"registeredSkillServiceType":"CanDoItAll.AgentFramework.Sandbox.Hosting.WorkspaceDeliverySkill, CanDoItAll.AgentFramework.Sandbox"}""",
            CapabilityProofStatus.NotRun,
            string.Empty,
            null,
            true);
        var agent = new AgentDefinition(
            Guid.NewGuid(),
            "Execution Agent",
            "Delivery",
            "Executes workspace runs.",
            "Do the work.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: "gpt-4.1",
            Workload: AgentWorkloadKind.Programming,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities:
            [
                new AgentCapabilityAssignment(
                    retainedCapability.Id,
                    retainedCapability.Key,
                    retainedCapability.Kind,
                    retainedCapability.ProofStatus,
                    retainedCapability.LastVerifiedAtUtc,
                    retainedCapability.ProofNotes),
                new AgentCapabilityAssignment(
                    retiredCapability.Id,
                    retiredCapability.Key,
                    retiredCapability.Kind,
                    retiredCapability.ProofStatus,
                    retiredCapability.LastVerifiedAtUtc,
                    retiredCapability.ProofNotes)
            ],
            Tags: [],
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow);
        var catalog = new SandboxWorkspaceCatalog(
            Version: "1.0",
            Agents: [agent],
            Providers: [],
            Capabilities: [retainedCapability, retiredCapability],
            Memory: []);

        var resolved = InvokeResolveAttachedCapabilities(catalog, agent);

        var attachedCapability = Assert.Single(resolved);
        Assert.Equal(retainedCapability.Id, attachedCapability.Id);
    }

    [Fact]
    public void ResolveAttachedCapabilities_filters_capability_when_raw_configuration_keeps_legacy_workspace_delivery_marker()
    {
        var retainedCapability = new CapabilityCatalogItem(
            Guid.NewGuid(),
            CapabilityKind.Tool,
            "workspace-read-file",
            "Workspace Read File",
            "Reads a file from the current workspace.",
            string.Empty,
            """{"tool":"workspace_read_file"}""",
            CapabilityProofStatus.NotRun,
            string.Empty,
            null,
            true);
        var retiredCapability = new CapabilityCatalogItem(
            Guid.NewGuid(),
            CapabilityKind.Skill,
            "legacy-workspace-capability",
            "Legacy Workspace Capability",
            "Stale legacy capability imported from an old sandbox catalog.",
            string.Empty,
            """{"registeredSkillServiceType":"Legacy.WorkspaceDeliverySkill, Legacy.Sandbox","legacyServiceType":"CanDoItAll.AgentFramework.Sandbox.Hosting.WorkspaceDeliverySkill, CanDoItAll.AgentFramework.Sandbox"}""",
            CapabilityProofStatus.NotRun,
            string.Empty,
            null,
            true);
        var agent = new AgentDefinition(
            Guid.NewGuid(),
            "Execution Agent",
            "Delivery",
            "Executes workspace runs.",
            "Do the work.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: "gpt-4.1",
            Workload: AgentWorkloadKind.Programming,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities:
            [
                new AgentCapabilityAssignment(
                    retainedCapability.Id,
                    retainedCapability.Key,
                    retainedCapability.Kind,
                    retainedCapability.ProofStatus,
                    retainedCapability.LastVerifiedAtUtc,
                    retainedCapability.ProofNotes),
                new AgentCapabilityAssignment(
                    retiredCapability.Id,
                    retiredCapability.Key,
                    retiredCapability.Kind,
                    retiredCapability.ProofStatus,
                    retiredCapability.LastVerifiedAtUtc,
                    retiredCapability.ProofNotes)
            ],
            Tags: [],
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow);
        var catalog = new SandboxWorkspaceCatalog(
            Version: "1.0",
            Agents: [agent],
            Providers: [],
            Capabilities: [retainedCapability, retiredCapability],
            Memory: []);

        var resolved = InvokeResolveAttachedCapabilities(catalog, agent);

        var attachedCapability = Assert.Single(resolved);
        Assert.Equal(retainedCapability.Id, attachedCapability.Id);
    }

    [Fact]
    public void ResolveAgentMemoryForRun_suppresses_unscoped_memory_for_governed_process_steps()
    {
        var agentId = Guid.NewGuid();
        var memory = new AgentMemoryRecord(
            Guid.NewGuid(),
            agentId,
            MemoryKind.Context,
            "Prior app target",
            "Use external-target/C/old/sample-app.",
            "manual",
            10,
            "{}",
            DateTimeOffset.UtcNow);
        var catalog = new SandboxWorkspaceCatalog(
            Version: "1.0",
            Agents: [],
            Providers: [],
            Capabilities: [],
            Memory: [memory]);

        var manualRun = CreateExecutionRun(agentId, "manual", processRunId: string.Empty, processStepId: string.Empty);
        var governedRun = CreateExecutionRun(agentId, "process-step", processRunId: "run-001", processStepId: "step-001");

        Assert.Single(InvokeResolveAgentMemoryForRun(catalog, agentId, manualRun));
        Assert.Empty(InvokeResolveAgentMemoryForRun(catalog, agentId, governedRun));
    }

    private static IReadOnlyList<CapabilityCatalogItem> InvokeResolveAttachedCapabilities(
        SandboxWorkspaceCatalog catalog,
        AgentDefinition agent)
    {
        var executionServiceType = Type.GetType(
            "CanDoItAll.AgentFramework.Core.AgentFrameworkWorkspaceExecutionService, CanDoItAll.AgentFramework.Core",
            throwOnError: true)
            ?? throw new InvalidOperationException("Could not resolve AgentFrameworkWorkspaceExecutionService.");
        var method = executionServiceType
            .GetMethod("ResolveAttachedCapabilities", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find ResolveAttachedCapabilities.");

        return Assert.IsAssignableFrom<IReadOnlyList<CapabilityCatalogItem>>(
            method.Invoke(null, [catalog, agent]));
    }

    private static IReadOnlyList<AgentMemoryRecord> InvokeResolveAgentMemoryForRun(
        SandboxWorkspaceCatalog catalog,
        Guid agentId,
        ExecutionRunRecord run)
    {
        var executionServiceType = Type.GetType(
            "CanDoItAll.AgentFramework.Core.AgentFrameworkWorkspaceExecutionService, CanDoItAll.AgentFramework.Core",
            throwOnError: true)
            ?? throw new InvalidOperationException("Could not resolve AgentFrameworkWorkspaceExecutionService.");
        var method = executionServiceType
            .GetMethod("ResolveAgentMemoryForRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find ResolveAgentMemoryForRun.");

        return Assert.IsAssignableFrom<IReadOnlyList<AgentMemoryRecord>>(
            method.Invoke(null, [catalog, agentId, run]));
    }

    private static ExecutionRunRecord CreateExecutionRun(
        Guid agentId,
        string sourceKind,
        string processRunId,
        string processStepId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: agentId,
            ChatSessionId: null,
            Title: "Test run",
            SourceKind: sourceKind,
            SourceId: "source-001",
            CorrelationId: string.Empty,
            CausationId: string.Empty,
            RequestedBy: "test",
            RequestedByKind: "test",
            MetadataJson: "{}",
            InputSummary: string.Empty,
            ResultSummary: string.Empty,
            ProviderName: "test",
            Model: "test-model",
            State: ExecutionState.Preparing,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [],
            ProcessRunId: processRunId,
            ProcessStepId: processStepId);
    }
}
