using System.Runtime.CompilerServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.Agents.Storage;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class MafStorageResultDisclosureIntegrationTests {
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
            target.IsEnabled = false;
            await catalogs.SaveAsync(target);
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
            target.IsEnabled = true;
            await catalogs.SaveAsync(target);
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

    private static StorageCatalogRecord Storage(string name, string root) => new() {
        Name = name, ProviderKind = StorageProviderKind.FileSystem, IsEnabled = true,
        ConnectionMode = StorageConnectionMode.Local, EndpointOrRoot = root,
        CapabilityMask = StorageCapability.Read | StorageCapability.Write | StorageCapability.Delete,
        HealthStatus = StorageHealthStatus.Healthy
    };

    private static async Task<string> CatalogRowsAsync(IServiceProvider services) {
        var factory = services.GetRequiredService<IDbContextFactory<StorageDbContext>>();
        await using var database = await factory.CreateDbContextAsync();
        return JsonSerializer.Serialize(await database.Set<StorageCatalogRecord>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync());
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
        var provider = new StorageAgentRuntimeToolProvider(services.GetRequiredService<IStorageCatalogService>(),
            services.GetRequiredService<IStorageDriverRegistry>(), services.GetRequiredService<IStorageBrowseDriverRegistry>(),
            services.GetRequiredService<StorageCatalogService>(), services.GetRequiredService<IAgentCatalogReadLeaseStore>(),
            services.GetRequiredService<IAgentToolAdmissionVerifier>());
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

    private sealed class StorageScriptClient(string toolName, Dictionary<string, object?> arguments, bool failAfterResult = false) : IChatClient {
        internal string ToolName => toolName;
        internal int Requests { get; private set; }
        internal List<string> Inputs { get; } = [];
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
            if (input.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Any()) {
                if (stopAfterResult) {
                    throw new IOException("Fixture stops after the durable tool result and before the final provider response.");
                }
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "completed")));
            }
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("diagnostic-storage-read", toolName, arguments)])));
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
