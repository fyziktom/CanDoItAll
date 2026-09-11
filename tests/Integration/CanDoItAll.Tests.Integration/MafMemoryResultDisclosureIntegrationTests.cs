using System.Runtime.CompilerServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Memory;
using CanDoItAll.AgentFramework.Memory.Tools;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Mock;
using CanDoItAll.Tests.Support;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class MafMemoryResultDisclosureIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Long_lived_MAF_Memory_recovery_rechecks_canonical_grants_without_recomposing_saved_tools(bool revokeSourceScope) {
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigurationOverrides = new Dictionary<string, string?> { ["Memory:Providers:DeterministicMock:Enabled"] = "true" }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var profile = services.GetRequiredService<IDatabaseRuntimeState>().GetSnapshot();
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(
            profileBinding: new(profile.ActiveProfileId!.Value, profile.ActiveFingerprint!, new(profile.Generation)));
        var profiles = services.GetRequiredService<IMemoryProviderProfileStore>();
        var provider = new MemoryProviderProfile(MemoryProviderInstanceId.Parse("memory.journal-proof"), "Journal Memory",
            MemoryProviderDriverKind.Mock, true, MemoryProviderHealthState.Healthy, MemoryProviderWorkspaceScope.AllWorkspaces,
            [], MemoryProviderProfilePolicy.Default, new(MemoryProviderKind.Parse("memory.mock"), MemoryProtocolVersion.Current,
                [new(MemoryCapabilityIds.ContextQuerySync, "v1", true), new(MemoryCapabilityIds.OperationStatus, "v1", true)],
                MemoryProviderInteractionSupport.SyncQueryOnly, [], MemoryProviderLimits.Default, MemoryExtensionData.Empty));
        await profiles.UpsertAsync(provider, DateTimeOffset.UtcNow);
        var access = new AgentMemoryAccessSettings {
            InvocationMode = AgentMemoryInvocationMode.Automatic, CanUseMemoryTools = true,
            ProviderBindings = [new(AgentMemoryProviderAlias.Parse("journal"), provider.InstanceId)],
            AllowedCapabilityIds = [MemoryCapabilityIds.ContextQuerySync, MemoryCapabilityIds.OperationStatus],
            AllowedSourceScopes = [MemorySourceScope.Manual]
        };
        var agent = fixture.Agent with {
            IsTemplate = false, Status = AgentLifecycleStatus.Active, ChatHistoryMode = AgentChatHistoryMode.FrameworkManaged,
            Permissions = fixture.Agent.Permissions with { CanUseTools = true },
            ConfigurationJson = AgentMemoryAccessMetadata.Write(AgentThinkingEffortPolicy.WriteAgentOverride(fixture.Agent.ConfigurationJson, null), access)
        };
        agent = await SaveCanonicalAgentAsync(services, agent);
        var driver = services.GetRequiredService<DeterministicMockMemoryProviderDriver>();
        var initialDispatchCount = driver.DispatchCount;
        var first = new ScriptClient(stopAfterResults: true);
        var (execute, retained) = CreateExecution(fixture, services, agent, first);
        var stopped = await Assert.ThrowsAnyAsync<Exception>(execute);
        AgentToolAdmissionJournalFixture.RequireScriptedFault(stopped, "Fixture stops after both durable Memory read results.");
        Assert.Equal(3, first.Requests);
        Assert.Equal(initialDispatchCount + 1, driver.DispatchCount);
        var journal = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
        var proposals = journal.Batches.SelectMany(batch => batch.Proposals).ToArray();
        Assert.Equal(2, proposals.Length);
        Assert.All(proposals, proposal => Assert.Equal(AgentToolProposalState.Completed, proposal.State));
        var operation = Assert.Single(await services.GetRequiredService<IMemoryOperationLedgerStore>().ListByProviderAsync(provider.InstanceId));
        var deniedAgent = agent;
        if (revokeSourceScope) {
            var narrowed = AgentMemoryAccessMetadata.Read(agent.ConfigurationJson);
            narrowed.AllowedSourceScopes = [];
            deniedAgent = await SaveCanonicalAgentAsync(services,
                agent with { ConfigurationJson = AgentMemoryAccessMetadata.Write(agent.ConfigurationJson, narrowed) });
            Assert.Empty(AgentMemoryAccessMetadata.Read(deniedAgent.ConfigurationJson).AllowedSourceScopes);
        } else {
            await profiles.UpsertAsync(provider with { IsEnabled = false }, DateTimeOffset.UtcNow);
        }
        first.Reset();
        var failure = await Assert.ThrowsAnyAsync<Exception>(execute);
        Assert.Contains("saved Memory result is no longer readable", failure.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, first.Requests);
        Assert.Equal(initialDispatchCount + 1, driver.DispatchCount);
        var afterDenial = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
        Assert.Equal(proposals.Select(item => item.Result), afterDenial.Batches.SelectMany(batch => batch.Proposals).Select(item => item.Result));
        if (!revokeSourceScope) {
            await profiles.UpsertAsync(provider, DateTimeOffset.UtcNow);
        }
        await SaveCanonicalAgentAsync(services, agent);
        first.Reset();
        Assert.Equal("completed", (await execute()).ResponseText);
        Assert.Equal(1, first.Requests);
        Assert.Contains("Mock memory context for retained context", Assert.Single(first.Inputs), StringComparison.Ordinal);
        Assert.Equal(1, retained.CompositionCount);
        Assert.Equal(agent.Id, retained.OriginalContext!.Agent.Id);
        Assert.Equal(agent.ConfigurationJson, retained.OriginalContext.Agent.ConfigurationJson);
        Assert.Equal(initialDispatchCount + 1, driver.DispatchCount);
        var after = Assert.Single(await services.GetRequiredService<IMemoryOperationLedgerStore>().ListByProviderAsync(provider.InstanceId));
        Assert.Equal(JsonSerializer.Serialize(operation), JsonSerializer.Serialize(after));
        var recovered = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
        Assert.Equal(proposals.Select(item => item.IntentId), recovered.Batches.SelectMany(batch => batch.Proposals).Select(item => item.IntentId));
    }

    private static async Task<AgentDefinition> SaveCanonicalAgentAsync(IServiceProvider services, AgentDefinition agent) {
        var customized = agent with {
            ConfigurationJson = AgentManagedSeedCustomizationMetadata.MarkCustomized(agent.ConfigurationJson)
        };
        var store = services.GetRequiredService<ISandboxWorkspaceCatalogStore>();
        await store.UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Where(item => item.Id != customized.Id).Append(customized).ToArray()
        });
        var saved = (await store.LoadCatalogSnapshotAsync()).Catalog.Agents.Single(item => item.Id == customized.Id);
        var expected = AgentMemoryAccessMetadata.Read(customized.ConfigurationJson);
        var actual = AgentMemoryAccessMetadata.Read(saved.ConfigurationJson);
        Assert.Equal(expected.CanUseMemoryTools, actual.CanUseMemoryTools);
        Assert.Equal(expected.AllowedSourceScopes, actual.AllowedSourceScopes);
        Assert.Equal(expected.AllowedCapabilityIds, actual.AllowedCapabilityIds);
        Assert.Equal(expected.ProviderBindings, actual.ProviderBindings);
        return saved;
    }

    private static (Func<Task<AgentRuntimeResponse>> Execute, RetainedRuntimeToolProvider Provider) CreateExecution(AgentToolAdmissionJournalFixture fixture, IServiceProvider services,
        AgentDefinition agent, ScriptClient client) {
        var policies = services.GetRequiredService<AgentToolPolicyCatalog>();
        var provider = services.GetServices<IAgentRuntimeToolProvider>().OfType<MemoryAgentRuntimeToolProvider>().Single();
        var retained = new RetainedRuntimeToolProvider(provider);
        var dependencies = MafAgentRuntimeDependencies.FromServices(services);
        dependencies = dependencies with {
            ProviderAgentFactory = new ScriptAgentFactory(client),
            RuntimeToolProviderComposer = new RuntimeToolProviderComposer(new RuntimeToolProviderAccessFilter(policies), policies),
            ToolPolicies = policies, ToolAdmissionJournal = fixture.NewJournal(fixture.NewStore()),
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
                Purpose = AgentRuntimeContextPurpose.InteractiveChat, RuntimeToolProvidersEnabled = true,
                WorkspaceToolsEnabled = false, ToolCapabilitiesEnabled = true
            }
        };
        return (() => runtime.ExecutionPort.ExecuteAsync(new(agent,
            fixture.Provider with { Kind = ProviderKind.Ollama, Transport = ProviderTransportKind.ChatCompletions },
            fixture.Detail.ChatSession!, [], [], "Retrieve retained Memory context and inspect its operation.", string.Empty,
            (_, _, _) => Task.CompletedTask, ExecutionOptions: options)), retained);
    }

    private sealed class ScriptAgentFactory(ScriptClient client) : IMafProviderAgentFactory {
        public AIAgent CreateFrameworkAgent(ProviderProfile provider, string model, ChatClientAgentOptions options,
            bool frameworkManagedHistory, bool allowBackgroundResponses) {
            Assert.True(frameworkManagedHistory);
            Assert.NotNull(options.ChatHistoryProvider);
            Assert.Contains(options.ChatOptions!.Tools!, tool => tool.Name == MemoryAgentRuntimeToolNames.ContextQuery);
            Assert.Contains(options.ChatOptions.Tools!, tool => tool.Name == MemoryAgentRuntimeToolNames.OperationStatus);
            return new ChatClientAgent(new MafToolAdmissionChatClient(client), options);
        }
    }

    private sealed class ScriptClient(bool stopAfterResults = false) : IChatClient {
        internal int Requests { get; private set; }
        internal List<string> Inputs { get; } = [];
        private bool stopAfterResult = stopAfterResults;

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
            if (results.Length >= 2) {
                if (stopAfterResult) {
                    throw new IOException("Fixture stops after both durable Memory read results.");
                }
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "completed")));
            }
            FunctionCallContent call;
            if (results.Length == 1) {
                var saved = JsonSerializer.SerializeToElement(results[0].Result, JsonSerializerOptions.Web)
                    .Deserialize<MemoryContextQueryToolResult>(JsonSerializerOptions.Web)!;
                call = new("diagnostic-memory-status", MemoryAgentRuntimeToolNames.OperationStatus,
                    new Dictionary<string, object?> { ["input"] = new MemoryOperationStatusToolInput(saved.OperationId!.Value) });
            } else {
                call = new("diagnostic-memory-query", MemoryAgentRuntimeToolNames.ContextQuery,
                    new Dictionary<string, object?> { ["input"] = new MemoryContextQueryToolInput("retained context", "journal") });
            }
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
