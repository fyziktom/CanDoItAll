using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Processes;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

/// <summary>
/// The injected tool-governance pipeline: domain contributors enrich the
/// provider-neutral context, governed process runs fail closed without their
/// contributor, and the continuation lifecycle stays bounded (abandoned
/// waiting runs are reconciled; the rehydration cache cannot grow without
/// limit).
/// </summary>
public sealed class ToolGovernancePipelineAndApprovalLifecycleTests
{
    [Fact]
    public async Task Process_contributor_maps_typed_restrictions_onto_the_neutral_context()
    {
        ToolInvocationPolicyContext? observedContext = null;
        var pipeline = new AgentToolInvocationPolicyPipeline(
            new RecordingPolicy(context =>
            {
                observedContext = context;
                return ToolInvocationPolicyDecision.Allow("signature");
            }),
            [new ProcessToolInvocationPolicyContextContributor()]);
        var auditScope = CreateProcessAuditScope();

        await pipeline.ComposeAndEvaluateAsync(
            CreateNeutralContext(),
            auditScope,
            CancellationToken.None);

        Assert.NotNull(observedContext);
        Assert.Equal(auditScope.ProcessRunId, observedContext!.ProcessRunId);
        Assert.Equal(auditScope.ProcessStepId, observedContext.ProcessStepId);
        Assert.False(observedContext.ProcessAllowsProductMutation);
        Assert.Equal(auditScope.ProcessStepAllowedOperations, observedContext.ProcessStepAllowedOperations);
        Assert.Equal(auditScope.ProcessStepTargetScope, observedContext.ProcessStepTargetScope);
        Assert.Contains("workspace_write_file", observedContext.ProductMutationToolNames);
    }

    [Fact]
    public async Task Governed_process_run_fails_closed_without_a_process_contributor()
    {
        var pipeline = new AgentToolInvocationPolicyPipeline(
            new RecordingPolicy(_ => ToolInvocationPolicyDecision.Allow("signature")),
            contributors: []);

        await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.ComposeAndEvaluateAsync(
            CreateNeutralContext(),
            CreateProcessAuditScope(),
            CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Interactive_run_without_process_identity_needs_no_contributor()
    {
        var pipeline = new AgentToolInvocationPolicyPipeline(
            new RecordingPolicy(_ => ToolInvocationPolicyDecision.Allow("signature")),
            contributors: []);

        var result = await pipeline.ComposeAndEvaluateAsync(
            CreateNeutralContext(),
            auditScope: null,
            CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, result.Decision.Kind);
    }

    [Fact]
    public async Task Compose_and_evaluate_returns_the_exact_effective_context_with_the_decision()
    {
        ToolInvocationPolicyContext? observedContext = null;
        var pipeline = new AgentToolInvocationPolicyPipeline(
            new RecordingPolicy(context =>
            {
                observedContext = context;
                return ToolInvocationPolicyDecision.Allow("signature");
            }),
            [new ProcessToolInvocationPolicyContextContributor()]);

        var result = await pipeline.ComposeAndEvaluateAsync(
            CreateNeutralContext(),
            CreateProcessAuditScope(),
            CancellationToken.None);

        Assert.Same(observedContext, result.EffectiveContext);
        Assert.Equal(ToolInvocationDecisionKind.Allow, result.Decision.Kind);
    }

    [Fact]
    public async Task Governed_process_run_rejects_an_unrelated_cloning_contributor()
    {
        var pipeline = new AgentToolInvocationPolicyPipeline(
            new RecordingPolicy(_ => ToolInvocationPolicyDecision.Allow("signature")),
            [new CloningContributor()]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.ComposeAndEvaluateAsync(
            CreateNeutralContext(),
            CreateProcessAuditScope(),
            CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Contributor_enriched_process_denial_remains_recoverable_downstream()
    {
        var pipeline = new AgentToolInvocationPolicyPipeline(
            new RecordingPolicy(_ => ToolInvocationPolicyDecision.Deny(
                "denied-signature",
                "The requested path is outside the governed workspace boundary.")),
            [new ProcessToolInvocationPolicyContextContributor()]);

        var evaluation = await pipeline.ComposeAndEvaluateAsync(
            CreateNeutralContext(),
            CreateProcessAuditScope(),
            CancellationToken.None);

        var recovered = AgentToolPolicyBlockGuard.TryCreateRecoverableDeniedResult(
            evaluation.EffectiveContext.ToolName,
            evaluation.Decision,
            evaluation.EffectiveContext,
            out var result);

        Assert.True(recovered);
        Assert.Contains("PolicyDenied", result, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(evaluation.EffectiveContext.ProcessRunId));
        Assert.False(string.IsNullOrWhiteSpace(evaluation.EffectiveContext.ProcessStepId));
    }

    [Fact]
    public async Task Maf_composition_uses_registered_process_contributor_for_recoverable_denial()
    {
        var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection();
        services.AddSingleton<IAgentToolInvocationPolicy>(new RecordingPolicy(
            _ => ToolInvocationPolicyDecision.Deny(
                "denied-signature",
                "The requested path is outside the governed workspace boundary.")));
        services.AddSingleton<
            IToolInvocationPolicyContextContributor,
            ProcessToolInvocationPolicyContextContributor>();
        using var provider = services.BuildServiceProvider();
        var pipeline = MafAgentRuntimeDependencies
            .FromServices(provider)
            .ToolInvocationPolicyPipeline;

        var evaluation = await pipeline.ComposeAndEvaluateAsync(
            CreateNeutralContext(),
            CreateProcessAuditScope(),
            CancellationToken.None);
        var recovered = AgentToolPolicyBlockGuard.TryCreateRecoverableDeniedResult(
            evaluation.EffectiveContext.ToolName,
            evaluation.Decision,
            evaluation.EffectiveContext,
            out var result);

        Assert.True(recovered);
        Assert.Contains("PolicyDenied", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Maf_policy_consumer_remains_process_semantic_free()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src", "MAF", "Common", "CanDoItAll.AgentFramework.Maf", "Runtime",
            "MafRuntimeAgentFactory.cs"));

        Assert.DoesNotContain("auditScope?.ProcessRunId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("auditScope?.ProcessStepId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("auditScope?.ProcessAllowsProductMutation", source, StringComparison.Ordinal);
        Assert.Contains("policyEvaluation.EffectiveContext", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Abandoned_waiting_run_lease_is_reconciled_after_the_cutoff()
    {
        var evicted = new List<AgentTurnContextLeaseEvictionDiagnostic>();
        var registry = new AgentTurnContextLeaseRegistry(
            timeToLive: TimeSpan.FromTicks(1),
            onEvicted: evicted.Add,
            abandonedWaitingRunCutoff: TimeSpan.FromTicks(2));
        var context = new AgentRuntimeTransientContext("Selected CRM partner: 42");
        var digest = AgentChatContextDigest.Compute(context);
        var waitingRun = CreateRun(ExecutionState.WaitingOnTool, digest);
        registry.Register(waitingRun, context);

        // Both the ordinary TTL and the abandoned cutoff have elapsed by the
        // next observation; the abandoned waiting lease must be reconciled and
        // continuation must fail closed instead of using stale context.
        Thread.Sleep(TimeSpan.FromMilliseconds(5));
        var probeRun = CreateRun(ExecutionState.WaitingOnTool, digest);
        registry.Register(probeRun, context);

        Assert.Contains(evicted, diagnostic => diagnostic.ExecutionRunId == waitingRun.Id);
        Assert.Throws<AgentRunTransientContextUnavailableException>(() => registry.Resolve(waitingRun));
    }

    [Fact]
    public void Waiting_run_lease_survives_ordinary_ttl_before_the_cutoff()
    {
        var registry = new AgentTurnContextLeaseRegistry(
            timeToLive: TimeSpan.FromTicks(1),
            abandonedWaitingRunCutoff: TimeSpan.FromDays(7));
        var context = new AgentRuntimeTransientContext("Selected CRM partner: 42");
        var digest = AgentChatContextDigest.Compute(context);
        var waitingRun = CreateRun(ExecutionState.WaitingOnTool, digest);
        registry.Register(waitingRun, context);

        Thread.Sleep(TimeSpan.FromMilliseconds(5));

        Assert.Same(context, registry.Resolve(waitingRun));
    }

    private static ExecutionRunRecord CreateRun(ExecutionState state, string transientContextDigest)
        => new(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: Guid.NewGuid(),
            Title: "Lifecycle test run",
            SourceKind: "chat-session",
            SourceId: Guid.NewGuid().ToString("N"),
            CorrelationId: string.Empty,
            CausationId: string.Empty,
            RequestedBy: "test",
            RequestedByKind: "interactive",
            MetadataJson: ExecutionInvocationMetadata.ApplyTransientContextRequirement(
                "{}",
                transientContextDigest),
            InputSummary: string.Empty,
            ResultSummary: string.Empty,
            ProviderName: "OpenAI",
            Model: "unit-test-model",
            State: state,
            Outcome: null,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            StartedAtUtc: null,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);

    private static WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState CreateProcessAuditScope()
        => new(
            ExecutionRunId: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            CorrelationId: string.Empty,
            SourceKind: "process-step",
            SourceId: "code-change",
            ProcessRunId: Guid.NewGuid().ToString("D"),
            ProcessStepId: Guid.NewGuid().ToString("D"),
            SchedulerRunId: string.Empty,
            MessageId: string.Empty,
            ProviderName: "OpenAI",
            Model: "unit-test-model",
            ExternalTargetRootBindings: [],
            AllowedExternalTargetAliases: [],
            ReadOnlyExternalTargetAliases: [],
            AllowedManagedArtifactReadRefs: [],
            ProcessCooperationMode: null,
            WorkspaceToolProfileOverride: null,
            ProcessBrowserToolsAllowed: false,
            ProcessAllowsProductMutation: false,
            ProcessRequiresProductMutationBeforeManagedOutput: true,
            ProcessProductMutationToolNames: ["workspace_write_file"],
            ProcessProductMutationRequiredBranchOutcomeKeys: ["implemented"],
            ProcessStepAllowedOperations: ["RunValidation"],
            ProcessStepTargetScope: "ExternalProductTargetMutable",
            ContextWorkspaceScope: WorkspaceScopeDescriptor.Sandbox,
            ProjectStructureLaunchAgent: null,
            ProjectStructureProcessNodeContext: null);

    private static ToolInvocationPolicyContext CreateNeutralContext()
    {
        var arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["path"] = "artifacts/report.md"
        };
        return new ToolInvocationPolicyContext(
            AgentId: Guid.NewGuid(),
            AgentName: "Pipeline test agent",
            ToolName: ToolContractCatalog.WorkspaceReadFile,
            RedactedArguments: arguments,
            Classification: ToolInvocationClassification.Read,
            IsKnownTool: true,
            AutoApprovalAllowed: false,
            ApprovalWrapperAvailable: false,
            ExecutionRunId: Guid.NewGuid().ToString("D"),
            SourceKind: "process-step",
            ProcessRunId: string.Empty,
            ProcessStepId: string.Empty)
        {
            PathArguments = ToolInvocationPathArgumentResolver.Resolve(
                ToolContractCatalog.WorkspaceReadFile,
                arguments.Select(argument =>
                    new KeyValuePair<string, object?>(argument.Key, argument.Value)))
        };
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

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    private sealed class RecordingPolicy(
        Func<ToolInvocationPolicyContext, ToolInvocationPolicyDecision> evaluate) : IAgentToolInvocationPolicy
    {
        public ValueTask<ToolInvocationPolicyDecision> EvaluateAsync(
            ToolInvocationPolicyContext context,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(evaluate(context));
    }

    private sealed class CloningContributor : IToolInvocationPolicyContextContributor
    {
        public ToolInvocationPolicyContext Contribute(
            ToolInvocationPolicyContext context,
            WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState? auditScope)
            => context with { };
    }
}
