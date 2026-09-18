using System.Runtime.CompilerServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedKernel.Streaming;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class MafHrResultDisclosureIntegrationTests {
    private const string ActivationCallId = "original-activation";
    private const string FailedReadCallId = "original-wrong-kind-read";
    private const string InterruptedQueryMessage = "Injected interruption after the CRM owner returned a missing record.";

    [Fact]
    public async Task Core_recovers_the_original_HR_run_after_activation_and_wrong_kind_read_without_repeating_the_mutation() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var canonical = (await workspace.ListAgentsAsync(includeTemplates: true)).Single(item => item.Id == HrAgentIdentity.AgentId);
        var capabilities = (await workspace.ListCapabilitiesAsync()).Where(item => item.Kind == CapabilityKind.Tool).ToArray();
        var available = capabilities.Select(item => item.Id).ToHashSet();
        var profile = services.GetRequiredService<IDatabaseRuntimeState>().GetSnapshot();
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true,
            profileBinding: new(profile.ActiveProfileId!.Value, profile.ActiveFingerprint!, new(profile.Generation)),
            configureAgent: seed => canonical with {
                ProviderProfileId = seed.ProviderProfileId, ChatHistoryMode = AgentChatHistoryMode.FrameworkManaged,
                ConfigurationJson = AgentThinkingEffortPolicy.WriteAgentOverride(canonical.ConfigurationJson, AgentReasoningEffortLevel.Medium),
                Capabilities = canonical.Capabilities.Where(item => available.Contains(item.CapabilityId)).ToArray()
            });
        var runtimeAgent = (await fixture.Store.LoadCatalogAsync()).Agents.Single(item => item.Id == fixture.Agent.Id);
        var provider = fixture.Provider with {
            Kind = ProviderKind.OpenAi, Transport = ProviderTransportKind.Responses, DefaultModel = runtimeAgent.Model,
            ConfigurationJson = ProviderModelThinkingConfiguration.Write(fixture.Provider.ConfigurationJson, runtimeAgent.Model,
                new(runtimeAgent.Model, AgentThinkingEffortSupportStatus.Supported, AgentThinkingEffortControlMode.EffortLevels,
                    [AgentReasoningEffortLevel.Medium], AgentReasoningEffortLevel.Medium))
        };
        await fixture.Store.UpdateCatalogAsync(catalog => catalog with {
            Capabilities = capabilities,
            Providers = catalog.Providers.Select(item => item.Id == provider.Id ? provider : item).ToArray()
        });
        var name = $"HR original recovery target {Guid.NewGuid():N}";
        var target = await services.GetRequiredService<HrAgentAdministrationService>().CreateAsync(HrAgentIdentity.AgentId,
            new(name, "Recovery fixture", "Retained activation", "Follow explicit operator requests."), default);
        Assert.Equal(AgentLifecycleStatus.Draft, target.Status);
        var activation = new HrAgentSettingsUpdateInput(target.AgentId, target.UpdatedAtUtc, Status: AgentLifecycleStatus.Active);
        var wrongKind = new CrmHrAgentItemReference(CrmHrAgentRecordKind.Party, target.AgentId);
        var owner = new InterruptOnceQueryOwner(services.GetRequiredService<ICrmHrAgentQueryService>());
        var tools = new HrAgentRuntimeToolProvider(services.GetRequiredService<HrAgentAdministrationService>(),
            services.GetRequiredService<HrAgentAvatarGenerationService>(), services.GetRequiredService<HrAgentUsageAnalyticsService>(),
            services.GetRequiredService<HrAgentProcessReviewService>(), owner, services.GetRequiredService<ICrmPartyCommandService>(),
            services.GetRequiredService<HrAgentRuntimeAuthorizationService>());
        AgentChatRunResult pending;
        using (var initial = new HrCoreRecoveryExecution(fixture, services, new ActivationThenReadClient(activation, wrongKind), tools)) {
            pending = await initial.Service.SendMessageAsync(fixture.Agent.Id, null, "Activate the agent, then inspect its CRM record.",
                new(AgentExecutionOperationId.New(), WorkspaceToolsEnabled: false) {
                    Context = ExecutionInvocationContext.Empty with {
                        SourceKind = "agents", SourceId = "agents", MetadataJson = fixture.Detail.Run.MetadataJson,
                        Policy = new(FinalizerMode: AgentFinalizerMode.Disabled)
                    }
                });
        }
        Assert.Equal(ExecutionState.WaitingOnTool, pending.State);
        var approval = Assert.Single((await fixture.NewStore().GetExecutionRunAsync(pending.ExecutionRunId))!.PendingApprovals);
        Assert.Equal(0, owner.Calls);
        Assert.Equal(AgentLifecycleStatus.Draft, (await workspace.ListAgentsAsync()).Single(item => item.Id == target.AgentId).Status);
        using (var approved = new HrCoreRecoveryExecution(fixture, services, new ActivationThenReadClient(activation, wrongKind), tools)) {
            await Assert.ThrowsAnyAsync<Exception>(() => approved.Service.ContinueExecutionRunAsync(pending.ExecutionRunId,
                AgentExecutionOperationId.New(), [new(approval.ApprovalId, true) { ToolAdmission = approval.ToolAdmission }]));
        }
        Assert.True(owner.Interrupted);
        Assert.Equal(1, owner.Calls);
        var original = (await fixture.NewStore().GetExecutionRunDetailAsync(pending.ExecutionRunId))!;
        Assert.Equal(ExecutionState.Failed, original.Run.State);
        Assert.Empty(original.Run.PendingApprovals);
        Assert.True(original.Run.ToolAdmission!.HasUnresolvedEffects);
        var originalProposals = original.Run.ToolAdmission.Batches.SelectMany(batch => batch.Proposals).ToArray();
        var committed = Assert.Single(originalProposals, item => item.CallId == ActivationCallId);
        var uncertain = Assert.Single(originalProposals, item => item.CallId == FailedReadCallId);
        Assert.Equal(AgentToolProposalState.Completed, committed.State);
        Assert.Equal(AgentToolEffectState.Committed, committed.EffectState);
        Assert.Equal(AgentToolProposalRecovery.ReconcileBeforeRetry, committed.Payload.Recovery);
        Assert.Equal(ExecutionApprovalStatus.Approved, committed.ApprovalStatus);
        Assert.Equal(committed.Payload.Digest, committed.ApprovedDigest);
        Assert.Equal(AgentToolProposalState.ReconciliationRequired, uncertain.State);
        Assert.Equal(AgentToolEffectState.Unknown, uncertain.EffectState);
        Assert.Null(uncertain.Result);
        var ownerAfterActivation = await ReadMutationOwnerAsync(services, HrAgentToolPolicy.HrAgentSettingsUpdate, name, null);
        Assert.Equal(AgentLifecycleStatus.Active, (await workspace.ListAgentsAsync()).Single(item => item.Id == target.AgentId).Status);

        var client = new ActivationThenReadClient(activation, wrongKind);
        using (var restarted = new HrCoreRecoveryExecution(fixture, services, client, tools)) {
            var recovered = await restarted.Service.RecoverExecutionRunAsync(pending.ExecutionRunId, AgentExecutionOperationId.New());
            Assert.Equal(pending.ExecutionRunId, recovered.ExecutionRunId);
            Assert.Equal(ExecutionState.Completed, recovered.State);
        }
        Assert.Equal(1, client.Requests);
        Assert.Equal(CrmHrAgentQueryErrorCodes.RecordNotFound, client.ReadFailureCode);
        Assert.Equal(2, owner.Calls);
        var saved = (await fixture.NewStore().GetExecutionRunDetailAsync(pending.ExecutionRunId))!;
        Assert.Equal(original.Run.ChatSessionId, saved.Run.ChatSessionId);
        Assert.Equal(original.Run.ToolAdmission.OriginalInput, saved.Run.ToolAdmission!.OriginalInput);
        Assert.Equal(original.Run.ToolAdmission.RuntimeContext, saved.Run.ToolAdmission.RuntimeContext);
        Assert.Equal(original.Run.ToolAdmission.Session, saved.Run.ToolAdmission.Session);
        Assert.Equal(JsonSerializer.Serialize(original.Run.ToolAdmission.Segments), JsonSerializer.Serialize(saved.Run.ToolAdmission.Segments));
        Assert.False(saved.Run.ToolAdmission.HasUnresolvedEffects);
        Assert.Empty(saved.Run.PendingApprovals);
        Assert.Equal(original.Approvals, saved.Approvals);
        var proposals = saved.Run.ToolAdmission.Batches.SelectMany(batch => batch.Proposals).ToArray();
        Assert.Equal(originalProposals.Select(item => item.IntentId), proposals.Select(item => item.IntentId));
        Assert.Equal(committed, Assert.Single(proposals, item => item.CallId == ActivationCallId));
        var read = Assert.Single(proposals, item => item.CallId == FailedReadCallId);
        Assert.Equal(AgentToolProposalState.Completed, read.State);
        Assert.Equal(AgentToolEffectState.None, read.EffectState);
        Assert.NotNull(read.Result);
        Assert.Equal(uncertain with {
            State = read.State, EffectState = read.EffectState, Result = read.Result, DispatchClaimId = read.DispatchClaimId
        }, read);
        var originalReceiptIds = original.ToolReceipts.Select(item => item.Id).ToHashSet();
        foreach (var originalReceipt in original.ToolReceipts) {
            Assert.Equal(originalReceipt, Assert.Single(saved.ToolReceipts, item => item.Id == originalReceipt.Id));
        }
        var activationReceipt = Assert.Single(saved.ToolReceipts,
            item => item.ToolName == HrAgentToolPolicy.HrAgentSettingsUpdate && !originalReceiptIds.Contains(item.Id));
        Assert.Equal(AgentToolInvocationOutcome.Succeeded, activationReceipt.InvocationOutcome);
        Assert.Equal(AgentToolEffectState.Committed, activationReceipt.EffectState);
        Assert.Equal("agent-catalog", activationReceipt.EffectSourceKind);
        Assert.Equal(target.AgentId.ToString("D"), activationReceipt.EffectSourceId);
        var receipt = Assert.Single(saved.ToolReceipts,
            item => item.ToolName == HrAgentToolPolicy.HrCrmItemSummaryGet && !originalReceiptIds.Contains(item.Id));
        Assert.Equal(AgentToolInvocationOutcome.Failed, receipt.InvocationOutcome);
        Assert.Equal(AgentToolEffectState.None, receipt.EffectState);
        Assert.Equal(CrmHrAgentQueryErrorCodes.RecordNotFound, receipt.FailureCode);
        Assert.DoesNotContain(InterruptedQueryMessage, receipt.FailureMessage, StringComparison.Ordinal);
        Assert.Equal(ownerAfterActivation, await ReadMutationOwnerAsync(services, HrAgentToolPolicy.HrAgentSettingsUpdate, name, null));

        var replay = new ActivationThenReadClient(activation, wrongKind);
        using (var completed = new HrCoreRecoveryExecution(fixture, services, replay, tools)) {
            var result = await completed.Service.RecoverExecutionRunAsync(pending.ExecutionRunId, AgentExecutionOperationId.New());
            Assert.Equal(pending.ExecutionRunId, result.ExecutionRunId);
            Assert.Equal(ExecutionState.Completed, result.State);
        }
        Assert.Equal(0, replay.Requests);
        Assert.Equal(2, owner.Calls);
        Assert.Equal(ownerAfterActivation, await ReadMutationOwnerAsync(services, HrAgentToolPolicy.HrAgentSettingsUpdate, name, null));
        var final = (await fixture.NewStore().GetExecutionRunDetailAsync(pending.ExecutionRunId))!;
        Assert.Equal(JsonSerializer.Serialize(saved.Run.ToolAdmission), JsonSerializer.Serialize(final.Run.ToolAdmission));
        Assert.Equal(saved.ToolReceipts, final.ToolReceipts);
        Assert.Equal(original.Approvals, final.Approvals);
        Assert.Equal(2, (await fixture.NewStore().ListExecutionRunsAsync()).Count);
    }

    private sealed class InterruptOnceQueryOwner(ICrmHrAgentQueryService owner) : ICrmHrAgentQueryService {
        internal int Calls { get; private set; }
        internal bool Interrupted { get; private set; }

        public Task<Result<IReadOnlyList<CrmHrAgentQueryItem>>> SearchAsync(CrmHrAgentSearchQuery query,
            CancellationToken cancellationToken = default) => owner.SearchAsync(query, cancellationToken);

        public async Task<Result<CrmHrAgentQueryItem>> GetSummaryAsync(CrmHrAgentItemReference reference,
            CancellationToken cancellationToken = default) {
            Calls++;
            var result = await owner.GetSummaryAsync(reference, cancellationToken);
            Assert.True(result.IsFailure);
            Assert.Equal(CrmHrAgentQueryErrorCodes.RecordNotFound, Assert.Single(result.Errors).Code);
            if (!Interrupted) {
                Interrupted = true;
                throw new IOException(InterruptedQueryMessage);
            }
            return result;
        }
    }

    private sealed class HrCoreRecoveryExecution : IDisposable {
        private readonly AgentExecutionPreparationCache cache = new(AgentExecutionPreparationCachePolicy.Default);

        internal HrCoreRecoveryExecution(AgentToolAdmissionJournalFixture fixture, IServiceProvider services,
            ActivationThenReadClient client, HrAgentRuntimeToolProvider provider) {
            var store = fixture.NewStore();
            var journal = fixture.NewJournal(store);
            var policies = new AgentToolPolicyCatalog(HrAgentToolPolicy.Capabilities);
            var dependencies = MafAgentRuntimeDependencies.FromServices(services);
            dependencies = dependencies with {
                ProviderAgentFactory = new ActivationThenReadAgentFactory(client), ToolAdmissionJournal = journal, ToolPolicies = policies,
                RuntimeToolProviderComposer = new RuntimeToolProviderComposer(new RuntimeToolProviderAccessFilter(policies), policies),
                CapabilityDependencies = dependencies.CapabilityDependencies with {
                    RuntimeToolProviders = [provider], ContextContributors = [], ToolPolicies = policies
                }
            };
            var runtime = new MafAgentRuntime(fixture.WorkspaceRoot, fixture.StorageScope, dependencies);
            var coordinator = new AgentExecutionActivityCoordinator(new PartitionedSequencedStream<AgentExecutionActivityStreamId,
                AgentExecutionActivity>(PartitionedSequencedStreamPolicy.Default, TimeProvider.System), TimeProvider.System);
            Service = new(store, new ZipAgentPackageService(fixture.WorkspaceRoot, fixture.StorageScope), runtime.ExecutionPort,
                runtime.ContinuationPort, runtime.DiagnosticsPort, runtime.ModelAdministrationPort,
                new CapabilityProofService(new PhysicalFileSystemPathPolicyFactory()), NullLogger<AgentFrameworkWorkspaceService>.Instance,
                coordinator, new(fixture.Profile.ProfileId, fixture.StorageScope, fixture.Profile.Generation), cache,
                new FixedAgentExecutionProfileGenerationSource(fixture.Profile.Generation), new RecoveryNoProcessLeases(),
                new ExternalTargetPathRegistryFactory(), toolAdmissionJournal: journal,
                executionAuthorityResolver: new RecoveryCurrentAuthority(fixture), toolPolicies: policies);
        }

        internal AgentFrameworkWorkspaceService Service { get; }

        public void Dispose() {
            Service.Dispose();
            cache.Dispose();
        }
    }

    private sealed class RecoveryCurrentAuthority(AgentToolAdmissionJournalFixture fixture) : IAgentExecutionAuthorityResolver {
        public ValueTask<AgentExecutionAuthorityRecord> ResolveAsync(AgentExecutionAuthorityResolutionRequest request,
            CancellationToken cancellationToken = default) {
            var original = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(fixture.Detail.Run.MetadataJson)!;
            var source = AgentTurnContextMetadata.TryReadTurnContextReference(fixture.Detail.Run.MetadataJson)!;
            Assert.Equal(fixture.Agent.Id, request.AgentId);
            Assert.Equal(fixture.Profile.Generation, request.ExpectedDatabaseProfileGeneration);
            Assert.Equal(original.WorkspaceScope, request.ObservedWorkspaceScope);
            Assert.Equal(source.SourceKind, request.SourceKind);
            Assert.Equal(source.SourceId, request.SourceId);
            return ValueTask.FromResult(new AgentExecutionAuthorityRecord(AgentExecutionAuthorityId.Create(), fixture.Agent.Id,
                fixture.Profile.ProfileId, fixture.Profile.Generation, original.WorkspaceScope, true, true,
                original.PolicyVersion, original.PolicyFingerprint, DateTimeOffset.UtcNow));
        }
    }

    private sealed class RecoveryNoProcessLeases : IWorkspaceExecutionRunProcessLeaseCleaner {
        public Task<WorkspaceExecutionRunProcessCleanupResult> CleanupAsync(Guid executionRunId)
            => Task.FromResult(WorkspaceExecutionRunProcessCleanupResult.Empty(executionRunId));
    }

    private sealed class ActivationThenReadAgentFactory(ActivationThenReadClient client) : IMafProviderAgentFactory {
        public AIAgent CreateFrameworkAgent(ProviderProfile provider, string model, ChatClientAgentOptions options,
            bool frameworkManagedHistory, bool allowBackgroundResponses) {
            Assert.True(frameworkManagedHistory);
            Assert.NotNull(options.ChatHistoryProvider);
            Assert.Equal(AgentThinkingEffortCapabilitySource.Configured, AgentThinkingEffortPolicy.ResolveCapability(provider, model).Source);
            Assert.IsType<ApprovalRequiredAIFunction>(Assert.Single(options.ChatOptions!.Tools!, item => item.Name == HrAgentToolPolicy.HrAgentSettingsUpdate));
            Assert.Contains(options.ChatOptions.Tools!, item => item.Name == HrAgentToolPolicy.HrCrmItemSummaryGet);
            return new ChatClientAgent(new MafToolAdmissionChatClient(client), options);
        }
    }

    private sealed class ActivationThenReadClient(HrAgentSettingsUpdateInput activation, CrmHrAgentItemReference read) : IChatClient {
        internal int Requests { get; private set; }
        internal string? ReadFailureCode { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) {
            Requests++;
            var results = messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().ToArray();
            var readResult = results.SingleOrDefault(item => item.CallId == FailedReadCallId);
            if (readResult is not null) {
                var failure = JsonSerializer.SerializeToElement(readResult.Result, MafToolProtocolCodec.SerializationOptions);
                ReadFailureCode = failure.GetProperty("errorCode").GetString();
                Assert.False(failure.GetProperty("succeeded").GetBoolean());
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Activation is complete; no CRM party exists for that agent id.")));
            }
            var activated = results.Any(item => item.CallId == ActivationCallId);
            object request = activated ? read : activation;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(
                activated ? FailedReadCallId : ActivationCallId,
                activated ? HrAgentToolPolicy.HrCrmItemSummaryGet : HrAgentToolPolicy.HrAgentSettingsUpdate,
                new Dictionary<string, object?> { ["request"] = request })])));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var update in response.ToChatResponseUpdates()) {
                yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose() { }
    }
}
