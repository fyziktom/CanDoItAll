using System.Runtime.CompilerServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Prompts;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class MafPromptGalleryDisclosureIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_MAF_gallery_recovery_requires_current_owner_eligibility_before_disclosing_saved_body(bool revokeConsumer) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var profile = services.GetRequiredService<IDatabaseRuntimeState>().GetSnapshot();
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(
            profileBinding: new(profile.ActiveProfileId!.Value, profile.ActiveFingerprint!, new(profile.Generation)));
        var gallery = services.GetRequiredService<IPromptGalleryService>();
        const string body = "Original immutable body retained for this admitted result.";
        var draft = new PromptGalleryDraft(null, null, null, "Journal gallery entry", "Retained immutable version.",
            PromptGalleryItemKind.FullPrompt, "agent-runtime", body, SupportedConsumers: [PromptGalleryConsumer.AgentRuntime]);
        var saved = await gallery.SaveDraftAsync(draft);
        Assert.True(saved.IsSuccess);
        var id = saved.Value!.PromptArtifactId;
        var version = await gallery.CreateVersionAsync(id, new("Journal proof", saved.Value.UpdatedAtUtc));
        Assert.True(version.IsSuccess);
        var agent = fixture.Agent with {
            IsTemplate = false, Status = AgentLifecycleStatus.Active, ChatHistoryMode = AgentChatHistoryMode.FrameworkManaged,
            Permissions = fixture.Agent.Permissions with { CanUseTools = true },
            ConfigurationJson = AgentThinkingEffortPolicy.WriteAgentOverride(fixture.Agent.ConfigurationJson, null)
        };
        var first = new ScriptClient(id, stopAfterResult: true);
        var stopped = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(fixture, services, agent, first));
        AgentToolAdmissionJournalFixture.RequireScriptedFault(stopped, "Fixture stops after the durable gallery body and before the final response.");
        Assert.Equal(2, first.Requests);
        var journal = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
        var proposal = Assert.Single(Assert.Single(journal.Batches).Proposals);
        Assert.Equal(AgentToolProposalState.Completed, proposal.State);
        Assert.Contains(body, proposal.Result!.PayloadJson, StringComparison.Ordinal);
        if (revokeConsumer) {
            var current = (await gallery.GetItemAsync(id)).Value!;
            Assert.True((await gallery.SaveDraftAsync(draft with {
                Id = id, ExpectedUpdatedAtUtc = current.UpdatedAtUtc, Content = "New human draft.",
                SupportedConsumers = [PromptGalleryConsumer.Workflow]
            })).IsSuccess);
        } else {
            Assert.True((await gallery.ArchiveAsync(id, true)).IsSuccess);
        }
        var denied = new ScriptClient(id);
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(fixture, services, agent, denied));
        Assert.Contains("no longer available to this runtime consumer and model", failure.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, denied.Requests);
        Assert.Equal(proposal.Result, (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!
            .ToolAdmission!.Batches[0].Proposals[0].Result);
        if (revokeConsumer) {
            var current = (await gallery.GetItemAsync(id)).Value!;
            Assert.True((await gallery.SaveDraftAsync(draft with {
                Id = id, ExpectedUpdatedAtUtc = current.UpdatedAtUtc, Content = "New human draft stays unchanged."
            })).IsSuccess);
        } else {
            Assert.True((await gallery.ArchiveAsync(id, false)).IsSuccess);
        }
        var before = (await gallery.GetItemAsync(id)).Value!;
        var restored = new ScriptClient(id);
        Assert.Equal("completed", (await ExecuteAsync(fixture, services, agent, restored)).ResponseText);
        Assert.Equal(1, restored.Requests);
        Assert.Contains(body, Assert.Single(restored.Inputs), StringComparison.Ordinal);
        var after = (await gallery.GetItemAsync(id)).Value!;
        Assert.Equal(before.UpdatedAtUtc, after.UpdatedAtUtc);
        Assert.Equal(before.DraftContent, after.DraftContent);
        Assert.Single(after.Versions);
        var recovered = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
        Assert.Equal(proposal.IntentId, recovered.Batches[0].Proposals[0].IntentId);
        Assert.Equal(proposal.Result, recovered.Batches[0].Proposals[0].Result);
    }

    private static Task<AgentRuntimeResponse> ExecuteAsync(AgentToolAdmissionJournalFixture fixture, IServiceProvider services,
        AgentDefinition agent, ScriptClient client) {
        var policies = new AgentToolPolicyCatalog(PromptGalleryToolPolicy.Capabilities);
        var provider = services.GetServices<IAgentRuntimeToolProvider>().OfType<PromptGalleryAgentRuntimeToolProvider>().Single();
        var dependencies = MafAgentRuntimeDependencies.FromServices(services);
        dependencies = dependencies with {
            ProviderAgentFactory = new ScriptAgentFactory(client),
            RuntimeToolProviderComposer = new RuntimeToolProviderComposer(new RuntimeToolProviderAccessFilter(policies), policies),
            ToolPolicies = policies, ToolAdmissionJournal = fixture.NewJournal(fixture.NewStore()),
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
        return runtime.ExecutionPort.ExecuteAsync(new(agent,
            fixture.Provider with { Kind = ProviderKind.Ollama, Transport = ProviderTransportKind.ChatCompletions },
            fixture.Detail.ChatSession!, [], [], "Retrieve the selected canonical prompt.", string.Empty,
            (_, _, _) => Task.CompletedTask, ExecutionOptions: options));
    }

    private sealed class ScriptAgentFactory(ScriptClient client) : IMafProviderAgentFactory {
        public AIAgent CreateFrameworkAgent(ProviderProfile provider, string model, ChatClientAgentOptions options,
            bool frameworkManagedHistory, bool allowBackgroundResponses) {
            Assert.True(frameworkManagedHistory);
            Assert.NotNull(options.ChatHistoryProvider);
            Assert.Contains(options.ChatOptions!.Tools!, tool => tool.Name == PromptGalleryToolPolicy.PromptGalleryItemGet);
            return new ChatClientAgent(new MafToolAdmissionChatClient(client), options);
        }
    }

    private sealed class ScriptClient(Guid id, bool stopAfterResult = false) : IChatClient {
        internal int Requests { get; private set; }
        internal List<string> Inputs { get; } = [];
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) {
            Requests++;
            var input = messages.ToArray();
            Inputs.Add(JsonSerializer.Serialize(input, MafToolProtocolCodec.SerializationOptions));
            if (input.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Any()) {
                if (stopAfterResult) {
                    throw new IOException("Fixture stops after the durable gallery body and before the final response.");
                }
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "completed")));
            }
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("diagnostic-gallery-get", PromptGalleryToolPolicy.PromptGalleryItemGet,
                    new Dictionary<string, object?> { ["request"] = new PromptGalleryAgentItemInput(id) })])));
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
