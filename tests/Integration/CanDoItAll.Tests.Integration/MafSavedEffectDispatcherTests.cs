using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Tools.Documents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Integration.Runtime.AgentSavedEffectDispatchFixture;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class MafSavedEffectDispatcherTests {
    [Fact]
    public async Task Original_approved_effect_executes_once_under_real_publication_exclusion_without_provider_continuation() {
        await using var fixture = await CreateAsync(1);
        var owner = fixture.Files.NewJournal();
        var provider = new SavedProvider(fixture);
        var policy = new RecordingPolicy();
        var reader = new ReceiptReader();
        var dispatcher = CreateDispatcher(fixture, owner, [provider], [reader], policy);
        await using var lease = await owner.AcquireSavedEffectDispatchAsync(
            Request(fixture.Prepared, decisions: Decisions(fixture.Prepared, true)), AllowRead, default);
        using var bound = lease.Bind();
        var request = new AgentSavedEffectDispatchRequest(fixture.Files.Session, Binding(fixture.Prepared));

        var result = await dispatcher.DispatchAsync(request, default);
        var persisted = (await fixture.ReadAsync())!;
        var proposal = Assert.Single(Assert.Single(persisted.ToolAdmission!.Batches).Proposals);
        Assert.Equal(AgentToolEffectState.Committed, result.EffectState);
        Assert.Equal(AgentToolProposalState.Completed, result.State);
        Assert.Equal(persisted.Revision, result.RunRevision);
        Assert.Equal(persisted.ToolAdmission.Revision, result.JournalRevision);
        Assert.Equal(1, provider.Entered);
        Assert.Equal(1, provider.Released);
        Assert.Equal(1, provider.Restored);
        Assert.Equal(1, reader.Calls);
        Assert.Equal(fixture.Prepared.ToolAdmission!.Batches[0].Proposals[0].Payload, proposal.Payload);
        AssertOriginPreserved(fixture.Prepared.ToolAdmission.Segments[0], persisted.ToolAdmission.Segments[0]);
        Assert.Null(persisted.SerializedSessionStateJson);
        Assert.True(persisted.ToolAdmission.ProviderDispatches.IsDefaultOrEmpty);
        Assert.Equal("accepted", Assert.IsType<JsonElement>(
            MafToolResultCheckpointCodec.Restore(proposal.Result!, proposal.EffectState)).GetString());
        var evaluated = Assert.Single(policy.Contexts);
        Assert.True(evaluated.ApplicationApprovalAvailable);
        Assert.False(evaluated.AutoApprovalAllowed);
        Assert.False(evaluated.ApprovalWrapperAvailable);
        Assert.Equal(fixture.Prepared.Id.ToString("D"), evaluated.ExecutionRunId);
        Assert.Equal(fixture.Prepared.SourceId, evaluated.SourceId);
        Assert.Equal(AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(fixture.Prepared.MetadataJson), evaluated.ExecutionGovernance);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => dispatcher.DispatchAsync(request, default));
        Assert.Equal(1, provider.Entered);
        Assert.Equal(1, provider.Restored);
    }

    [Fact]
    public async Task Rejected_selection_checkpoints_without_creating_or_claiming_a_tool() {
        await using var fixture = await CreateAsync(1);
        var owner = fixture.Files.NewJournal();
        await using var lease = await owner.AcquireSavedEffectDispatchAsync(
            Request(fixture.Prepared, decisions: Decisions(fixture.Prepared, false)), AllowRead, default);
        using var bound = lease.Bind();
        var result = await CreateDispatcher(fixture, owner, [], []).DispatchAsync(
            new(fixture.Files.Session, Binding(fixture.Prepared)), default);

        Assert.Equal(AgentToolProposalState.Rejected, result.State);
        Assert.Null((await fixture.ReadAsync())!.ToolAdmission!.Batches[0].Proposals[0].DispatchClaimId);
    }

    [Theory]
    [InlineData(SelectionFailure.MissingTool)]
    [InlineData(SelectionFailure.AmbiguousTool)]
    [InlineData(SelectionFailure.MissingReceipt)]
    [InlineData(SelectionFailure.AmbiguousReceipt)]
    public async Task Missing_or_ambiguous_owner_refuses_before_restore_and_claim(SelectionFailure failure) {
        await using var fixture = await CreateAsync(1);
        var owner = fixture.Files.NewJournal();
        var provider = new SavedProvider(fixture);
        IAgentRuntimeToolProvider[] tools = failure switch {
            SelectionFailure.MissingTool => [],
            SelectionFailure.AmbiguousTool => [provider, new SavedProvider(fixture)],
            _ => [provider]
        };
        IAgentToolReceiptReconciliationProvider[] receipts = failure switch {
            SelectionFailure.MissingReceipt => [],
            SelectionFailure.AmbiguousReceipt => [new ReceiptReader(), new ReceiptReader()],
            _ => [new ReceiptReader()]
        };
        await using var lease = await owner.AcquireSavedEffectDispatchAsync(
            Request(fixture.Prepared, decisions: Decisions(fixture.Prepared, true)), AllowRead, default);
        using var bound = lease.Bind();
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => CreateDispatcher(fixture, owner, tools, receipts)
            .DispatchAsync(new(fixture.Files.Session, Binding(fixture.Prepared)), default));
        Assert.Equal(0, provider.Restored);
        Assert.Equal(0, provider.Entered);
        Assert.Null((await fixture.ReadAsync())!.ToolAdmission!.Batches[0].Proposals[0].DispatchClaimId);
    }

    [Theory]
    [InlineData(RestorationFailure.Arguments)]
    [InlineData(RestorationFailure.Source)]
    public async Task Changed_original_restoration_cannot_claim_or_probe_an_owner_receipt(RestorationFailure failure) {
        await using var fixture = await CreateAsync(1);
        var owner = fixture.Files.NewJournal();
        var provider = new SavedProvider(fixture) { RestorationFailure = failure };
        var reader = new ReceiptReader();
        await using var lease = await owner.AcquireSavedEffectDispatchAsync(
            Request(fixture.Prepared, decisions: Decisions(fixture.Prepared, true)), AllowRead, default);
        using var bound = lease.Bind();
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => CreateDispatcher(fixture, owner, [provider], [reader])
            .DispatchAsync(new(fixture.Files.Session, Binding(fixture.Prepared)), default));
        Assert.Equal(0, provider.Entered);
        Assert.Equal(0, reader.Calls);
        Assert.Null((await fixture.ReadAsync())!.ToolAdmission!.Batches[0].Proposals[0].DispatchClaimId);
    }

    [Fact]
    public async Task Missing_foreign_or_wrong_binding_lease_cannot_restore_an_effect() {
        await using var fixture = await CreateAsync(2);
        var owner = fixture.Files.NewJournal();
        var provider = new SavedProvider(fixture);
        var dispatcher = CreateDispatcher(fixture, owner, [provider], [new ReceiptReader()]);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => dispatcher.DispatchAsync(
            new(fixture.Files.Session, Binding(fixture.Prepared)), default));
        await using var lease = await owner.AcquireSavedEffectDispatchAsync(
            Request(fixture.Prepared, decisions: Decisions(fixture.Prepared, true, true)), AllowRead, default);
        using var bound = lease.Bind();
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => dispatcher.DispatchAsync(
            new(fixture.Files.Session, Binding(fixture.Prepared, 1)), default));
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => CreateDispatcher(fixture, fixture.Files.NewJournal(), [provider], [new ReceiptReader()])
            .DispatchAsync(new(fixture.Files.Session, Binding(fixture.Prepared)), default));
        var foreignSession = new AgentToolSessionReference(Guid.NewGuid(), fixture.Files.Session.ChatSessionId, fixture.Files.Session.AuthorityId);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => dispatcher.DispatchAsync(new(foreignSession, Binding(fixture.Prepared)), default));
        Assert.Equal(0, provider.Restored);
    }

    [Fact]
    public async Task Existing_receipt_for_undispatched_effect_does_not_invent_a_claim_or_repeat_the_effect() {
        await using var fixture = await CreateAsync(1);
        var owner = fixture.Files.NewJournal();
        var provider = new SavedProvider(fixture);
        await using var lease = await owner.AcquireSavedEffectDispatchAsync(
            Request(fixture.Prepared, decisions: Decisions(fixture.Prepared, true)), AllowRead, default);
        using var bound = lease.Bind();
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => CreateDispatcher(fixture, owner, [provider], [new ReceiptReader(committed: true)])
            .DispatchAsync(new(fixture.Files.Session, Binding(fixture.Prepared)), default));
        Assert.Equal(0, provider.Entered);
        Assert.Null((await fixture.ReadAsync())!.ToolAdmission!.Batches[0].Proposals[0].DispatchClaimId);
    }

    [Theory]
    [InlineData(Denial.GenericPolicy)]
    [InlineData(Denial.ModuleAuthorization)]
    [InlineData(Denial.AgentPermission)]
    public async Task Current_denial_is_known_not_committed_and_never_enters_the_effect(Denial denial) {
        await using var fixture = await CreateAsync(1);
        if (denial == Denial.AgentPermission) {
            var changed = await fixture.Files.Store.UpdateCatalogAsync(catalog => catalog with {
                Agents = catalog.Agents.Select(agent => agent.Id == fixture.Files.Agent.Id
                    ? agent with { Permissions = agent.Permissions with { CanUseTools = false } } : agent).ToArray()
            });
            Assert.False(changed.Agents.Single(agent => agent.Id == fixture.Files.Agent.Id).Permissions.CanUseTools);
        }
        var owner = fixture.Files.NewJournal();
        var provider = new SavedProvider(fixture) { Denied = denial == Denial.ModuleAuthorization };
        var policy = new RecordingPolicy(deny: denial == Denial.GenericPolicy);
        await using var lease = await owner.AcquireSavedEffectDispatchAsync(
            Request(fixture.Prepared, decisions: Decisions(fixture.Prepared, true)), AllowRead, default);
        using var bound = lease.Bind();
        var result = await CreateDispatcher(fixture, owner, [provider], [new ReceiptReader()], policy)
            .DispatchAsync(new(fixture.Files.Session, Binding(fixture.Prepared)), default);
        Assert.Equal(AgentToolEffectState.NotCommitted, result.EffectState);
        Assert.Equal(AgentToolProposalState.Completed, result.State);
        Assert.Equal(0, provider.Entered);
        var proposal = (await fixture.ReadAsync())!.ToolAdmission!.Batches[0].Proposals[0];
        using var replay = AgentToolInvocationEffectScope.Begin();
        var failure = Assert.IsType<JsonElement>(MafToolResultCheckpointCodec.Restore(proposal.Result!, proposal.EffectState));
        Assert.Equal("ToolPolicyDenied", failure.GetProperty("errorCode").GetString());
        Assert.False(failure.GetProperty("canRetryWithCorrectedInput").GetBoolean());
        Assert.Equal("ToolPolicyDenied", replay.PreDispatchFailure!.FailureCode);
    }

    [Theory]
    [InlineData(BodyFailure.Exception)]
    [InlineData(BodyFailure.Cancellation)]
    [InlineData(BodyFailure.UnknownResult)]
    [InlineData(BodyFailure.ReleaseAfterCommit)]
    public async Task Entered_or_unacknowledged_effect_remains_uncertain_and_cannot_redispatch(BodyFailure failure) {
        await using var fixture = await CreateAsync(1);
        var owner = fixture.Files.NewJournal();
        var provider = new SavedProvider(fixture) { BodyFailure = failure };
        var dispatcher = CreateDispatcher(fixture, owner, [provider], [new ReceiptReader()]);
        await using var lease = await owner.AcquireSavedEffectDispatchAsync(
            Request(fixture.Prepared, decisions: Decisions(fixture.Prepared, true)), AllowRead, default);
        using var bound = lease.Bind();
        var request = new AgentSavedEffectDispatchRequest(fixture.Files.Session, Binding(fixture.Prepared));
        var exception = await Record.ExceptionAsync(() => dispatcher.DispatchAsync(request, default));
        Assert.NotNull(exception);
        var proposal = (await fixture.ReadAsync())!.ToolAdmission!.Batches[0].Proposals[0];
        Assert.Equal(AgentToolProposalState.ReconciliationRequired, proposal.State);
        Assert.Equal(AgentToolEffectState.Unknown, proposal.EffectState);
        Assert.NotNull(proposal.DispatchClaimId);
        Assert.Equal(1, provider.Entered);
        Assert.Equal(1, provider.Released);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => dispatcher.DispatchAsync(request, default));
        Assert.Equal(1, provider.Entered);
        Assert.Equal(1, provider.Restored);
        await using var unlocked = await fixture.Files.Store.AcquireAgentReadLeaseAsync(fixture.Files.Agent.Id);
        Assert.NotNull(unlocked.Agent);
    }

    [Fact]
    public async Task Lost_journal_acknowledgement_keeps_committed_evidence_and_never_repeats_the_body() {
        await using var fixture = await CreateAsync(1);
        var armed = false;
        var faultedStore = fixture.Files.NewStore(stage => {
            if (armed && stage == ExistingRunDetailCommitStage.RunPersisted) {
                armed = false;
                throw new IOException("saved-result-acknowledgement-lost");
            }
        });
        var owner = fixture.Files.NewJournal(faultedStore);
        var provider = new SavedProvider(fixture) { AfterCommit = () => armed = true };
        var dispatcher = CreateDispatcher(fixture, owner, [provider], [new ReceiptReader()]);
        await using var lease = await owner.AcquireSavedEffectDispatchAsync(
            Request(fixture.Prepared, decisions: Decisions(fixture.Prepared, true)), AllowRead, default);
        using var bound = lease.Bind();
        var request = new AgentSavedEffectDispatchRequest(fixture.Files.Session, Binding(fixture.Prepared));
        var failure = await Record.ExceptionAsync(() => dispatcher.DispatchAsync(request, default));

        AssertIoCause(failure, "saved-result-acknowledgement-lost");
        var proposal = (await fixture.ReadAsync())!.ToolAdmission!.Batches[0].Proposals[0];
        Assert.Equal(AgentToolProposalState.Completed, proposal.State);
        Assert.Equal(AgentToolEffectState.Committed, proposal.EffectState);
        Assert.NotNull(proposal.Result);
        Assert.Equal(1, provider.Entered);
        Assert.Equal(1, provider.Released);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => dispatcher.DispatchAsync(request, default));
        Assert.Equal(1, provider.Entered);
    }

    private static Task<AgentSavedEffectDispatchFixture> CreateAsync(int count)
        => AgentSavedEffectDispatchFixture.CreateAsync(count, canonicalCatalog: true);

    private static MafSavedEffectDispatcher CreateDispatcher(AgentSavedEffectDispatchFixture fixture, AgentToolAdmissionJournal owner,
        IReadOnlyList<IAgentRuntimeToolProvider> providers, IReadOnlyList<IAgentToolReceiptReconciliationProvider> receipts,
        RecordingPolicy? policy = null) {
        var paths = new PhysicalFileSystemPathPolicyFactory();
        var services = new WorkspaceRuntimeServicesFactory([], new ManagedCodeMarkItDownDocumentMarkdownConverter(),
            paths, new ExternalTargetPathRegistryFactory());
        var policies = new AgentToolPolicyCatalog([new(Payload(0).ToolName, ToolInvocationClassification.Mutation,
            true, true, ToolCapabilitySideEffectKind.InternalStateMutation, ToolCapabilityOperationRequirementKind.None,
            [], [], true, false, false, false, ToolCapabilityBrowserProofRole.None, ToolCapabilityIdempotencyDescriptor.StateChanging)]);
        return new(fixture.Files.WorkspaceRoot, fixture.Files.StorageScope, paths, services, owner, fixture.Files.Store,
            providers, receipts, new(policy ?? new RecordingPolicy(), []), policies, NullLogger<MafSavedEffectDispatcher>.Instance);
    }

    public enum SelectionFailure { MissingTool, AmbiguousTool, MissingReceipt, AmbiguousReceipt }
    public enum RestorationFailure { None, Arguments, Source }
    public enum Denial { GenericPolicy, ModuleAuthorization, AgentPermission }
    public enum BodyFailure { None, Exception, Cancellation, UnknownResult, ReleaseAfterCommit }

    private sealed class RecordingPolicy(bool deny = false) : IAgentToolInvocationPolicy {
        private readonly DefaultAgentToolInvocationPolicy inner = new();
        internal List<ToolInvocationPolicyContext> Contexts { get; } = [];
        public ValueTask<ToolInvocationPolicyDecision> EvaluateAsync(ToolInvocationPolicyContext context, CancellationToken cancellationToken) {
            Contexts.Add(context);
            return deny ? ValueTask.FromResult(ToolInvocationPolicyDecision.Deny("fixture", "Current owner policy refused."))
                : inner.EvaluateAsync(context, cancellationToken);
        }
    }

    private sealed class SavedProvider(AgentSavedEffectDispatchFixture fixture) : IAgentRuntimeToolProvider {
        public int Order => 0;
        public AgentRuntimeToolProviderDescriptor Descriptor { get; } = new("saved-owner", "Saved owner", string.Empty);
        internal int Restored { get; private set; }
        internal int Entered { get; private set; }
        internal int Released { get; private set; }
        internal bool Denied { get; init; }
        internal RestorationFailure RestorationFailure { get; init; }
        internal BodyFailure BodyFailure { get; init; }
        internal Action? AfterCommit { get; init; }
        public bool SupportsSavedEffect(AgentToolPreparedPayload original) => original.ToolName == Payload(0).ToolName;
        public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(AgentRuntimeToolProviderContext context, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Saved dispatch must not compose new model tools.");

        public ValueTask<AgentRuntimeSavedEffectTool> RestoreSavedEffectAsync(AgentRuntimeSavedEffectContext context,
            AgentToolPreparedPayload original, CancellationToken cancellationToken) {
            Restored++;
            Assert.Equal(fixture.Prepared.ToolAdmission!.Session, context.Session);
            var function = AIFunctionFactory.Create((int index) => {
                Entered++;
                Assert.Equal(0, index);
                Assert.Equal(Binding(fixture.Prepared), AgentToolInvocationClaim.Current!.Binding);
                if (BodyFailure == BodyFailure.Exception) {
                    throw new IOException("entered-effect-failure");
                }
                if (BodyFailure == BodyFailure.Cancellation) {
                    throw new OperationCanceledException("entered-effect-cancellation");
                }
                if (BodyFailure != BodyFailure.UnknownResult) {
                    AgentToolInvocationEffectScope.RecordCommitted("saved-owner", original.Digest.Value);
                    AfterCommit?.Invoke();
                }
                return "accepted";
            }, original.ToolName);
            var metadata = new AgentRuntimeToolMetadata(Descriptor.ProviderKey, original.ToolName, AgentRuntimeToolOperationKind.Mutation, true) {
                RestoreAdmission = (arguments, source) => {
                    Assert.Equal(original.ArgumentsJson, arguments.GetRawText());
                    Assert.Equal(original.SourcePreparation, source);
                    return RestorationFailure switch {
                        RestorationFailure.Arguments => new(original.ToolName, original.SemanticVersion, original.Digest, "{\"index\":1}", original.Effect, original.Recovery, source),
                        RestorationFailure.Source => new(original.ToolName, original.SemanticVersion, original.Digest, original.ArgumentsJson, original.Effect, original.Recovery, Envelope("{\"changed\":true}")),
                        _ => original
                    };
                },
                AuthorizeAdmissionAsync = async (_, token) => {
                    if (Denied) {
                        throw new UnauthorizedAccessException("Current module grant removed.");
                    }
                    var claim = AgentToolInvocationClaim.Current!;
                    await claim.RequireCurrentIdentityAsync(token);
                    var catalog = await fixture.Files.Store.AcquireAgentReadLeaseAsync(context.Agent.Id, token);
                    try {
                        var publication = await claim.AcquirePublicationAsync(catalog, token);
                        return new Authorization(publication, catalog, () => {
                            Released++;
                            if (BodyFailure == BodyFailure.ReleaseAfterCommit) {
                                throw new IOException("publication-release-failure");
                            }
                        });
                    } catch {
                        await catalog.DisposeAsync();
                        throw;
                    }
                },
                AuthorizeResultDisclosureAsync = (_, _) => throw new InvalidOperationException("Saved dispatch returns only terminal binding knowledge.")
            };
            return ValueTask.FromResult(new AgentRuntimeSavedEffectTool(function, metadata));
        }
    }

    private sealed class Authorization(AgentToolInvocationPublicationLease publication, IAgentCatalogReadLease catalog, Action released) : IAsyncDisposable {
        public async ValueTask DisposeAsync() {
            try {
                await publication.DisposeAsync();
            } finally {
                await catalog.DisposeAsync();
            }
            released();
        }
    }
}
