using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Tests.Support;
using CanDoItAll.Tools.Documents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class MafInheritedSavedEffectDispatcherTests {
    [Fact]
    public async Task Inherited_saved_effect_preserves_original_scope_and_source_through_policy_and_real_publication() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateForPublicationAsync();
        var waiting = await fixture.WaitForApprovalAsync();
        var provider = new Provider(fixture);
        var policy = new Policy();
        await using var lease = await ResumeAsync(fixture, waiting);
        using var bound = lease.Bind();
        var dispatcher = Dispatcher(fixture, provider, policy);
        var result = await dispatcher.DispatchAsync(Request(waiting), default);

        Assert.Equal(AgentToolEffectState.Committed, result.EffectState);
        Assert.Equal(AgentToolProposalState.Completed, result.State);
        Assert.Equal(1, provider.Restored);
        Assert.Equal(1, provider.Entered);
        Assert.Equal(1, provider.Receipts);
        var context = Assert.Single(policy.Contexts);
        Assert.Null(context.ExecutionGovernance);
        Assert.Equal(waiting.Id.ToString("D"), context.ExecutionRunId);
        Assert.Equal(waiting.SourceKind, context.SourceKind);
        Assert.Equal(waiting.SourceId, context.SourceId);
        Assert.Equal(waiting.ProcessRunId, context.ProcessRunId);
        Assert.Equal(waiting.ProcessStepId, context.ProcessStepId);
        Assert.Same(ProcessToolInvocationScopePolicy.Instance, context.ScopePolicy);
        Assert.Equal(fixture.Files.StorageScope.Key, context.ContextWorkspaceScopeKey);
        Assert.True(context.ApplicationApprovalAvailable);
        Assert.False(context.AutoApprovalAllowed);
        var retained = await fixture.ReadAsync();
        Assert.Null(retained.ChatSessionId);
        Assert.Equal(waiting.ToolAdmission!.PlanningDecision!.InheritedOrigin, retained.ToolAdmission!.PlanningDecision!.InheritedOrigin);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => dispatcher.DispatchAsync(Request(waiting), default));
        Assert.Equal(1, provider.Entered);
    }

    [Theory]
    [InlineData(AgentRuntimeToolProviderPurpose.InteractiveChat)]
    [InlineData(AgentRuntimeToolProviderPurpose.AutoApprovedNonInteractive)]
    [InlineData(AgentRuntimeToolProviderPurpose.A2AEndpoint)]
    public async Task Provider_must_support_the_original_governed_purpose_before_restoration(AgentRuntimeToolProviderPurpose purpose) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateForPublicationAsync();
        var waiting = await fixture.WaitForApprovalAsync();
        var provider = new Provider(fixture, purpose);
        await using var lease = await ResumeAsync(fixture, waiting);
        using var bound = lease.Bind();
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => Dispatcher(fixture, provider).DispatchAsync(Request(waiting), default));
        Assert.Equal(0, provider.Restored);
        Assert.Equal(0, provider.Receipts);
        Assert.Null(Proposal(await fixture.ReadAsync()).DispatchClaimId);
    }

    [Theory]
    [InlineData(Denial.Agent)]
    [InlineData(Denial.Policy)]
    [InlineData(Denial.Module)]
    public async Task Current_permissions_and_normal_policy_still_deny_inherited_effects(Denial denial) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateForPublicationAsync();
        var waiting = await fixture.WaitForApprovalAsync();
        if (denial == Denial.Agent) {
            var changed = await fixture.Files.Store.UpdateCatalogAsync(catalog => catalog with {
                Agents = catalog.Agents.Select(agent => agent.Id == waiting.AgentId
                    ? agent with { Permissions = agent.Permissions with { CanUseTools = false } } : agent).ToArray()
            });
            Assert.False(changed.Agents.Single(agent => agent.Id == waiting.AgentId).Permissions.CanUseTools);
            await using var current = await fixture.Files.NewStore().AcquireAgentReadLeaseAsync(waiting.AgentId);
            Assert.False(current.Agent!.Permissions.CanUseTools);
        }
        var provider = new Provider(fixture) { Denied = denial == Denial.Module };
        await using var lease = await ResumeAsync(fixture, waiting);
        using var bound = lease.Bind();
        var result = await Dispatcher(fixture, provider, new(denial == Denial.Policy)).DispatchAsync(Request(waiting), default);
        Assert.Equal(AgentToolEffectState.NotCommitted, result.EffectState);
        Assert.Equal(0, provider.Entered);
        Assert.NotNull(Proposal(await fixture.ReadAsync()).DispatchClaimId);
    }

    [Fact]
    public async Task Missing_process_policy_contributor_cannot_enter_the_effect() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateForPublicationAsync();
        var waiting = await fixture.WaitForApprovalAsync();
        var provider = new Provider(fixture);
        var policy = new Policy();
        await using var lease = await ResumeAsync(fixture, waiting);
        using var bound = lease.Bind();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Dispatcher(fixture, provider, policy, includeProcessPolicy: false)
            .DispatchAsync(Request(waiting), default));
        Assert.Empty(policy.Contexts);
        Assert.Equal(0, provider.Entered);
    }

    [Fact]
    public async Task Source_revocation_during_restore_cannot_probe_receipt_or_claim() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateForPublicationAsync();
        var waiting = await fixture.WaitForApprovalAsync();
        var provider = new Provider(fixture) { AfterRestore = () => fixture.Policy.DispatchAllowed = false };
        await using var lease = await ResumeAsync(fixture, waiting);
        using var bound = lease.Bind();
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => Dispatcher(fixture, provider).DispatchAsync(Request(waiting), default));
        Assert.Equal(1, provider.Restored);
        Assert.Equal(0, provider.Receipts);
        Assert.Equal(0, provider.Entered);
        Assert.Null(Proposal(await fixture.ReadAsync()).DispatchClaimId);
    }

    [Fact]
    public async Task Actual_committed_effect_remains_recorded_when_source_is_revoked_before_acknowledgement() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateForPublicationAsync();
        var waiting = await fixture.WaitForApprovalAsync();
        var provider = new Provider(fixture) { AfterCommit = () => fixture.Policy.DispatchAllowed = false };
        await using var lease = await ResumeAsync(fixture, waiting);
        using var bound = lease.Bind();
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => Dispatcher(fixture, provider).DispatchAsync(Request(waiting), default));
        var retained = Proposal(await fixture.ReadAsync());
        Assert.Equal(AgentToolProposalState.Completed, retained.State);
        Assert.Equal(AgentToolEffectState.Committed, retained.EffectState);
        Assert.NotNull(retained.Result);
        Assert.Equal(1, provider.Entered);
    }

    [Theory]
    [InlineData(ChangedContext.Source)]
    [InlineData(ChangedContext.Profile)]
    [InlineData(ChangedContext.Agent)]
    public void Inherited_context_cannot_substitute_original_identity(ChangedContext changed) {
        var journal = AgentInheritedPlanningTestData.Create();
        var original = journal.PlanningDecision!.InheritedOrigin!;
        var agent = new AgentDefinition(journal.Session.AgentId, "Inherited context", "Tester", string.Empty, string.Empty,
            AgentLifecycleStatus.Active, Guid.NewGuid(), string.Empty, AgentWorkloadKind.Programming,
            AgentChatHistoryMode.FrameworkManaged, 0, false, false, "{}", false, string.Empty,
            AgentPermissionsPolicy.Default with { CanUseTools = true }, [], [], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        var replacement = changed switch {
            ChangedContext.Source => original with { Source = new(original.Source.SourceKind, Guid.NewGuid().ToString("D"),
                original.Source.OwnerFingerprint, original.Source.ExecutionFingerprint) },
            ChangedContext.Profile => original with { Profile = new(Guid.NewGuid(), "changed-profile", new(1)) },
            ChangedContext.Agent => original with { AgentId = Guid.NewGuid() },
            _ => throw new ArgumentOutOfRangeException(nameof(changed))
        };
        Assert.Throws<ArgumentException>(() => AgentRuntimeSavedEffectContext.ForInherited(journal.Session,
            agent, replacement, WorkspaceScopeDescriptor.Organization(journal.Session.Profile.ProfileId.ToString("N"))));
    }

    [Fact]
    public async Task Request_factory_preserves_session_kind_and_cannot_grant_a_dispatch_lease() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateForPublicationAsync();
        var waiting = await fixture.WaitForApprovalAsync();
        var binding = Binding(waiting);
        Assert.Throws<ArgumentException>(() => new AgentSavedEffectDispatchRequest(waiting.ToolAdmission!.Session.Reference, binding));
        Assert.Throws<ArgumentException>(() => AgentSavedEffectDispatchRequest.ForInherited(fixture.Files.Session, binding));
        var request = Request(waiting);
        Assert.Equal(waiting.ToolAdmission!.Session.Reference, request.Session);
        Assert.Equal(binding, request.Binding);
        var provider = new Provider(fixture);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => Dispatcher(fixture, provider).DispatchAsync(request, default));
        Assert.Equal(0, provider.Restored);
        Assert.Equal(0, provider.Entered);
        Assert.Null(Proposal(await fixture.ReadAsync()).DispatchClaimId);
    }

    public enum Denial { Agent, Policy, Module }
    public enum ChangedContext { Source, Profile, Agent }

    private static AgentToolProposalRecord Proposal(ExecutionRunRecord run)
        => AgentToolJournalTransitions.RequireProposal(run.ToolAdmission!, Binding(run));
    private static AgentToolApprovalBinding Binding(ExecutionRunRecord run)
        => run.ToolAdmission!.PlanningDecision!.Current.Steps[^1].Publications.Single().Proposal;
    private static AgentSavedEffectDispatchRequest Request(ExecutionRunRecord run)
        => AgentSavedEffectDispatchRequest.ForInherited(run.ToolAdmission!.Session.Reference, Binding(run));
    private static Task<AgentToolRunLease> ResumeAsync(AgentInheritedPlanningFixture fixture, ExecutionRunRecord run)
        => fixture.Owner.ResumeInheritedSavedEffectAsync(new(run.Id, null, run.Revision, run.ToolAdmission!.Revision, Binding(run),
            run.PendingApprovals.Select(item => new PendingToolApprovalDecision(item.ApprovalId, true) { ToolAdmission = item.ToolAdmission }).ToArray()),
            AgentPlanningDecisionExpectation.From(run), new(run.ToolAdmission.PlanningDecision!.InheritedOrigin!, (_, token) => {
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }), AgentInheritedPlanningFixture.AllowRead, default);

    private static MafSavedEffectDispatcher Dispatcher(AgentInheritedPlanningFixture fixture, Provider provider, Policy? policy = null,
        bool includeProcessPolicy = true) {
        var paths = new PhysicalFileSystemPathPolicyFactory();
        var services = new WorkspaceRuntimeServicesFactory([], new ManagedCodeMarkItDownDocumentMarkdownConverter(),
            paths, new ExternalTargetPathRegistryFactory());
        var policies = new AgentToolPolicyCatalog([new(AgentPlanningLiveOwnerFixture.Publication().Payload.ToolName,
            ToolInvocationClassification.Mutation, true, true, ToolCapabilitySideEffectKind.InternalStateMutation,
            ToolCapabilityOperationRequirementKind.None, [], [], true, false, false, false,
            ToolCapabilityBrowserProofRole.None, ToolCapabilityIdempotencyDescriptor.StateChanging)]);
        return new(fixture.Files.WorkspaceRoot, fixture.Files.StorageScope, paths, services, fixture.Journal, fixture.Files.Store,
            [provider], [provider], new(policy ?? new Policy(), includeProcessPolicy ? [new ProcessToolInvocationPolicyContextContributor()] : []),
            policies, NullLogger<MafSavedEffectDispatcher>.Instance);
    }

    private sealed class Policy(bool deny = false) : IAgentToolInvocationPolicy {
        private readonly DefaultAgentToolInvocationPolicy inner = new();
        internal List<ToolInvocationPolicyContext> Contexts { get; } = [];
        public ValueTask<ToolInvocationPolicyDecision> EvaluateAsync(ToolInvocationPolicyContext context, CancellationToken cancellationToken) {
            Contexts.Add(context);
            return deny ? ValueTask.FromResult(ToolInvocationPolicyDecision.Deny("fixture", "Current owner policy refused."))
                : inner.EvaluateAsync(context, cancellationToken);
        }
    }

    private sealed class Provider(AgentInheritedPlanningFixture fixture,
        AgentRuntimeToolProviderPurpose purpose = AgentRuntimeToolProviderPurpose.GovernedProcessAutomation)
        : IAgentRuntimeToolProvider, IAgentToolReceiptReconciliationProvider {
        public int Order => 0;
        public AgentRuntimeToolProviderDescriptor Descriptor { get; } = new("inherited-saved-owner", "Inherited saved owner",
            string.Empty, supportedPurposes: [purpose]);
        internal int Restored { get; private set; }
        internal int Entered { get; private set; }
        internal int Receipts { get; private set; }
        internal bool Denied { get; init; }
        internal Action? AfterRestore { get; init; }
        internal Action? AfterCommit { get; init; }
        public bool Supports(string toolName) => toolName == AgentPlanningLiveOwnerFixture.Publication().Payload.ToolName;
        public bool SupportsSavedEffect(AgentToolPreparedPayload original) => Supports(original.ToolName);
        public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(AgentRuntimeToolProviderContext context, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Saved dispatch cannot compose new provider tools.");

        public ValueTask<AgentRuntimeSavedEffectTool> RestoreSavedEffectAsync(AgentRuntimeSavedEffectContext context,
            AgentToolPreparedPayload original, CancellationToken cancellationToken) {
            Restored++;
            Assert.Equal(fixture.Original.ToolAdmission!.Session, context.Session);
            Assert.Equal(fixture.Original.ToolAdmission.PlanningDecision!.InheritedOrigin, context.InheritedOrigin);
            Assert.Equal(fixture.Files.StorageScope, context.ContextScope);
            Assert.Null(context.Governance);
            var function = AIFunctionFactory.Create(() => {
                Entered++;
                Assert.NotNull(AgentToolInvocationClaim.Current);
                Assert.Equal(fixture.Original.ProcessRunId, WorkspaceExecutionAuditContext.Current!.ProcessRunId);
                AgentToolInvocationEffectScope.RecordCommitted(Descriptor.ProviderKey, original.Digest.Value);
                AfterCommit?.Invoke();
                return "accepted";
            }, original.ToolName);
            var metadata = new AgentRuntimeToolMetadata(Descriptor.ProviderKey, original.ToolName, AgentRuntimeToolOperationKind.Mutation, true) {
                RestoreAdmission = (arguments, source) => {
                    Assert.Equal(original.ArgumentsJson, arguments.GetRawText());
                    Assert.Equal(original.SourcePreparation, source);
                    return original;
                },
                AuthorizeAdmissionAsync = async (_, token) => {
                    if (Denied) {
                        throw new UnauthorizedAccessException("Current module authority removed.");
                    }
                    var claim = AgentToolInvocationClaim.Current!;
                    await claim.RequireCurrentIdentityAsync(token);
                    var current = await fixture.ReadAsync();
                    var held = await fixture.Files.Store.AcquirePlanningAuthoritySnapshotAsync(AgentPlanningPublicationExpectation.Inherited(
                        fixture.Files.StorageScope, current.ToolAdmission!.Session, current.Revision, current.ToolAdmission.Revision), token);
                    try {
                        Assert.Equal(TimeSpan.FromSeconds(29), AgentToolRunLease.Current!.PlanningWork!.RequirePublication(held, claim.Binding));
                        return held;
                    } catch {
                        await held.DisposeAsync();
                        throw;
                    }
                },
                AuthorizeResultDisclosureAsync = (_, _) => throw new InvalidOperationException("Saved dispatch returns terminal binding knowledge.")
            };
            AfterRestore?.Invoke();
            return ValueTask.FromResult(new AgentRuntimeSavedEffectTool(function, metadata));
        }

        public async ValueTask<AgentToolReceiptObservation> ReconcileAsync(AgentToolReceiptReconciliationClaim claim,
            CancellationToken cancellationToken = default) {
            Receipts++;
            await claim.RequireAsync(cancellationToken);
            return AgentToolReceiptObservation.NotObserved;
        }
    }
}
