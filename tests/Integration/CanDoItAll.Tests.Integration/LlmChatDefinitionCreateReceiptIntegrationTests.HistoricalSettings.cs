using System.Runtime.CompilerServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Agents.SimpleChats;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Tests.Integration.Runtime;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.LlmChats;

public sealed partial class LlmChatDefinitionCreateReceiptIntegrationTests {
    [Theory]
    [InlineData(HistoricalReadChange.None)]
    [InlineData(HistoricalReadChange.ReadRevoked)]
    [InlineData(HistoricalReadChange.SettingsCapabilityRevoked)]
    [InlineData(HistoricalReadChange.ProfileChanged)]
    [InlineData(HistoricalReadChange.OriginalRevisionRemoved)]
    [InlineData(HistoricalReadChange.DefinitionRemoved)]
    public async Task Approved_settings_read_then_update_retains_R1_through_actual_MAF_journal_restart(HistoricalReadChange change) {
        await using var database = ReceiptDatabase.Create("simple-chat-historical-settings");
        var resolver = new Resolver();
        await using var application = await database.OpenAsync(resolver);
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var definitions = services.GetRequiredService<ILlmChatDefinitionApplicationService>();
        var command = CreateCommand().Definition;
        var created = await definitions.CreateAsync(command);
        Assert.True(created.IsSuccess);
        var first = created.Value!;
        var expected = new HrSimpleChatDefinitionVersion(first.Definition.Id.Value,
            first.Revision.Revision.Value, first.Definition.ConcurrencyToken);
        var update = new HrSimpleChatUpdateRequest(expected, command with {
            Name = "Updated name", Summary = "Updated summary", SystemPrompt = "R2 prompt only from the approved update",
            Tags = ["changed"], RevisionReason = "Second revision"
        });
        var profile = services.GetRequiredService<IDatabaseRuntimeState>().GetSnapshot();
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(
            profileBinding: new(profile.ActiveProfileId!.Value, profile.ActiveFingerprint!, new(profile.Generation)),
            managedHr: true, includeRecoveryInput: true,
            configureAgent: agent => agent with {
                ConfigurationJson = AgentThinkingEffortPolicy.WriteAgentOverride(agent.ConfigurationJson, null)
            });
        var authority = new HistoricalSettingsAuthority(fixture);

        var initialClient = new HistoricalSettingsClient(expected, update);
        var initial = await ExecuteHistoricalSettingsAsync(fixture, services, authority, initialClient);
        Assert.Equal(HrSimpleChatToolPolicy.HrSimpleChatSettingsGet, Assert.Single(initial.PendingApprovals).ToolName);
        Assert.Equal(1, initialClient.Requests);
        Assert.Empty(initialClient.Results);
        await fixture.ApproveAsync(initial.PendingApprovals);

        var readClient = new HistoricalSettingsClient(expected, update);
        var read = await ExecuteHistoricalSettingsAsync(fixture, services, authority, readClient);
        Assert.Equal(HrSimpleChatToolPolicy.HrSimpleChatSettingsUpdate, Assert.Single(read.PendingApprovals).ToolName);
        Assert.Equal(1, readClient.Requests);
        RequireOriginalSettings(readClient, expected, command);
        var beforeUpdate = await ReadHistoricalRunAsync(fixture);
        var originalRead = Assert.Single(beforeUpdate.Batches.SelectMany(batch => batch.Proposals),
            proposal => proposal.Payload.ToolName == HrSimpleChatToolPolicy.HrSimpleChatSettingsGet);
        Assert.Equal(AgentToolProposalState.Completed, originalRead.State);
        Assert.Equal(ExecutionApprovalStatus.Approved, originalRead.ApprovalStatus);
        Assert.Equal(originalRead.Payload.Digest, originalRead.ApprovedDigest);
        var proposedUpdate = Assert.Single(beforeUpdate.Batches.SelectMany(batch => batch.Proposals),
            proposal => proposal.Payload.ToolName == HrSimpleChatToolPolicy.HrSimpleChatSettingsUpdate);
        Assert.Equal(ExecutionApprovalStatus.Pending, proposedUpdate.ApprovalStatus);
        Assert.Equal(1, (await definitions.GetAsync(first.Definition.Id)).Value!.Revision.Revision.Value);
        await fixture.ApproveAsync(read.PendingApprovals);

        var updateClient = new HistoricalSettingsClient(expected, update, stopAfterUpdate: true);
        var stopped = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteHistoricalSettingsAsync(fixture, services, authority, updateClient));
        AgentToolAdmissionJournalFixture.RequireScriptedFault(stopped, HistoricalSettingsClient.StopMessage);
        Assert.Equal(1, updateClient.Requests);
        RequireOriginalSettings(updateClient, expected, command);
        var afterUpdate = await ReadHistoricalRunAsync(fixture);
        var committedUpdate = Assert.Single(afterUpdate.Batches.SelectMany(batch => batch.Proposals),
            proposal => proposal.Payload.ToolName == HrSimpleChatToolPolicy.HrSimpleChatSettingsUpdate);
        Assert.Equal(AgentToolProposalState.Completed, committedUpdate.State);
        Assert.Equal(AgentToolEffectState.Committed, committedUpdate.EffectState);
        Assert.Equal(ExecutionApprovalStatus.Approved, committedUpdate.ApprovalStatus);
        Assert.Equal(committedUpdate.Payload.Digest, committedUpdate.ApprovedDigest);
        Assert.Equal(proposedUpdate.IntentId, committedUpdate.IntentId);
        Assert.Equal(originalRead, Assert.Single(afterUpdate.Batches.SelectMany(batch => batch.Proposals),
            proposal => proposal.IntentId == originalRead.IntentId));
        var current = (await definitions.GetAsync(first.Definition.Id)).Value!;
        Assert.Equal(2, current.Revision.Revision.Value);
        Assert.Equal(expected.ConcurrencyToken + 1, current.Definition.ConcurrencyToken);
        Assert.Equal(update.Definition.SystemPrompt, current.Revision.SystemPrompt);
        var historical = await definitions.GetRevisionAsync(first.Definition.Id, new(1));
        Assert.True(historical.IsSuccess);
        Assert.Equal(command.SystemPrompt, historical.Value!.SystemPrompt);
        var callsBeforeReplay = resolver.Calls;
        resolver.Reject = true;

        var store = fixture.NewStore();
        var settingsKey = HrSimpleChatToolPolicy.Get(HrSimpleChatOperation.Settings).CapabilityKey;
        var originalAssignment = fixture.Agent.Capabilities.Single(assignment => assignment.CapabilityKey == settingsKey);
        if (change == HistoricalReadChange.ReadRevoked) {
            authority.ReadAllowed = false;
        } else if (change == HistoricalReadChange.SettingsCapabilityRevoked) {
            await store.UpdateCatalogAsync(catalog => catalog with {
                Agents = catalog.Agents.Select(agent => agent.Id == fixture.Agent.Id ? agent with {
                    ConfigurationJson = AgentManagedSeedCustomizationMetadata.MarkCustomized(agent.ConfigurationJson),
                    Capabilities = agent.Capabilities.Where(assignment => assignment.CapabilityKey != settingsKey).ToArray()
                } : agent).ToArray()
            });
            Assert.DoesNotContain((await store.LoadCatalogSnapshotAsync()).Catalog.Agents.Single(agent => agent.Id == fixture.Agent.Id)
                .Capabilities, assignment => assignment.CapabilityKey == settingsKey);
        } else if (change is HistoricalReadChange.OriginalRevisionRemoved or HistoricalReadChange.DefinitionRemoved) {
            await using var owner = await services.GetRequiredService<IDbContextFactory<SimpleChatsDbContext>>().CreateDbContextAsync();
            if (change == HistoricalReadChange.OriginalRevisionRemoved) {
                Assert.Equal(1, await owner.Set<LlmChatDefinitionRevisionRow>()
                    .Where(row => row.DefinitionId == first.Definition.Id.Value && row.Revision == 1).ExecuteDeleteAsync());
            } else {
                Assert.Equal(2, await owner.Set<LlmChatDefinitionRevisionRow>()
                    .Where(row => row.DefinitionId == first.Definition.Id.Value).ExecuteDeleteAsync());
                Assert.Equal(1, await owner.Set<LlmChatDefinitionRow>().Where(row => row.Id == first.Definition.Id.Value).ExecuteDeleteAsync());
            }
        }

        if (change != HistoricalReadChange.None) {
            var deniedClient = new HistoricalSettingsClient(expected, update);
            Exception denial;
            if (change == HistoricalReadChange.ProfileChanged) {
                await using var otherDatabase = ReceiptDatabase.Create("simple-chat-historical-other-profile");
                await using var otherApplication = await otherDatabase.OpenAsync(new Resolver());
                await using var otherScope = otherApplication.Services.CreateAsyncScope();
                var otherProfile = otherScope.ServiceProvider.GetRequiredService<IDatabaseRuntimeState>().GetSnapshot();
                Assert.NotEqual(profile.ActiveProfileId, otherProfile.ActiveProfileId);
                denial = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteHistoricalSettingsAsync(fixture, otherScope.ServiceProvider, authority, deniedClient));
            } else {
                denial = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteHistoricalSettingsAsync(fixture, services, authority, deniedClient));
            }
            RequireHistoricalFailure(denial, change switch {
                HistoricalReadChange.OriginalRevisionRemoved => LlmChatErrorCodes.StorageCorrupted,
                HistoricalReadChange.DefinitionRemoved => LlmChatErrorCodes.DefinitionNotFound,
                _ => "hr-simple-chat.authorization-denied"
            });
            Assert.Equal(0, deniedClient.Requests);
            Assert.Empty(deniedClient.Results);
            var retained = await ReadHistoricalRunAsync(fixture);
            Assert.Equal(originalRead, Assert.Single(retained.Batches.SelectMany(batch => batch.Proposals), proposal => proposal.IntentId == originalRead.IntentId));
            Assert.Equal(committedUpdate, Assert.Single(retained.Batches.SelectMany(batch => batch.Proposals), proposal => proposal.IntentId == committedUpdate.IntentId));
            if (change is HistoricalReadChange.OriginalRevisionRemoved or HistoricalReadChange.DefinitionRemoved) {
                Assert.True((await definitions.GetRevisionAsync(first.Definition.Id, new(1))).IsFailure);
                Assert.Equal(callsBeforeReplay, resolver.Calls);
                return;
            }
            authority.ReadAllowed = true;
            if (change == HistoricalReadChange.SettingsCapabilityRevoked) {
                await store.UpdateCatalogAsync(catalog => catalog with {
                    Agents = catalog.Agents.Select(agent => agent.Id == fixture.Agent.Id ? agent with {
                        Capabilities = [.. agent.Capabilities, originalAssignment]
                    } : agent).ToArray()
                });
            }
        }

        var resumedClient = new HistoricalSettingsClient(expected, update);
        var resumed = await ExecuteHistoricalSettingsAsync(fixture, services, authority, resumedClient);
        Assert.Equal("completed", resumed.ResponseText);
        Assert.Empty(resumed.PendingApprovals);
        Assert.Equal(1, resumedClient.Requests);
        RequireOriginalSettings(resumedClient, expected, command);
        var replayClient = new HistoricalSettingsClient(expected, update);
        Assert.Equal("completed", (await ExecuteHistoricalSettingsAsync(fixture, services, authority, replayClient)).ResponseText);
        Assert.Equal(0, replayClient.Requests);
        Assert.Equal(callsBeforeReplay, resolver.Calls);
        var final = (await definitions.GetAsync(first.Definition.Id)).Value!;
        Assert.Equal(current.Definition.ConcurrencyToken, final.Definition.ConcurrencyToken);
        Assert.Equal(2, final.Revision.Revision.Value);
        Assert.Equal(update.Definition.SystemPrompt, final.Revision.SystemPrompt);
        var saved = await ReadHistoricalRunAsync(fixture);
        Assert.Equal(2, saved.Batches.SelectMany(batch => batch.Proposals).Count());
        Assert.Equal(originalRead, Assert.Single(saved.Batches.SelectMany(batch => batch.Proposals), proposal => proposal.IntentId == originalRead.IntentId));
        Assert.Equal(committedUpdate, Assert.Single(saved.Batches.SelectMany(batch => batch.Proposals), proposal => proposal.IntentId == committedUpdate.IntentId));
        await AssertCountsAsync(application, definitions: 1, receipts: 0, revisions: 2);
    }

    public enum HistoricalReadChange { None, ReadRevoked, SettingsCapabilityRevoked, ProfileChanged, OriginalRevisionRemoved, DefinitionRemoved }

    private static void RequireHistoricalFailure(Exception failure, string expectedCode) {
        for (Exception? current = failure; current is not null; current = current.InnerException) {
            if (current is HrSimpleChatAdministrationException ownerFailure && ownerFailure.Code == expectedCode) {
                return;
            }
        }
        throw new InvalidOperationException("The saved-settings fixture did not reach the expected owner disclosure denial.", failure);
    }

    private static void RequireOriginalSettings(HistoricalSettingsClient client, HrSimpleChatDefinitionVersion expected,
        CreateLlmChatDefinitionCommand command) {
        var result = Assert.Single(client.Results, result => result.CallId == HistoricalSettingsClient.ReadCallId);
        var json = JsonSerializer.SerializeToElement(result.Result, HrSimpleChatProposalCodec.SerializerOptions);
        var settings = json.Deserialize<HrSimpleChatDefinitionSettings>(HrSimpleChatProposalCodec.SerializerOptions)!;
        Assert.Equal(expected, settings.Summary.Version);
        Assert.Equal(command.Name, settings.Summary.Name);
        Assert.Equal(command.Tags!.ToArray(), settings.Summary.Tags.ToArray());
        Assert.Equal(command.SystemPrompt, settings.Settings.SystemPrompt);
        Assert.Equal(command.Settings, settings.Settings.Settings);
        Assert.DoesNotContain("R2 prompt only", JsonSerializer.Serialize(client.Results, HrSimpleChatProposalCodec.SerializerOptions), StringComparison.Ordinal);
    }

    private static async Task<AgentToolJournalRecord> ReadHistoricalRunAsync(AgentToolAdmissionJournalFixture fixture)
        => (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;

    private static async Task<AgentRuntimeResponse> ExecuteHistoricalSettingsAsync(AgentToolAdmissionJournalFixture fixture,
        IServiceProvider services, HistoricalSettingsAuthority authority, HistoricalSettingsClient client) {
        var store = fixture.NewStore();
        var journal = fixture.NewJournal(store);
        var codec = new HrSimpleChatProposalCodec();
        var administration = new HrSimpleChatAdministration(
            services.GetRequiredService<ILlmChatDefinitionApplicationService>(),
            services.GetRequiredService<ILlmChatDefinitionCreateReceiptService>(),
            services.GetRequiredService<ILlmChatProviderResolver>(),
            services.GetRequiredService<ILlmChatRuntimeLeaseFactory>(),
            services.GetRequiredService<ILlmChatOperationScopeAccessor>(),
            new HrSimpleChatRuntimeAuthorization(store, store, journal, authority), codec);
        var provider = new HrSimpleChatRuntimeToolProvider(administration);
        var policies = new AgentToolPolicyCatalog(HrSimpleChatToolPolicy.Capabilities);
        var runtimeProvider = fixture.Provider with { Kind = ProviderKind.OpenAi, Transport = ProviderTransportKind.Responses };
        var model = ManagedSeedProviderFallbacks.ResolveModel(fixture.Agent, runtimeProvider);
        runtimeProvider = runtimeProvider with {
            ConfigurationJson = ProviderModelThinkingConfiguration.Write(runtimeProvider.ConfigurationJson, model,
                new(model, AgentThinkingEffortSupportStatus.Supported, AgentThinkingEffortControlMode.EffortLevels,
                    [AgentReasoningEffortLevel.Medium], AgentReasoningEffortLevel.Medium))
        };
        Assert.Equal(AgentReasoningEffortLevel.Medium,
            AgentThinkingEffortPolicy.ResolveEffectiveEffort(runtimeProvider, model, fixture.Agent.ConfigurationJson));
        var dependencies = MafAgentRuntimeDependencies.FromServices(services);
        dependencies = dependencies with {
            ProviderAgentFactory = new HistoricalSettingsAgentFactory(client, model),
            RuntimeToolProviderComposer = new RuntimeToolProviderComposer(new RuntimeToolProviderAccessFilter(policies), policies),
            ToolPolicies = policies, ToolAdmissionJournal = journal,
            CapabilityDependencies = dependencies.CapabilityDependencies with {
                RuntimeToolProviders = [provider], ContextContributors = [], ToolPolicies = policies
            }
        };
        var runtime = new MafAgentRuntime(fixture.WorkspaceRoot, WorkspaceScopeDescriptor.Sandbox, dependencies);
        var options = new AgentRuntimeExecutionOptions(null, AgentFinalizerMode.Disabled, true, 0) {
            AdmittedToolSession = fixture.Session, ToolAdmissionSupport = AgentToolAdmissionSupport.Recoverable,
            Governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(fixture.Detail.Run.MetadataJson),
            AuthorityPolicyFingerprint = "fixture-authority", ModelContextDigest = "fixture-context",
            ContextIntent = AgentRuntimeContextIntent.Empty with {
                Purpose = AgentRuntimeContextPurpose.InteractiveChat, RuntimeToolProvidersEnabled = true,
                WorkspaceToolsEnabled = false, ToolCapabilitiesEnabled = true
            }
        };
        var attachedCapabilityIds = fixture.Agent.Capabilities.Select(item => item.CapabilityId).ToHashSet();
        var capabilities = (await fixture.Store.LoadCatalogSnapshotAsync()).Catalog.Capabilities
            .Where(item => attachedCapabilityIds.Contains(item.Id))
            .Where(item => !AgentCapabilityRequirementEvaluator.IsRetiredCapability(item))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return await runtime.ExecutionPort.ExecuteAsync(new(fixture.Agent,
            runtimeProvider,
            fixture.Detail.ChatSession!, capabilities, [], fixture.Detail.Run.ToolAdmission!.OriginalInput!.Content, string.Empty,
            (_, _, _) => Task.CompletedTask, ExecutionOptions: options));
    }

    private sealed class HistoricalSettingsAuthority(AgentToolAdmissionJournalFixture fixture) : IAgentExecutionAuthorityResolver {
        public bool ReadAllowed { get; set; } = true;

        public ValueTask<AgentExecutionAuthorityRecord> ResolveAsync(AgentExecutionAuthorityResolutionRequest request,
            CancellationToken cancellationToken = default) {
            var original = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(fixture.Detail.Run.MetadataJson)!;
            var source = AgentTurnContextMetadata.TryReadTurnContextReference(fixture.Detail.Run.MetadataJson)!;
            Assert.Equal(source.SourceKind, request.SourceKind);
            Assert.Equal(source.SourceId, request.SourceId);
            Assert.Equal(original.WorkspaceScope, request.ObservedWorkspaceScope);
            return ValueTask.FromResult(new AgentExecutionAuthorityRecord(AgentExecutionAuthorityId.Create(), fixture.Agent.Id,
                fixture.Profile.ProfileId, fixture.Profile.Generation, original.WorkspaceScope, ReadAllowed, ReadAllowed,
                "current-settings-policy", "current-settings-authority", DateTimeOffset.UtcNow,
                allowedCapabilityKeys: HrSimpleChatToolPolicy.Operations.Select(operation => operation.CapabilityKey).ToArray()));
        }
    }

    private sealed class HistoricalSettingsAgentFactory(HistoricalSettingsClient client, string expectedModel) : IMafProviderAgentFactory {
        public AIAgent CreateFrameworkAgent(ProviderProfile provider, string model, ChatClientAgentOptions options,
            bool frameworkManagedHistory, bool allowBackgroundResponses) {
            Assert.True(frameworkManagedHistory);
            Assert.NotNull(options.ChatHistoryProvider);
            Assert.Equal(ProviderKind.OpenAi, provider.Kind);
            Assert.Equal(ProviderTransportKind.Responses, provider.Transport);
            Assert.Equal(expectedModel, model);
            Assert.Equal(AgentThinkingEffortCapabilitySource.Configured, AgentThinkingEffortPolicy.ResolveCapability(provider, model).Source);
            Assert.Equal(ReasoningEffort.Medium, options.ChatOptions!.Reasoning!.Effort);
            Assert.IsType<ApprovalRequiredAIFunction>(Assert.Single(options.ChatOptions.Tools!, tool => tool.Name == HrSimpleChatToolPolicy.HrSimpleChatSettingsGet));
            Assert.IsType<ApprovalRequiredAIFunction>(Assert.Single(options.ChatOptions.Tools!, tool => tool.Name == HrSimpleChatToolPolicy.HrSimpleChatSettingsUpdate));
            return new ChatClientAgent(new MafToolAdmissionChatClient(client), options);
        }
    }

    private sealed class HistoricalSettingsClient(HrSimpleChatDefinitionVersion expected, HrSimpleChatUpdateRequest update,
        bool stopAfterUpdate = false) : IChatClient {
        public const string ReadCallId = "read-original-settings";
        private const string UpdateCallId = "update-original-settings";
        public const string StopMessage = "Stop after the acknowledged Simple Chat update before the final provider response.";
        public int Requests { get; private set; }
        public List<FunctionResultContent> Results { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) {
            Requests++;
            var results = messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().ToArray();
            Results.AddRange(results);
            if (results.Any(result => result.CallId == UpdateCallId)) {
                var acknowledged = JsonSerializer.SerializeToElement(
                    Assert.Single(results, result => result.CallId == UpdateCallId).Result,
                    HrSimpleChatProposalCodec.SerializerOptions);
                if (acknowledged.ValueKind == JsonValueKind.Object && acknowledged.TryGetProperty("errorCode", out var errorCode)) {
                    Assert.Fail($"The approved update returned failure code '{errorCode.GetString()}' before its owner acknowledgement.");
                }
                var summary = acknowledged.Deserialize<HrSimpleChatDefinitionSummary>(HrSimpleChatProposalCodec.SerializerOptions)!;
                Assert.Equal(new HrSimpleChatDefinitionVersion(expected.DefinitionId, expected.Revision + 1,
                    expected.ConcurrencyToken + 1), summary.Version);
                if (stopAfterUpdate) {
                    throw new IOException(StopMessage);
                }
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "completed")));
            }
            var updateRequest = JsonSerializer.SerializeToElement(update, HrSimpleChatProposalCodec.SerializerOptions);
            Assert.Equal("medium", updateRequest.GetProperty("definition").GetProperty("settings").GetProperty("thinkingEffort").GetString());
            var call = results.Any(result => result.CallId == ReadCallId)
                ? new FunctionCallContent(UpdateCallId, HrSimpleChatToolPolicy.HrSimpleChatSettingsUpdate,
                    new Dictionary<string, object?> { ["request"] = updateRequest })
                : new FunctionCallContent(ReadCallId, HrSimpleChatToolPolicy.HrSimpleChatSettingsGet,
                    new Dictionary<string, object?> { ["request"] = JsonSerializer.SerializeToElement(expected, HrSimpleChatProposalCodec.SerializerOptions) });
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, [call])));
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
