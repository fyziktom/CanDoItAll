using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafToolPolicyContextFactoryTests {
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, false, false, true)]
    public void Mapping_preserves_approval_semantics_and_sensitive_argument_redaction_without_runtime_or_filesystem(
        bool automatic, bool wrapped, bool effective, bool application) {
        var agent = Agent();
        var arguments = new KeyValuePair<string, object?>[] { new("path", "report.txt"), new("password", "test credential value") };
        var inspections = 0;
        var context = MafToolInvocationPolicyContextFactory.Create(agent, ToolContractCatalog.WorkspaceWriteFile, arguments,
            ToolInvocationClassification.Mutation, AgentToolPolicyCatalog.BuiltIn, true, new(automatic, wrapped, effective, application),
            null, (name, supplied, audit, manifest, targets) => {
                inspections++;
                Assert.Equal(ToolContractCatalog.WorkspaceWriteFile, name);
                Assert.Same(arguments, supplied);
                Assert.Null(audit);
                Assert.Empty(manifest);
                Assert.NotNull(targets);
                return new("inspected script", "inspection unavailable");
            }, null, []);

        Assert.Equal(1, inspections);
        Assert.Equal(agent.Id, context.AgentId);
        Assert.Equal(agent.Name, context.AgentName);
        Assert.Equal(automatic, context.AutoApprovalAllowed);
        Assert.Equal(wrapped, context.ApprovalWrapperAvailable);
        Assert.Equal(effective, context.ApprovalWrapperEffectiveForProvider);
        Assert.Equal(application, context.ApplicationApprovalAvailable);
        Assert.Equal(wrapped && effective || application, context.HasEffectiveApprovalPath);
        Assert.Equal("inspected script", context.InspectedScriptContent);
        Assert.Equal("inspection unavailable", context.ScriptInspectionFailure);
        Assert.DoesNotContain("test credential value", context.RedactedArguments.Values);
        Assert.Empty(context.ProcessRunId);
        Assert.Empty(context.ProcessStepId);
        Assert.Null(context.ScopePolicy);
        Assert.NotNull(context.DeclaredCapability);
    }

    [Fact]
    public async Task Application_approval_cannot_widen_original_read_only_governance() {
        var agent = Agent();
        var governance = new AgentExecutionGovernanceSnapshot(AgentExecutionAuthorityId.Create(), agent.Id,
            Guid.NewGuid(), new(1), WorkspaceScopeDescriptor.Sandbox, true, false, "test-policy", "test-fingerprint");
        var context = MafToolInvocationPolicyContextFactory.Create(agent, ToolContractCatalog.WorkspaceWriteFile,
            [new("path", "report.txt"), new("content", "saved")], ToolInvocationClassification.Mutation,
            AgentToolPolicyCatalog.BuiltIn, true, new(false, false, false, true), governance,
            (_, _, _, _, _) => ScriptContentInspection.Empty, null, []);

        var decision = await new DefaultAgentToolInvocationPolicy().EvaluateAsync(context, default);
        Assert.Same(governance, context.ExecutionGovernance);
        Assert.True(context.HasEffectiveApprovalPath);
        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Throws<AgentToolPolicyBlockedException>(() => AgentToolPolicyBlockGuard.ThrowIfBlocked(
            context.ToolName, decision, context.HasEffectiveApprovalPath));
    }

    [Fact]
    public async Task History_wrapper_forwards_saved_effect_without_model_or_provider_history_execution() {
        var port = new SavedPort();
        var wrapper = new HistoryAgentRuntime(port, port);
        var binding = new AgentToolApprovalBinding(new(Guid.NewGuid()), new(Guid.NewGuid()), 2,
            AgentToolProtocolEnvelope.ComputeDigest("original"));
        var request = new AgentSavedEffectDispatchRequest(new(Guid.NewGuid(), Guid.NewGuid(), AgentExecutionAuthorityId.Create()), binding);
        using var cancellation = new CancellationTokenSource();

        Assert.Equal(AgentSavedEffectDispatchSupport.OriginalPreparedOwnerEffect, wrapper.SavedEffectDispatchSupport);
        var result = await wrapper.DispatchSavedEffectAsync(request, cancellation.Token);
        Assert.Same(request, port.Request);
        Assert.Equal(cancellation.Token, port.Token);
        Assert.Equal(binding, result.Binding);
        Assert.Equal(11, result.RunRevision);
        Assert.Equal(7, result.JournalRevision);
    }

    private static AgentDefinition Agent() => new(Guid.NewGuid(), "Policy mapper", "Tester", string.Empty, string.Empty,
        AgentLifecycleStatus.Active, Guid.NewGuid(), string.Empty, AgentWorkloadKind.Programming,
        AgentChatHistoryMode.FrameworkManaged, 0, false, false, "{}", false, string.Empty,
        AgentPermissionsPolicy.Default with { CanUseTools = true }, [], [], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    private sealed class SavedPort : IAgentExecutionRuntime, IAgentContinuationRuntime {
        internal AgentSavedEffectDispatchRequest? Request { get; private set; }
        internal CancellationToken Token { get; private set; }
        public AgentSavedEffectDispatchSupport SavedEffectDispatchSupport => AgentSavedEffectDispatchSupport.OriginalPreparedOwnerEffect;
        public Task<AgentRuntimeResponse> ExecuteAsync(AgentRuntimeExecutionRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Saved effects cannot enter model execution.");
        public Task<AgentRuntimeResponse> ContinueAsync(AgentRuntimeContinuationRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Saved effects cannot restore provider continuation.");
        public Task<AgentSavedEffectDispatchResult> DispatchSavedEffectAsync(AgentSavedEffectDispatchRequest request,
            CancellationToken cancellationToken = default) {
            Request = request;
            Token = cancellationToken;
            return Task.FromResult(new AgentSavedEffectDispatchResult(request.Binding, AgentToolProposalState.Completed,
                AgentToolEffectState.Committed, 11, 7));
        }
    }
}
