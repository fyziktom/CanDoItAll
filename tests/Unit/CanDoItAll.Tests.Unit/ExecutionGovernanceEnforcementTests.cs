using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit;

/// <summary>
/// The admitted execution governance snapshot is the single runtime permission
/// input: composition excludes tools outside it, and the invocation policy
/// independently denies calls that exceed it — even when an approval path or
/// auto-approval would otherwise allow the tool.
/// </summary>
public sealed class ExecutionGovernanceEnforcementTests
{
    [Fact]
    public async Task Read_only_authority_denies_mutation_tool_even_with_auto_approval()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateInteractiveContext(
            ToolContractCatalog.WorkspaceWriteFile,
            ToolInvocationClassification.Mutation,
            CreateGovernance(readAllowed: true, mutationAllowed: false),
            autoApprovalAllowed: true,
            approvalWrapperAvailable: true);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("read-only", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Read_only_authority_still_allows_read_tools()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateInteractiveContext(
            ToolContractCatalog.WorkspaceReadFile,
            ToolInvocationClassification.Read,
            CreateGovernance(readAllowed: true, mutationAllowed: false),
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task Authority_without_workspace_read_denies_workspace_read_tools()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateInteractiveContext(
            ToolContractCatalog.WorkspaceReadFile,
            ToolInvocationClassification.Read,
            CreateGovernance(readAllowed: false, mutationAllowed: false),
            autoApprovalAllowed: true,
            approvalWrapperAvailable: true);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("read access", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Composition_filter_excludes_mutation_tools_for_read_only_authority()
    {
        var capabilityState = new RuntimeCapabilityState();
        capabilityState.Tools.Add(AIFunctionFactory.Create(
            () => "written",
            ToolContractCatalog.WorkspaceWriteFile,
            "Writes a file."));
        capabilityState.Tools.Add(AIFunctionFactory.Create(
            () => "content",
            ToolContractCatalog.WorkspaceReadFile,
            "Reads a file."));
        var progressMessages = new List<string>();

        await MafRuntimeAgentFactory.FilterToolsOutsideExecutionGovernanceAsync(
            capabilityState,
            CreateGovernance(readAllowed: true, mutationAllowed: false),
            (_, _, message) =>
            {
                progressMessages.Add(message);
                return Task.CompletedTask;
            });

        var remainingTool = Assert.Single(capabilityState.Tools);
        Assert.Equal(ToolContractCatalog.WorkspaceReadFile, remainingTool.Name);
        Assert.Contains(progressMessages, message =>
            message.Contains(ToolContractCatalog.WorkspaceWriteFile, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Composition_filter_keeps_all_tools_when_no_snapshot_is_present()
    {
        var capabilityState = new RuntimeCapabilityState();
        capabilityState.Tools.Add(AIFunctionFactory.Create(
            () => "written",
            ToolContractCatalog.WorkspaceWriteFile,
            "Writes a file."));

        await MafRuntimeAgentFactory.FilterToolsOutsideExecutionGovernanceAsync(
            capabilityState,
            governance: null,
            (_, _, _) => Task.CompletedTask);

        Assert.Single(capabilityState.Tools);
    }

    [Fact]
    public void Authority_projection_round_trips_into_the_governance_snapshot()
    {
        var authority = CreateAuthorityRecord(
            readAllowed: true,
            mutationAllowed: false,
            allowedOperations: ["ReadProjectStructure", "RunValidation"],
            allowedExternalTargetAliases: ["external-target/c/products/demo"]);
        var turnReference = CreateTurnReference();

        var metadataJson = AgentTurnContextMetadata.Apply("{}", turnReference, authority);
        var snapshot = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(metadataJson);

        Assert.NotNull(snapshot);
        Assert.Equal(authority.AuthorityId, snapshot!.AuthorityId);
        Assert.Equal(authority.AgentId, snapshot.AgentId);
        Assert.Equal(authority.DatabaseProfileId, snapshot.DatabaseProfileId);
        Assert.Equal(authority.WorkspaceScope, snapshot.WorkspaceScope);
        Assert.True(snapshot.ReadAllowed);
        Assert.False(snapshot.MutationAllowed);
        Assert.Equal(authority.PolicyFingerprint, snapshot.PolicyFingerprint);
        Assert.Contains("ReadProjectStructure", snapshot.AllowedOperations);
        Assert.Contains("external-target/c/products/demo", snapshot.WritableExternalTargetAliases);
    }

    [Fact]
    public void Tampered_or_missing_authority_projection_yields_no_snapshot()
    {
        Assert.Null(AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(null));
        Assert.Null(AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot("{}"));
        Assert.Null(AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot("not-json"));
        Assert.Null(AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(
            """{"agentExecutionAuthority":{"authorityId":"tampered","workspaceScopeKind":"Project"}}"""));
    }

    private static ToolInvocationPolicyContext CreateInteractiveContext(
        string toolName,
        ToolInvocationClassification classification,
        AgentExecutionGovernanceSnapshot governance,
        bool autoApprovalAllowed,
        bool approvalWrapperAvailable)
    {
        var redactedArguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["path"] = "artifacts/report.md"
        };
        return new ToolInvocationPolicyContext(
            AgentId: governance.AgentId,
            AgentName: "Governance enforcement agent",
            ToolName: toolName,
            RedactedArguments: redactedArguments,
            Classification: classification,
            IsKnownTool: true,
            AutoApprovalAllowed: autoApprovalAllowed,
            ApprovalWrapperAvailable: approvalWrapperAvailable,
            ExecutionRunId: Guid.NewGuid().ToString("D"),
            SourceKind: "project-structure",
            ProcessRunId: string.Empty,
            ProcessStepId: string.Empty,
            ApprovalWrapperEffectiveForProvider: approvalWrapperAvailable)
        {
            ExecutionGovernance = governance,
            PathArguments = ToolInvocationPathArgumentResolver.Resolve(
                toolName,
                redactedArguments.Select(argument =>
                    new KeyValuePair<string, object?>(argument.Key, argument.Value)))
        };
    }

    private static AgentExecutionGovernanceSnapshot CreateGovernance(
        bool readAllowed,
        bool mutationAllowed)
        => AgentExecutionGovernanceSnapshot.FromAuthority(CreateAuthorityRecord(readAllowed, mutationAllowed));

    private static AgentExecutionAuthorityRecord CreateAuthorityRecord(
        bool readAllowed,
        bool mutationAllowed,
        IReadOnlyList<string>? allowedOperations = null,
        IReadOnlyList<string>? allowedExternalTargetAliases = null)
        => new(
            AgentExecutionAuthorityId.Create(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DatabaseProfileGeneration(1),
            WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")),
            readAllowed,
            mutationAllowed,
            "v2-canonical",
            "policy-fingerprint-value",
            DateTimeOffset.UtcNow,
            allowedOperations: allowedOperations,
            allowedExternalTargetAliases: allowedExternalTargetAliases);

    private static AgentTurnContextReference CreateTurnReference()
        => new(
            AgentTurnContextId.Create(),
            AgentContextEpochId.Create(),
            new AgentChatContextSourceKind("project-structure"),
            new AgentChatContextSourceId(Guid.NewGuid().ToString("D")),
            surface: "project-structure",
            view: "hierarchy",
            observationVersion: 3,
            modelContextDigest: "digest-value",
            capturedAtUtc: DateTimeOffset.UtcNow);
}
