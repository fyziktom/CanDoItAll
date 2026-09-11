using System.Runtime.CompilerServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class MafHrResultDisclosureIntegrationTests {
    [Theory]
    [InlineData(HrAgentToolPolicy.HrCrmSearch)]
    [InlineData(HrAgentToolPolicy.HrCrmItemSummaryGet)]
    public async Task Actual_managed_HR_MAF_recovery_rechecks_owner_privacy_and_keeps_the_saved_result(string toolName) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var profile = services.GetRequiredService<IDatabaseRuntimeState>().GetSnapshot();
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(
            profileBinding: new(profile.ActiveProfileId!.Value, profile.ActiveFingerprint!, new(profile.Generation)), managedHr: true);
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var canonical = (await workspace.ListAgentsAsync(includeTemplates: true)).Single(item => item.Id == HrAgentIdentity.AgentId);
        var agent = canonical with {
            ChatHistoryMode = AgentChatHistoryMode.FrameworkManaged,
            ConfigurationJson = AgentThinkingEffortPolicy.WriteAgentOverride(canonical.ConfigurationJson, null)
        };
        var capabilities = (await workspace.ListCapabilitiesAsync()).Where(item => item.Kind == CapabilityKind.Tool).ToArray();
        var available = capabilities.Select(item => item.Id).ToHashSet();
        agent = agent with { Capabilities = agent.Capabilities.Where(item => available.Contains(item.CapabilityId)).ToArray() };
        Assert.True(HrAgentRuntimeAuthorizationPolicy.IsManagedHrActor(agent));
        Assert.True(HrAgentRuntimeAuthorizationPolicy.IsToolAuthorized(agent, capabilities, toolName, requiresCrmScope: true));
        var privateName = $"Retained HR person {Guid.NewGuid():N}";
        var created = await services.GetRequiredService<ICrmPartyCommandService>().CreatePartyAsync(
            new(PartyType.Person, privateName), "managed-disclosure-fixture");
        Assert.True(created.IsSuccess);
        var partyId = created.Value!.PartyId;
        object request = toolName == HrAgentToolPolicy.HrCrmSearch
            ? new CrmHrAgentSearchQuery(privateName, CrmHrAgentRecordKind.Party)
            : new CrmHrAgentItemReference(CrmHrAgentRecordKind.Party, partyId);
        var first = new ScriptClient(toolName, request, stopAfterResult: true);
        var stopped = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(fixture, services, agent, capabilities, first));
        AgentToolAdmissionJournalFixture.RequireScriptedFault(stopped, "Fixture stops after the durable HR result and before the next response.");
        Assert.Equal(2, first.Requests);
        var saved = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
        var proposal = Assert.Single(Assert.Single(saved.Batches).Proposals);
        Assert.Equal(AgentToolProposalState.Completed, proposal.State);
        Assert.Contains(privateName, proposal.Result!.PayloadJson, StringComparison.Ordinal);

        await SetPrivacyAsync(services, partyId, sensitive: true);
        var denied = new ScriptClient(toolName, request);
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(fixture, services, agent, capabilities, denied));
        Assert.Contains("no longer available for disclosure", failure.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, denied.Requests);
        var retained = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
        Assert.Equal(proposal, Assert.Single(Assert.Single(retained.Batches).Proposals));

        await SetPrivacyAsync(services, partyId, sensitive: false);
        var restored = new ScriptClient(toolName, request);
        Assert.Equal("completed", (await ExecuteAsync(fixture, services, agent, capabilities, restored)).ResponseText);
        Assert.Equal(1, restored.Requests);
        Assert.Contains(privateName, Assert.Single(restored.Inputs), StringComparison.Ordinal);
        var recovered = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
        Assert.Equal(proposal.IntentId, recovered.Batches[0].Proposals[0].IntentId);
        Assert.Equal(proposal.Result, recovered.Batches[0].Proposals[0].Result);
        await using var database = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        Assert.Equal(1, await database.Set<Party>().CountAsync(item => item.Id == partyId));
    }

    private static async Task SetPrivacyAsync(IServiceProvider services, Guid partyId, bool sensitive) {
        await using var database = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        (await database.Set<Party>().SingleAsync(item => item.Id == partyId)).IsSensitive = sensitive;
        await database.SaveChangesAsync();
    }

    private static Task<AgentRuntimeResponse> ExecuteAsync(AgentToolAdmissionJournalFixture fixture, IServiceProvider services,
        AgentDefinition agent, IReadOnlyList<CapabilityCatalogItem> capabilities, ScriptClient client) {
        var policies = new AgentToolPolicyCatalog(HrAgentToolPolicy.Capabilities);
        var provider = services.GetServices<IAgentRuntimeToolProvider>().OfType<HrAgentRuntimeToolProvider>().Single();
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
            fixture.Detail.ChatSession!, capabilities, [], "Read the authorized CRM summary.", string.Empty,
            (_, _, _) => Task.CompletedTask, ExecutionOptions: options));
    }

    private sealed class ScriptAgentFactory(ScriptClient client) : IMafProviderAgentFactory {
        public AIAgent CreateFrameworkAgent(ProviderProfile provider, string model, ChatClientAgentOptions options,
            bool frameworkManagedHistory, bool allowBackgroundResponses) {
            Assert.True(frameworkManagedHistory);
            Assert.NotNull(options.ChatHistoryProvider);
            Assert.Contains(options.ChatOptions!.Tools!, tool => tool.Name == client.ToolName);
            return new ChatClientAgent(new MafToolAdmissionChatClient(client), options);
        }
    }

    private sealed class ScriptClient(string toolName, object request, bool stopAfterResult = false) : IChatClient {
        internal string ToolName => toolName;
        internal int Requests { get; private set; }
        internal List<string> Inputs { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) {
            Requests++;
            var input = messages.ToArray();
            Inputs.Add(JsonSerializer.Serialize(input, MafToolProtocolCodec.SerializationOptions));
            if (input.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Any()) {
                if (stopAfterResult) {
                    throw new IOException("Fixture stops after the durable HR result and before the next response.");
                }
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "completed")));
            }
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("diagnostic-hr-read", toolName, new Dictionary<string, object?> { ["request"] = request })])));
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
