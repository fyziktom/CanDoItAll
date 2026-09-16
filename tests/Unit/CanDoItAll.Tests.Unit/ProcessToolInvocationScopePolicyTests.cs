using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Workbench.AgentContext;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ProcessToolInvocationScopePolicyTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Saved_operation_ceiling_is_enforced_after_owner_contribution(bool hasValidationGrant) {
        var audit = CreateAudit(hasValidationGrant ? [ProcessOperationContractNames.RunValidation] : []);
        var pipeline = new AgentToolInvocationPolicyPipeline(new DefaultAgentToolInvocationPolicy(),
            [new ProcessToolInvocationPolicyContextContributor()]);

        var evaluation = await pipeline.ComposeAndEvaluateAsync(CreateContext(ToolContractCatalog.WorkspaceDotNetTest),
            audit, CancellationToken.None);

        Assert.Equal(hasValidationGrant ? ToolInvocationDecisionKind.Allow : ToolInvocationDecisionKind.Deny, evaluation.Decision.Kind);
        Assert.Equal(audit.ProcessStepAllowedOperations, evaluation.EffectiveContext.ProcessStepAllowedOperations);
        Assert.Equal(audit.ProcessStepTargetScope, evaluation.EffectiveContext.ProcessStepTargetScope);
        Assert.Equal(audit.ProcessRunId, evaluation.EffectiveContext.ProcessRunId);
        Assert.Equal(audit.ProcessStepId, evaluation.EffectiveContext.ProcessStepId);
        if (!hasValidationGrant) {
            Assert.Equal("This governed step is missing an operation contract and cannot use tool 'workspace_dotnet_test'. Required operation: RunValidation.",
                evaluation.Decision.Reason);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Owner_allows_only_restrictions_and_preserves_later_approval(bool autoApprove, bool hasApprovalWrapper) {
        var context = CreateContext(ToolContractCatalog.WorkspaceWriteFile, "artifacts/process-runs/run-a/evidence.md") with {
            AutoApprovalAllowed = autoApprove,
            ApprovalWrapperAvailable = hasApprovalWrapper,
            ApprovalWrapperEffectiveForProvider = hasApprovalWrapper
        };
        var pipeline = new AgentToolInvocationPolicyPipeline(new DefaultAgentToolInvocationPolicy(),
            [new ProcessToolInvocationPolicyContextContributor()]);
        var evaluation = await pipeline.ComposeAndEvaluateAsync(context,
            CreateAudit([ProcessOperationContractNames.WriteManagedProcessArtifacts]), CancellationToken.None);

        Assert.Equal(autoApprove ? ToolInvocationDecisionKind.Allow : ToolInvocationDecisionKind.RequireApproval,
            evaluation.Decision.Kind);
        if (!autoApprove && !hasApprovalWrapper) {
            Assert.Throws<AgentToolPolicyBlockedException>(() => AgentToolPolicyBlockGuard.ThrowIfBlocked(context.ToolName,
                evaluation.Decision, evaluation.EffectiveContext.HasEffectiveApprovalPath));
        } else {
            AgentToolPolicyBlockGuard.ThrowIfBlocked(context.ToolName, evaluation.Decision,
                evaluation.EffectiveContext.HasEffectiveApprovalPath);
        }
    }

    [Theory]
    [InlineData(ToolInvocationDecisionKind.Allow)]
    [InlineData(ToolInvocationDecisionKind.RequireApproval)]
    [InlineData(ToolInvocationDecisionKind.SanitizeResult)]
    public async Task Owner_cannot_short_circuit_remaining_authorization(ToolInvocationDecisionKind result) {
        var policy = new ProbeScopePolicy((_, signature) => new(result, "owner attempted to allow", signature));
        var context = CreateContext(ToolContractCatalog.WorkspaceWriteFile) with { ScopePolicy = policy };

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new DefaultAgentToolInvocationPolicy().EvaluateAsync(context, CancellationToken.None).AsTask());

        Assert.Contains("may only deny or defer", failure.Message, StringComparison.Ordinal);
        Assert.Equal(1, policy.Evaluations);
    }

    [Theory]
    [InlineData(ToolInvocationDecisionKind.Deny)]
    [InlineData(ToolInvocationDecisionKind.SkipExecution)]
    public async Task Owner_restrictions_are_not_overwritten_by_auto_approval(ToolInvocationDecisionKind kind) {
        var policy = new ProbeScopePolicy((_, signature) => new(kind, "owner restriction", signature));
        var context = CreateContext(ToolContractCatalog.WorkspaceWriteFile) with { ScopePolicy = policy, AutoApprovalAllowed = true };

        var result = await new DefaultAgentToolInvocationPolicy().EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(kind, result.Kind);
        Assert.Equal("owner restriction", result.Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Earlier_known_tool_and_owner_path_shape_checks_run_before_operation_contract(bool invalidPathShape) {
        var owner = new ProbeScopePolicy((_, _) => throw new InvalidOperationException("Owner must not run."));
        var context = CreateContext(ToolContractCatalog.WorkspaceReadFile) with {
            IsKnownTool = invalidPathShape,
            ScopePolicy = owner,
            PathArguments = invalidPathShape ? new([], ["path"]) : ToolInvocationPathArgumentSet.Empty
        };

        var result = await new DefaultAgentToolInvocationPolicy().EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, result.Kind);
        Assert.Contains(invalidPathShape ? "unsupported path argument shape" : "not part of the composed capability set",
            result.Reason, StringComparison.Ordinal);
        Assert.Equal(0, owner.Evaluations);
    }

    [Fact]
    public async Task Governed_default_policy_cannot_run_without_its_owner() {
        var context = CreateContext(ToolContractCatalog.WorkspaceDotNetTest);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new DefaultAgentToolInvocationPolicy().EvaluateAsync(context, CancellationToken.None).AsTask());

        Assert.Contains("no owner invocation-scope policy", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Copying_saved_fields_without_owner_policy_does_not_satisfy_pipeline() {
        var pipeline = new AgentToolInvocationPolicyPipeline(new DefaultAgentToolInvocationPolicy(), [new RemovingPolicyContributor()]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.ComposeAndEvaluateAsync(
            CreateContext(ToolContractCatalog.WorkspaceDotNetTest), CreateAudit([ProcessOperationContractNames.RunValidation]),
            CancellationToken.None).AsTask());
    }

    [Fact]
    public void Process_contributor_cannot_replace_another_scope_owner() {
        var context = CreateContext(ToolContractCatalog.WorkspaceDotNetTest) with {
            ScopePolicy = new ProbeScopePolicy((_, _) => null)
        };

        Assert.Throws<InvalidOperationException>(() => new ProcessToolInvocationPolicyContextContributor()
            .Contribute(context, CreateAudit([ProcessOperationContractNames.RunValidation])));
    }

    [Fact]
    public async Task Primary_output_identity_and_exact_recovery_message_are_owner_owned() {
        const string path = "artifacts/process-runs/run-a/steps/step-a.md";
        var context = CreateContext(ToolContractCatalog.WorkspaceReadFile, path);
        var pipeline = new AgentToolInvocationPolicyPipeline(new DefaultAgentToolInvocationPolicy(),
            [new ProcessToolInvocationPolicyContextContributor()]);
        var evaluation = await pipeline.ComposeAndEvaluateAsync(context, CreateAudit([ProcessOperationContractNames.ReadProcessContext]),
            CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, evaluation.Decision.Kind);
        Assert.Contains($"its own primary managed output '{path}' before creating it", evaluation.Decision.Reason, StringComparison.Ordinal);
        Assert.True(AgentToolPolicyBlockGuard.TryCreateRecoverableDeniedResult(context.ToolName, evaluation.Decision,
            evaluation.EffectiveContext, out var recovery));
        Assert.Equal($"PolicyDenied: Tool 'workspace_read_file' was denied for this governed process step. {evaluation.Decision.Reason} This is not a missing tool permission and not a blocker. Do not retry the read, stat, list, or search. Do not write a status-only InProgress or Blocked placeholder and stop. Continue the step's required product, validation, or external work from launch variables, upstream artifacts, project-structure context, or product readback. When recording the step outcome, create or overwrite the named primary managed artifact with workspace_write_file or workspace_append_file, then return submit_process_step_outcome with evidenceRefs containing that managed ref. Submit Blocked only if the artifact write is denied, or if a required tool is denied or fails on a concrete environment boundary.", recovery);
        Assert.Equal("artifacts/process-runs/run-a", ProcessToolInvocationScopePolicy.Instance.GetWorkspaceFacts(evaluation.EffectiveContext).ManagedArtifactRoot);
    }

    [Theory]
    [InlineData(AgentChatTrustedSourceKinds.ProjectStructure, "Continue from the selected project-structure subtree or managed workspace")]
    [InlineData("chat-session", "Continue within the managed workspace")]
    public void Structure_owner_contributes_exact_interactive_continuation(string sourceKind, string continuation) {
        var context = CreateContext(ToolContractCatalog.WorkspaceReadFile) with {
            SourceKind = sourceKind,
            ProcessRunId = string.Empty,
            ProcessStepId = string.Empty
        };
        var contributed = new ProjectStructureRuntimeGuidanceContributor().Contribute(context, null);
        var decision = ToolInvocationPolicyDecision.Deny("signature", "external-target path is outside the current run boundary.");

        Assert.True(AgentToolPolicyBlockGuard.TryCreateRecoverableDeniedResult(context.ToolName, decision, contributed, out var result));
        Assert.Equal($"PolicyDenied: Tool 'workspace_read_file' was denied by the external workspace boundary. {decision.Reason} This is a recoverable path error, not a failed agent run. Do not broaden or guess another external-target root. {continuation}, or use an exact external root explicitly grounded for this run.", result);
    }

    [Fact]
    public void Runtime_policy_and_continuation_are_not_serialized_as_authority() {
        var context = new ProcessToolInvocationPolicyContextContributor().Contribute(
            CreateContext(ToolContractCatalog.WorkspaceReadFile), CreateAudit([ProcessOperationContractNames.ReadProcessContext])) with {
            ExternalWorkspaceReadRecoveryContinuation = "private runtime contribution"
        };
        var json = JsonSerializer.Serialize(context);
        using var document = JsonDocument.Parse(json);

        Assert.False(document.RootElement.TryGetProperty(nameof(context.ScopePolicy), out _));
        Assert.False(document.RootElement.TryGetProperty(nameof(context.ExternalWorkspaceReadRecoveryContinuation), out _));
        Assert.Equal("run-a", document.RootElement.GetProperty(nameof(context.ProcessRunId)).GetString());
        Assert.Equal(ProcessOperationContractNames.ReadProcessContext,
            document.RootElement.GetProperty(nameof(context.ProcessStepAllowedOperations))[0].GetString());
    }

    private static ToolInvocationPolicyContext CreateContext(string name, string path = "") {
        var arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (path.Length > 0) {
            arguments["path"] = path;
        }
        return new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Process tool operator", name, arguments,
            AgentToolPolicyCatalog.BuiltIn.Classify(name), IsKnownTool: true, AutoApprovalAllowed: true,
            ApprovalWrapperAvailable: false, "execution-a", "process-step", "run-a", "step-instance-a") {
            SourceId = "step.a",
            PathArguments = ToolInvocationPathArgumentResolver.Resolve(name,
                arguments.Select(item => new KeyValuePair<string, object?>(item.Key, item.Value)))
        };
    }

    private static WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState CreateAudit(IReadOnlyList<string> operations)
        => new(Guid.Parse("22222222-2222-2222-2222-222222222222"), Guid.Parse("11111111-1111-1111-1111-111111111111"),
            null, string.Empty, "process-step", "step.a", "run-a", "step-instance-a", string.Empty, string.Empty,
            "OpenAI", "unit-test-model", [], [], [], [], null, null, false, false, false, [], [], operations,
            ProcessOperationContractNames.ManagedProcessArtifactsOnly, WorkspaceScopeDescriptor.Sandbox, null, null);

    private sealed class RemovingPolicyContributor : IToolInvocationPolicyContextContributor {
        public ToolInvocationPolicyContext Contribute(ToolInvocationPolicyContext context,
            WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState? auditScope)
            => new ProcessToolInvocationPolicyContextContributor().Contribute(context, auditScope) with { ScopePolicy = null };
    }

    private sealed class ProbeScopePolicy(Func<ToolInvocationPolicyContext, string, ToolInvocationPolicyDecision?> evaluate)
        : IToolInvocationScopePolicy {
        public int Evaluations { get; private set; }

        public ToolInvocationPolicyDecision? EvaluateRestrictions(ToolInvocationPolicyContext context, string signature) {
            Evaluations++;
            return evaluate(context, signature);
        }

        public ToolInvocationPolicyDecision? EvaluateRestrictions(ToolInvocationPolicyContext context, string signature,
            ToolInvocationScopePolicyPhase phase)
            => phase == ToolInvocationScopePolicyPhase.Contract ? EvaluateRestrictions(context, signature)
                : ProcessToolInvocationScopePolicy.Instance.EvaluateRestrictions(context, signature, phase);

        public ToolInvocationWorkspaceScopeFacts GetWorkspaceFacts(ToolInvocationPolicyContext context)
            => ProcessToolInvocationScopePolicy.Instance.GetWorkspaceFacts(context);

        public bool TryCreateRecoverableDeniedResult(string toolName, ToolInvocationPolicyDecision decision,
            ToolInvocationPolicyContext context, out string result) {
            result = string.Empty;
            return false;
        }
    }
}
