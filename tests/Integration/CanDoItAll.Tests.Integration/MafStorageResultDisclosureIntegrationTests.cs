using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Agents.Storage;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel.Streaming;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class MafStorageResultDisclosureIntegrationTests {
    private const string CatalogCallId = "original-storage-catalog";
    private const string ReadCallId = "diagnostic-storage-read";

    [Fact]
    public async Task Core_recovers_original_invalid_Storage_browse_after_current_authority_revalidation_without_driver_dispatch() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var profile = services.GetRequiredService<IDatabaseRuntimeState>().GetSnapshot();
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true,
            profileBinding: new(profile.ActiveProfileId!.Value, profile.ActiveFingerprint!, new(profile.Generation)));
        var catalogs = services.GetRequiredService<IStorageCatalogService>();
        var target = await catalogs.SaveAsync(Storage("Original browse recovery catalog", fixture.WorkspaceRoot));
        // The seeded template agent carries catalog capabilities (a local MCP server) that this Core recovery
        // path would try to compose; the Storage tools under test come from the runtime tool provider instead.
        var agent = await SaveCanonicalAgentAsync(services, fixture.Agent with {
            Permissions = fixture.Agent.Permissions with { CanUseTools = true },
            ConfigurationJson = Configuration(fixture.Agent.ConfigurationJson, [target.Id]),
            Capabilities = []
        });
        await fixture.Store.UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Where(item => item.Id != agent.Id).Append(agent).ToArray()
        });
        var arguments = new Dictionary<string, object?> { ["storageId"] = target.Id, ["pageSize"] = 200 };
        var driver = new CountingBrowseDriver(services.GetRequiredService<IStorageBrowseDriverRegistry>().Resolve(StorageProviderKind.FileSystem));
        var provider = CreateProvider(services, new StorageBrowseDriverRegistry([driver]));
        var legacyFailure = new UntypedBrowseFailureProvider(provider);
        var firstClient = new StorageScriptClient(StorageToolPolicy.StorageBrowse, arguments, catalogFirst: true);
        Exception originalFailure;
        using (var initial = new StorageRecoveryExecution(fixture, services, firstClient, legacyFailure)) {
            originalFailure = await Assert.ThrowsAnyAsync<Exception>(() => initial.Service.SendMessageAsync(agent.Id, null, "List the catalog, then browse a page.",
                new(AgentExecutionOperationId.New(), WorkspaceToolsEnabled: true) {
                    Context = ExecutionInvocationContext.Empty with {
                        SourceKind = "agents", SourceId = "agents", MetadataJson = fixture.Detail.Run.MetadataJson,
                        Policy = new(FinalizerMode: AgentFinalizerMode.Disabled)
                    }
                }));
        }
        Assert.True(legacyFailure.ObservedRequestRejection,
            $"The original run must fail on the untyped browse rejection after {firstClient.Requests} provider request(s); actual failure: {originalFailure}");
        Assert.Equal(2, firstClient.Requests);
        Assert.Equal(0, driver.Calls);
        var original = Assert.Single(await fixture.NewStore().ListExecutionRunsAsync(), run => run.Id != fixture.Detail.Run.Id);
        var originalDetail = (await fixture.NewStore().GetExecutionRunDetailAsync(original.Id))!;
        Assert.Equal(ExecutionState.Failed, original.State);
        Assert.True(original.ToolAdmission!.HasUnresolvedEffects);
        Assert.Empty(original.PendingApprovals);
        var originalProposals = original.ToolAdmission.Batches.SelectMany(batch => batch.Proposals).ToArray();
        var completed = Assert.Single(originalProposals, proposal => proposal.CallId == CatalogCallId);
        var uncertain = Assert.Single(originalProposals, proposal => proposal.CallId == ReadCallId);
        Assert.Equal(AgentToolProposalState.Completed, completed.State);
        Assert.Equal(AgentToolProposalState.ReconciliationRequired, uncertain.State);
        Assert.Equal(AgentToolEffectState.Unknown, uncertain.EffectState);
        Assert.Equal(AgentToolProposalRecovery.RevalidateAndRead, uncertain.Payload.Recovery);
        Assert.Null(uncertain.Result);
        var catalogRows = await CatalogRecordsAsync(services);

        await SaveCanonicalAgentAsync(services, agent with { ConfigurationJson = Configuration(agent.ConfigurationJson, []) });
        var deniedClient = new StorageScriptClient(StorageToolPolicy.StorageBrowse, arguments, catalogFirst: true);
        using (var denied = new StorageRecoveryExecution(fixture, services, deniedClient, provider)) {
            var failure = await Assert.ThrowsAnyAsync<Exception>(() => denied.Service.RecoverExecutionRunAsync(original.Id, AgentExecutionOperationId.New()));
            Assert.Contains("storage.result-disclosure-denied", Codes(failure));
        }
        Assert.Equal(0, deniedClient.Requests);
        Assert.Equal(0, driver.Calls);
        var deniedRun = (await fixture.NewStore().GetExecutionRunAsync(original.Id))!;
        var deniedAdmission = deniedRun.ToolAdmission!;
        Assert.Equal(original.ToolAdmission.Revision + 1, deniedAdmission.Revision);
        Assert.NotNull(deniedAdmission.ActiveDispatchLeaseId);
        Assert.NotEqual(Guid.Empty, deniedAdmission.ActiveDispatchLeaseId);
        Assert.NotEqual(original.ToolAdmission.ActiveDispatchLeaseId, deniedAdmission.ActiveDispatchLeaseId);
        Assert.Equal(JsonSerializer.Serialize(original.ToolAdmission with {
            Revision = deniedAdmission.Revision, ActiveDispatchLeaseId = deniedAdmission.ActiveDispatchLeaseId
        }), JsonSerializer.Serialize(deniedAdmission));

        await SaveCanonicalAgentAsync(services, agent);
        var recoveryClient = new StorageScriptClient(StorageToolPolicy.StorageBrowse, arguments, catalogFirst: true);
        using (var restarted = new StorageRecoveryExecution(fixture, services, recoveryClient, provider)) {
            var recovered = await restarted.Service.RecoverExecutionRunAsync(original.Id, AgentExecutionOperationId.New());
            Assert.Equal(original.Id, recovered.ExecutionRunId);
            Assert.Equal(ExecutionState.Completed, recovered.State);
        }
        Assert.Equal(1, recoveryClient.Requests);
        Assert.Equal(AgentToolInputValidationException.FailureCode, recoveryClient.ReadFailureCode);
        Assert.Equal(0, driver.Calls);
        var saved = (await fixture.NewStore().GetExecutionRunDetailAsync(original.Id))!;
        Assert.Equal(original.ChatSessionId, saved.Run.ChatSessionId);
        Assert.Equal(original.ToolAdmission.OriginalInput, saved.Run.ToolAdmission!.OriginalInput);
        Assert.Equal(original.ToolAdmission.RuntimeContext, saved.Run.ToolAdmission.RuntimeContext);
        Assert.Equal(original.ToolAdmission.Session, saved.Run.ToolAdmission.Session);
        Assert.Equal(JsonSerializer.Serialize(original.ToolAdmission.Segments), JsonSerializer.Serialize(saved.Run.ToolAdmission.Segments));
        Assert.False(saved.Run.ToolAdmission.HasUnresolvedEffects);
        Assert.Empty(saved.Run.PendingApprovals);
        Assert.Equal(originalDetail.Approvals, saved.Approvals);
        var proposals = saved.Run.ToolAdmission.Batches.SelectMany(batch => batch.Proposals).ToArray();
        Assert.Equal(originalProposals.Select(item => item.IntentId), proposals.Select(item => item.IntentId));
        Assert.Equal(completed, Assert.Single(proposals, proposal => proposal.CallId == CatalogCallId));
        var read = Assert.Single(proposals, proposal => proposal.CallId == ReadCallId);
        Assert.Equal(AgentToolProposalState.Completed, read.State);
        Assert.Equal(AgentToolEffectState.None, read.EffectState);
        Assert.NotNull(read.Result);
        Assert.Equal(uncertain with {
            State = read.State, EffectState = read.EffectState, Result = read.Result, DispatchClaimId = read.DispatchClaimId
        }, read);
        foreach (var receipt in originalDetail.ToolReceipts) {
            Assert.Equal(receipt, Assert.Single(saved.ToolReceipts, item => item.Id == receipt.Id));
        }
        var originalReceiptIds = originalDetail.ToolReceipts.Select(item => item.Id).ToHashSet();
        var failureReceipt = Assert.Single(saved.ToolReceipts,
            item => item.ToolName == StorageToolPolicy.StorageBrowse && !originalReceiptIds.Contains(item.Id));
        Assert.Equal(AgentToolInvocationOutcome.Failed, failureReceipt.InvocationOutcome);
        Assert.Equal(AgentToolEffectState.None, failureReceipt.EffectState);
        Assert.Equal(AgentToolInputValidationException.FailureCode, failureReceipt.FailureCode);
        await AssertCatalogUnchangedExceptRootValidationAsync(catalogRows, services);

        var finalClient = new StorageScriptClient(StorageToolPolicy.StorageBrowse, arguments, catalogFirst: true);
        using (var replay = new StorageRecoveryExecution(fixture, services, finalClient, provider)) {
            var result = await replay.Service.RecoverExecutionRunAsync(original.Id, AgentExecutionOperationId.New());
            Assert.Equal(original.Id, result.ExecutionRunId);
            Assert.Equal(ExecutionState.Completed, result.State);
        }
        Assert.Equal(0, finalClient.Requests);
        Assert.Equal(0, driver.Calls);
        var final = (await fixture.NewStore().GetExecutionRunDetailAsync(original.Id))!;
        Assert.Equal(JsonSerializer.Serialize(saved.Run.ToolAdmission), JsonSerializer.Serialize(final.Run.ToolAdmission));
        Assert.Equal(saved.ToolReceipts, final.ToolReceipts);
        Assert.Equal(saved.Approvals, final.Approvals);
        await AssertCatalogUnchangedExceptRootValidationAsync(catalogRows, services);
        Assert.Equal(2, (await fixture.NewStore().ListExecutionRunsAsync()).Count);
    }

    [Theory]
    [InlineData(StorageToolPolicy.StorageReadTextFile, false)]
    [InlineData(StorageToolPolicy.StorageReadTextFile, true)]
    [InlineData(StorageToolPolicy.StorageCatalogList, false)]
    [InlineData(StorageToolPolicy.StorageCatalogList, true)]
    public async Task Long_lived_MAF_Storage_attachment_rechecks_canonical_grants_without_recomposing_saved_tools(
        string toolName, bool revokeAllowedCatalog) {
        await using var application = await TestApplication.CreateAsync();
        await using var services = application.Services.CreateAsyncScope();
        var profile = services.ServiceProvider.GetRequiredService<IDatabaseRuntimeState>().GetSnapshot();
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(
            profileBinding: new(profile.ActiveProfileId!.Value, profile.ActiveFingerprint!, new(profile.Generation)));
        var catalogs = services.ServiceProvider.GetRequiredService<IStorageCatalogService>();
        var target = await catalogs.SaveAsync(Storage("Private retained catalog", fixture.WorkspaceRoot));
        var other = await catalogs.SaveAsync(Storage("Other allowed catalog", Path.Combine(fixture.WorkspaceRoot, "other")));
        var file = Path.Combine(target.EndpointOrRoot, "private.txt");
        await File.WriteAllTextAsync(file, "original private text");
        var agent = fixture.Agent with {
            IsTemplate = false, Status = AgentLifecycleStatus.Active,
            Permissions = fixture.Agent.Permissions with { CanUseTools = true },
            ConfigurationJson = Configuration(fixture.Agent.ConfigurationJson, [target.Id, other.Id])
        };
        agent = await SaveCanonicalAgentAsync(services.ServiceProvider, agent);
        var arguments = toolName == StorageToolPolicy.StorageReadTextFile
            ? new Dictionary<string, object?> { ["storageId"] = target.Id, ["locator"] = "private.txt" }
            : new Dictionary<string, object?>();
        var first = new StorageScriptClient(toolName, arguments, failAfterResult: true);
        var (execute, retained) = CreateExecution(fixture, services.ServiceProvider, agent, first);
        var stopped = await Assert.ThrowsAnyAsync<Exception>(execute);
        AgentToolAdmissionJournalFixture.RequireScriptedFault(stopped, "Fixture stops after the durable tool result and before the final provider response.");
        Assert.Equal(2, first.Requests);
        var saved = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
        var proposal = Assert.Single(Assert.Single(saved.Batches).Proposals);
        Assert.Equal(AgentToolProposalState.Completed, proposal.State);
        Assert.Equal(AgentToolProposalRecovery.RevalidateAndRead, proposal.Payload.Recovery);
        var checkpoint = proposal.Result;
        Assert.NotNull(checkpoint);
        Assert.Contains(toolName == StorageToolPolicy.StorageReadTextFile ? "original private text" : target.Name,
            checkpoint.PayloadJson, StringComparison.Ordinal);
        File.Delete(file);

        var currentAgent = agent;
        if (revokeAllowedCatalog) {
            currentAgent = await SaveCanonicalAgentAsync(services.ServiceProvider,
                agent with { ConfigurationJson = Configuration(agent.ConfigurationJson, [other.Id]) });
            Assert.DoesNotContain(target.Id, AgentWorkspaceToolAccessMetadata.Read(currentAgent.ConfigurationJson).AllowedStorageCatalogIds);
        } else {
            await catalogs.SaveAsync(StorageCatalogSaveRequest.FromSnapshot(target) with { IsEnabled = false });
        }
        var beforeDenied = await CatalogRowsAsync(services.ServiceProvider);
        first.Reset();
        var failure = await Assert.ThrowsAnyAsync<Exception>(execute);
        Assert.Contains("storage.result-disclosure-denied", Codes(failure));
        Assert.Equal(0, first.Requests);
        Assert.Equal(beforeDenied, await CatalogRowsAsync(services.ServiceProvider));
        var deniedState = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
        Assert.Equal(checkpoint, Assert.Single(Assert.Single(deniedState.Batches).Proposals).Result);

        if (!revokeAllowedCatalog) {
            await catalogs.SaveAsync(StorageCatalogSaveRequest.FromSnapshot(target) with { IsEnabled = true });
        }
        await SaveCanonicalAgentAsync(services.ServiceProvider, agent);
        first.Reset();
        var restored = await execute();
        Assert.Contains("completed", restored.ResponseText, StringComparison.Ordinal);
        Assert.Equal(1, first.Requests);
        Assert.Contains(toolName == StorageToolPolicy.StorageReadTextFile ? "original private text" : target.Name,
            Assert.Single(first.Inputs), StringComparison.Ordinal);
        Assert.False(File.Exists(file));
        Assert.Equal(1, retained.CompositionCount);
        Assert.Equal(agent.Id, retained.OriginalContext!.Agent.Id);
        Assert.Equal(agent.ConfigurationJson, retained.OriginalContext.Agent.ConfigurationJson);
        var recovered = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
        Assert.Equal(proposal.IntentId, Assert.Single(recovered.Batches[0].Proposals).IntentId);
        Assert.Equal(checkpoint, recovered.Batches[0].Proposals[0].Result);
    }

    private static async Task<AgentDefinition> SaveCanonicalAgentAsync(IServiceProvider services, AgentDefinition agent) {
        var customized = agent with {
            ConfigurationJson = AgentManagedSeedCustomizationMetadata.MarkCustomized(agent.ConfigurationJson)
        };
        Assert.True(AgentManagedSeedCustomizationMetadata.HasCurrentCustomization(customized.ConfigurationJson));
        var store = services.GetRequiredService<ISandboxWorkspaceCatalogStore>();
        await store.UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Where(item => item.Id != customized.Id).Append(customized).ToArray()
        });
        var saved = (await store.LoadCatalogSnapshotAsync()).Catalog.Agents.Single(item => item.Id == customized.Id);
        Assert.True(AgentManagedSeedCustomizationMetadata.HasCurrentCustomization(saved.ConfigurationJson));
        var expected = AgentWorkspaceToolAccessMetadata.Read(customized.ConfigurationJson);
        var actual = AgentWorkspaceToolAccessMetadata.Read(saved.ConfigurationJson);
        Assert.Equal(expected.CanReadStorage, actual.CanReadStorage);
        Assert.Equal(expected.AllowedStorageCatalogIds, actual.AllowedStorageCatalogIds);
        return saved;
    }

    private static string Configuration(string configurationJson, List<Guid> allowed)
        => AgentWorkspaceToolAccessMetadata.Write(AgentThinkingEffortPolicy.WriteAgentOverride(configurationJson, null), new() {
            CanReadFiles = false, CanReadStorage = true, AllowedStorageCatalogIds = allowed
        });

    private static StorageCatalogSaveRequest Storage(string name, string root) => new() {
        Name = name, ProviderKind = StorageProviderKind.FileSystem, IsEnabled = true,
        ConnectionMode = StorageConnectionMode.Local, EndpointOrRoot = root,
        CapabilityMask = StorageCapability.Read | StorageCapability.Write | StorageCapability.Delete,
        HealthStatus = StorageHealthStatus.Healthy
    };

    private static async Task<StorageCatalogRecord[]> CatalogRecordsAsync(IServiceProvider services) {
        var factory = services.GetRequiredService<IDbContextFactory<StorageDbContext>>();
        await using var database = await factory.CreateDbContextAsync();
        return await database.Set<StorageCatalogRecord>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync();
    }

    private static async Task<string> CatalogRowsAsync(IServiceProvider services)
        => JsonSerializer.Serialize(await CatalogRecordsAsync(services));

    // Current storage resolution re-validates the host-bound root, which saves the row and advances
    // RootLastValidatedAtUtc and UpdatedAtUtc. Every other catalog field must stay byte-identical and
    // neither timestamp may move backwards; the catalog row set itself must not change.
    private static async Task AssertCatalogUnchangedExceptRootValidationAsync(StorageCatalogRecord[] before, IServiceProvider services) {
        var after = await CatalogRecordsAsync(services);
        Assert.Equal(before.Length, after.Length);
        for (var index = 0; index < before.Length; index++) {
            Assert.Equal(before[index].Id, after[index].Id);
            Assert.True(before[index].RootLastValidatedAtUtc is null || after[index].RootLastValidatedAtUtc >= before[index].RootLastValidatedAtUtc,
                "The current root validation time must not move backwards.");
            Assert.True(after[index].UpdatedAtUtc >= before[index].UpdatedAtUtc, "The catalog row update time must not move backwards.");
            Assert.Equal(WithoutRootValidation(before[index]), WithoutRootValidation(after[index]));
        }
    }

    private static string WithoutRootValidation(StorageCatalogRecord row) {
        var node = JsonSerializer.SerializeToNode(row)!.AsObject();
        node.Remove(nameof(StorageCatalogRecord.RootLastValidatedAtUtc));
        node.Remove(nameof(StorageCatalogRecord.UpdatedAtUtc));
        return node.ToJsonString();
    }

    private static IEnumerable<string> Codes(Exception exception) {
        if (exception is AgentToolAdmissionException admission) {
            yield return admission.Code;
        }
        if (exception.InnerException is { } inner) {
            foreach (var code in Codes(inner)) {
                yield return code;
            }
        }
    }

    private static (Func<Task<AgentRuntimeResponse>> Execute, RetainedRuntimeToolProvider Provider) CreateExecution(AgentToolAdmissionJournalFixture fixture,
        IServiceProvider services, AgentDefinition agent, StorageScriptClient client) {
        var policies = new AgentToolPolicyCatalog(StorageToolPolicy.Capabilities);
        var provider = CreateProvider(services);
        var retained = new RetainedRuntimeToolProvider(provider);
        var dependencies = MafAgentRuntimeDependencies.FromServices(services);
        dependencies = dependencies with {
            ProviderAgentFactory = new ScriptAgentFactory(client),
            RuntimeToolProviderComposer = new RuntimeToolProviderComposer(new RuntimeToolProviderAccessFilter(policies), policies),
            ToolPolicies = policies,
            ToolAdmissionJournal = fixture.NewJournal(fixture.NewStore()),
            CapabilityDependencies = dependencies.CapabilityDependencies with {
                RuntimeToolProviders = [retained], ContextContributors = [], ToolPolicies = policies
            }
        };
        var runtime = new MafAgentRuntime(fixture.WorkspaceRoot, WorkspaceScopeDescriptor.Sandbox, dependencies);
        var options = new AgentRuntimeExecutionOptions(null, AgentFinalizerMode.Disabled, true, 0) {
            AdmittedToolSession = fixture.Session, ToolAdmissionSupport = AgentToolAdmissionSupport.Recoverable,
            Governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(fixture.Detail.Run.MetadataJson),
            AuthorityPolicyFingerprint = "fixture-authority", ModelContextDigest = "fixture-context",
            ContextIntent = AgentRuntimeContextIntent.Empty with {
                Purpose = AgentRuntimeContextPurpose.InteractiveChat,
                RuntimeToolProvidersEnabled = false, WorkspaceToolsEnabled = true, ToolCapabilitiesEnabled = true
            }
        };
        return (() => runtime.ExecutionPort.ExecuteAsync(new(agent,
            fixture.Provider with { Kind = ProviderKind.Ollama, Transport = ProviderTransportKind.ChatCompletions },
            fixture.Detail.ChatSession!, [], [], "Read the allowed catalog.", string.Empty,
            (_, _, _) => Task.CompletedTask, ExecutionOptions: options)), retained);
    }

    private sealed class ScriptAgentFactory(StorageScriptClient client) : IMafProviderAgentFactory {
        public AIAgent CreateFrameworkAgent(ProviderProfile provider, string model, ChatClientAgentOptions options,
            bool frameworkManagedHistory, bool allowBackgroundResponses) {
            Assert.True(frameworkManagedHistory);
            Assert.NotNull(options.ChatHistoryProvider);
            Assert.Contains(options.ChatOptions!.Tools!, tool => tool.Name == client.ToolName);
            return new ChatClientAgent(new MafToolAdmissionChatClient(client), options);
        }
    }

    private sealed class StorageScriptClient(string toolName, Dictionary<string, object?> arguments,
        bool failAfterResult = false, bool catalogFirst = false) : IChatClient {
        internal string ToolName => toolName;
        internal int Requests { get; private set; }
        internal List<string> Inputs { get; } = [];
        internal string? ReadFailureCode { get; private set; }
        private bool stopAfterResult = failAfterResult;

        internal void Reset() {
            stopAfterResult = false;
            Requests = 0;
            Inputs.Clear();
        }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) {
            Requests++;
            var input = messages.ToArray();
            Inputs.Add(JsonSerializer.Serialize(input, MafToolProtocolCodec.SerializationOptions));
            var results = input.SelectMany(message => message.Contents).OfType<FunctionResultContent>().ToArray();
            if (catalogFirst && !results.Any(item => item.CallId == CatalogCallId)) {
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                    [new FunctionCallContent(CatalogCallId, StorageToolPolicy.StorageCatalogList, new Dictionary<string, object?>())])));
            }
            if (results.SingleOrDefault(item => item.CallId == ReadCallId) is { } readResult) {
                if (stopAfterResult) {
                    throw new IOException("Fixture stops after the durable tool result and before the final provider response.");
                }
                if (catalogFirst) {
                    var failure = JsonSerializer.SerializeToElement(readResult.Result, MafToolProtocolCodec.SerializationOptions);
                    ReadFailureCode = failure.GetProperty("errorCode").GetString();
                    Assert.False(failure.GetProperty("succeeded").GetBoolean());
                    Assert.Contains("pageSize between 1 and 100", failure.GetProperty("message").GetString(), StringComparison.Ordinal);
                }
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "completed")));
            }
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent(ReadCallId, toolName, arguments)])));
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

    private static StorageAgentRuntimeToolProvider CreateProvider(IServiceProvider services, IStorageBrowseDriverRegistry? browseDrivers = null)
        => new(services.GetRequiredService<IStorageCatalogService>(), services.GetRequiredService<IStorageDriverRegistry>(),
            browseDrivers ?? services.GetRequiredService<IStorageBrowseDriverRegistry>(), services.GetRequiredService<StorageCatalogService>(),
            services.GetRequiredService<IAgentCatalogReadLeaseStore>(), services.GetRequiredService<IAgentToolAdmissionVerifier>());

    // The registry validates that a driver implements exactly the operation interfaces its capabilities advertise.
    // The wrapped FileSystem driver advertises Stat, so the counting wrapper must forward that surface as well.
    private sealed class CountingBrowseDriver(IStorageBrowseDriver owner) : IStorageBrowseDriver, IStorageBrowseStatDriver {
        private readonly IStorageBrowseStatDriver statOwner = Assert.IsAssignableFrom<IStorageBrowseStatDriver>(owner);
        public StorageProviderKind ProviderKind => owner.ProviderKind;
        public StorageBrowseCapability Capabilities => owner.Capabilities;
        public StorageBrowseWorkBudget MaximumBudget => owner.MaximumBudget;
        internal int Calls { get; private set; }

        public Task<StorageBrowsePage> BrowseAsync(StorageDriverInput storage, StorageBrowseRequest request, CancellationToken cancellationToken = default) {
            Calls++;
            return owner.BrowseAsync(storage, request, cancellationToken);
        }

        public Task<StorageBrowseEntry> StatAsync(StorageDriverInput storage, StorageBrowseStatRequest request, CancellationToken cancellationToken = default) {
            Calls++;
            return statOwner.StatAsync(storage, request, cancellationToken);
        }
    }

    private sealed class UntypedBrowseFailureProvider(IAgentRuntimeToolProvider owner) : IAgentRuntimeToolProvider {
        public int Order => owner.Order;
        public AgentRuntimeToolProviderDescriptor? Descriptor => owner.Descriptor;
        internal bool ObservedRequestRejection { get; private set; }

        public AgentRuntimeConfiguredWorkspacePolicy GetConfiguredWorkspacePolicy(AgentWorkspaceToolAccessSettings access,
            AgentRuntimeContextIntent intent) => owner.GetConfiguredWorkspacePolicy(access, intent);

        public async ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(AgentRuntimeToolProviderContext context, CancellationToken cancellationToken) {
            var tools = await owner.CreateToolsAsync(context, cancellationToken);
            return tools.Select(tool => {
                if (tool is not AIFunction function || tool.Name != StorageToolPolicy.StorageBrowse) {
                    return tool;
                }
                var wrapped = new UntypedBrowseFailureFunction(function, this);
                Assert.Equal(function.Name, wrapped.Name);
                Assert.Equal(function.Description, wrapped.Description);
                Assert.Equal(function.JsonSchema.GetRawText(), wrapped.JsonSchema.GetRawText());
                return wrapped;
            }).ToArray();
        }

        public IReadOnlyList<AgentRuntimeToolMetadata> GetToolMetadata(AgentRuntimeToolProviderContext context) => owner.GetToolMetadata(context);

        private sealed class UntypedBrowseFailureFunction(AIFunction inner, UntypedBrowseFailureProvider provider) : DelegatingAIFunction(inner) {
            protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken) {
                try {
                    return await base.InvokeCoreAsync(arguments, cancellationToken);
                } catch (InvalidOperationException exception) when (!provider.ObservedRequestRejection &&
                    exception is IAgentToolFailureEffectEvidence { ErrorCode: AgentToolInputValidationException.FailureCode, EffectState: AgentToolEffectState.None } &&
                    exception.InnerException is StorageBrowseException { Error.Code: StorageBrowseErrorCode.InvalidRequest }) {
                    provider.ObservedRequestRejection = true;
                    throw exception.InnerException;
                }
            }
        }
    }

    private sealed class StorageRecoveryExecution : IDisposable {
        private readonly AgentExecutionPreparationCache cache = new(AgentExecutionPreparationCachePolicy.Default);

        internal StorageRecoveryExecution(AgentToolAdmissionJournalFixture fixture, IServiceProvider services,
            StorageScriptClient client, IAgentRuntimeToolProvider provider) {
            var store = fixture.NewStore();
            var journal = fixture.NewJournal(store);
            var policies = new AgentToolPolicyCatalog(StorageToolPolicy.Capabilities);
            var dependencies = MafAgentRuntimeDependencies.FromServices(services);
            dependencies = dependencies with {
                ProviderAgentFactory = new ScriptAgentFactory(client), ToolAdmissionJournal = journal, ToolPolicies = policies,
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
                new FixedAgentExecutionProfileGenerationSource(fixture.Profile.Generation), new NoProcessLeases(),
                new ExternalTargetPathRegistryFactory(), toolAdmissionJournal: journal,
                executionAuthorityResolver: new CurrentAuthority(fixture), toolPolicies: policies);
        }

        internal AgentFrameworkWorkspaceService Service { get; }

        public void Dispose() {
            Service.Dispose();
            cache.Dispose();
        }
    }

    private sealed class CurrentAuthority(AgentToolAdmissionJournalFixture fixture) : IAgentExecutionAuthorityResolver {
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

    private sealed class NoProcessLeases : IWorkspaceExecutionRunProcessLeaseCleaner {
        public Task<WorkspaceExecutionRunProcessCleanupResult> CleanupAsync(Guid executionRunId)
            => Task.FromResult(WorkspaceExecutionRunProcessCleanupResult.Empty(executionRunId));
    }
}
