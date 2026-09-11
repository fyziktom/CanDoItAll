using System.Runtime.CompilerServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class CrmPlanningRuntimeIntegrationTests {
    [Theory]
    [InlineData(CrmPlanningToolPolicy.Search)]
    [InlineData(CrmPlanningToolPolicy.Summary)]
    public async Task Registered_ordinary_planner_reads_real_CRM_and_rechecks_privacy_before_exact_saved_result_replay(string tool) {
        await using var fixture = await Fixture.CreateAsync();
        Assert.NotEqual(HrAgentIdentity.AgentId, fixture.Agent.Id);
        var proposal = await fixture.CompleteBeforeLostReplyAsync(tool);
        Assert.Contains(fixture.PersonName, proposal.Result!.PayloadJson, StringComparison.Ordinal);
        Assert.NotNull(proposal.Payload.SourcePreparation);
        Assert.Equal(2, proposal.Payload.SemanticVersion);
        Assert.Equal(AgentToolProposalEffect.Read, proposal.Payload.Effect);
        Assert.Equal(AgentToolProposalRecovery.RevalidateAndRead, proposal.Payload.Recovery);
        Assert.False(proposal.RequiresApproval);
        Assert.Equal(1, fixture.Queries.Reads);

        await fixture.SetPrivacyAsync(true);
        var denied = fixture.Client(tool);
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(denied));
        Assert.Equal(0, denied.Requests);
        Assert.Equal(proposal, await fixture.ProposalAsync());

        await fixture.SetPrivacyAsync(false);
        var replay = fixture.Client(tool);
        var result = await fixture.ExecuteAsync(replay);
        Assert.Equal("completed", result.ResponseText);
        Assert.Equal(1, replay.Requests);
        Assert.Contains(fixture.PersonName, Assert.Single(replay.Inputs), StringComparison.Ordinal);
        var retained = await fixture.ProposalAsync();
        Assert.Equal(proposal.IntentId, retained.IntentId);
        Assert.Equal(proposal.Result, retained.Result);
        await using var owner = await fixture.OwnerAsync();
        Assert.Equal(1, await owner.Set<Party>().CountAsync(item => item.Id == fixture.PersonId));
        Assert.Equal(fixture.PersonName, (await owner.Set<Party>().SingleAsync(item => item.Id == fixture.PersonId)).DisplayName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Current_CRM_scope_or_capability_revocation_blocks_prepared_dispatch_before_the_real_owner_read(bool revokeScope) {
        await using var fixture = await Fixture.CreateAsync();
        var client = fixture.Client(CrmPlanningToolPolicy.Search);
        client.BeforeProposal = () => fixture.SaveActorAsync(grantSearch: revokeScope, crmScope: !revokeScope);
        var result = await fixture.ExecuteAsync(client);
        Assert.Equal("completed", result.ResponseText);
        Assert.Equal(2, client.Requests);
        Assert.Equal(0, fixture.Queries.Reads);
        var saved = await fixture.ProposalAsync();
        Assert.Equal(AgentToolProposalState.Completed, saved.State);
        Assert.Equal(AgentToolEffectState.NotCommitted, saved.EffectState);
        Assert.Contains("ToolPolicyDenied", saved.Result!.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.PersonName, saved.Result.PayloadJson, StringComparison.Ordinal);
        await fixture.SaveActorAsync();
        var observed = fixture.Client(CrmPlanningToolPolicy.Search);
        Assert.Equal("completed", (await fixture.ExecuteAsync(observed)).ResponseText);
        Assert.Equal(0, observed.Requests);
        Assert.Empty(observed.Inputs);
        Assert.Equal(0, fixture.Queries.Reads);
        Assert.Equal(saved, await fixture.ProposalAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Restart_cannot_replace_the_original_project_lifetime_or_capability_identity(bool replaceCapability) {
        await using var fixture = await Fixture.CreateAsync();
        var proposal = await fixture.CompleteBeforeLostReplyAsync(CrmPlanningToolPolicy.Search);
        if (replaceCapability) {
            await fixture.ReplaceSearchCapabilityAsync();
        } else {
            var originalLifetime = fixture.Project.LifetimeId;
            await fixture.ReplaceProjectAsync();
            Assert.NotEqual(originalLifetime, fixture.Project.LifetimeId);
        }
        await fixture.SaveActorAsync();
        var client = fixture.Client(CrmPlanningToolPolicy.Search);
        var reads = fixture.Queries.Reads;
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(client));
        Assert.Equal(0, client.Requests);
        Assert.Equal(reads, fixture.Queries.Reads);
        Assert.Equal(proposal, await fixture.ProposalAsync());
    }

    [Fact]
    public async Task Enabling_a_new_read_capability_does_not_widen_an_already_persisted_SDK_invocation() {
        await using var fixture = await Fixture.CreateAsync(grantSummary: false);
        var interrupted = fixture.Client(CrmPlanningToolPolicy.Search);
        interrupted.StopBeforeProposal = true;
        var first = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(interrupted));
        AgentToolAdmissionJournalFixture.RequireScriptedFault(first, ScriptClient.BeforeProposalFault);
        var original = (await fixture.Journal.NewStore().GetExecutionRunAsync(fixture.Journal.Session.ExecutionRunId))!.ToolAdmission!;
        Assert.Single(original.Segments);
        Assert.Empty(original.Batches);
        await fixture.SaveActorAsync(grantSummary: true);
        var resumed = fixture.Client(CrmPlanningToolPolicy.Summary);
        var denied = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(resumed));
        Assert.Contains("incompatible", denied.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, resumed.Requests);
        Assert.Equal(0, fixture.Queries.Reads);
        var retained = (await fixture.Journal.NewStore().GetExecutionRunAsync(fixture.Journal.Session.ExecutionRunId))!.ToolAdmission!;
        Assert.Equal(original.Segments[0], Assert.Single(retained.Segments));
        Assert.Empty(retained.Batches);
    }

    [Fact]
    public async Task Catalog_keys_are_selectable_without_automatic_assignments_and_ungranted_agents_receive_no_CRM_tools() {
        await using var fixture = await Fixture.CreateAsync(grantSearch: false, grantSummary: false);
        Assert.Equal(2, fixture.Capabilities.Length);
        await using var lease = await fixture.Journal.NewJournal().AcquireRunAsync(fixture.Journal.Session, default);
        using var bound = lease.Bind();
        var provider = fixture.Provider;
        var context = fixture.Context();
        Assert.Empty(await provider.CreateToolsAsync(context, default));
        Assert.Empty(provider.GetToolMetadata(context));
        Assert.Equal(AgentRuntimeToolProviderPurpose.InteractiveChat, Assert.Single(provider.Descriptor.SupportedPurposes));
        Assert.All(CrmPlanningToolPolicy.Capabilities, item => Assert.Equal(ToolInvocationClassification.Read, item.Classification));
    }

    [Theory]
    [InlineData(CrmPlanningToolPolicy.Search)]
    [InlineData(CrmPlanningToolPolicy.Summary)]
    public async Task Actual_AIFunction_rejects_calls_without_the_original_journal_claim(string tool) {
        await using var fixture = await Fixture.CreateAsync();
        await using var lease = await fixture.Journal.NewJournal().AcquireRunAsync(fixture.Journal.Session, default);
        using var bound = lease.Bind();
        var tools = await fixture.Provider.CreateToolsAsync(fixture.Context(), default);
        var function = Assert.IsAssignableFrom<AIFunction>(Assert.Single(tools, item => item.Name == tool));
        await Assert.ThrowsAnyAsync<Exception>(async () => await function.InvokeAsync(new AIFunctionArguments {
            ["request"] = fixture.Request(tool)
        }));
        Assert.Equal(0, fixture.Queries.Reads);
        Assert.Empty((await fixture.Journal.NewStore().GetExecutionRunAsync(fixture.Journal.Session.ExecutionRunId))!.ToolAdmission!.Batches);
    }

    [Fact]
    public async Task Prepared_provider_owned_source_recovers_before_dispatch_with_the_same_intent_and_real_owner_read() {
        await using var fixture = await Fixture.CreateAsync();
        var call = new FunctionCallContent("prepared-planning-read", CrmPlanningToolPolicy.Search,
            new Dictionary<string, object?> { ["request"] = fixture.Request(CrmPlanningToolPolicy.Search) });
        var requestDigest = AgentToolProtocolEnvelope.ComputeDigest("original-planning-request");
        AgentToolProposalRecord original;
        var journal = fixture.Journal.NewJournal();
        using var transport = fixture.Client(CrmPlanningToolPolicy.Search);
        await using (var lease = await journal.AcquireRunAsync(fixture.Journal.Session, default)) {
            using var bound = lease.Bind();
            var opened = await fixture.OpenManualAsync(journal, lease, transport);
            using var active = opened.Context.Bind();
            await opened.Context.AdmitResponseAsync(requestDigest, AgentToolAdmissionJournalFixture.Envelope(), [call], default);
            original = Assert.Single(Assert.Single((await journal.ReadAsync(lease, default)).Batches).Proposals);
            Assert.Equal(AgentToolProposalState.Prepared, original.State);
            Assert.NotNull(original.Payload.SourcePreparation);
            Assert.Equal(0, fixture.Queries.Reads);
        }

        var restarted = fixture.Journal.NewJournal(fixture.Journal.NewStore());
        await using var resumedLease = await restarted.AcquireRunAsync(fixture.Journal.Session, default);
        using var resumedBound = resumedLease.Bind();
        var restored = await fixture.OpenManualAsync(restarted, resumedLease, transport);
        using var restoredContext = restored.Context.Bind();
        Assert.NotNull(await restored.Context.ReplayResponseAsync(requestDigest, default));
        var function = Assert.IsAssignableFrom<AIFunction>(Assert.Single(restored.Tools, item => item.Name == call.Name));
        using var effect = AgentToolInvocationEffectScope.Begin();
        var result = await restored.Context.InvokeAsync(call,
            token => function.InvokeAsync(new AIFunctionArguments {
                ["request"] = fixture.Request(CrmPlanningToolPolicy.Search)
            }, token), effect, default);
        var json = JsonSerializer.SerializeToElement(result, MafToolProtocolCodec.SerializationOptions);
        var item = Assert.Single(json.Deserialize<CrmHrAgentQueryItem[]>(MafToolProtocolCodec.SerializationOptions)!);
        Assert.Equal(fixture.PersonId, item.Id);
        Assert.Equal(fixture.PersonName, item.DisplayLabel);
        Assert.Equal(1, fixture.Queries.Reads);
        Assert.Equal(0, transport.Requests);
        var saved = Assert.Single(Assert.Single((await restarted.ReadAsync(resumedLease, default)).Batches).Proposals);
        Assert.Equal(original.IntentId, saved.IntentId);
        Assert.Equal(original.Payload, saved.Payload);
        Assert.Equal(AgentToolProposalState.Completed, saved.State);
    }

    private sealed class Fixture(TestApplication application, AsyncServiceScope scope, AgentToolAdmissionJournalFixture journal,
        ProjectWriteAdmission project, CapabilityCatalogItem[] capabilities, QueryCounts queries) : IAsyncDisposable {
        internal AgentToolAdmissionJournalFixture Journal => journal;
        internal IServiceProvider Services => scope.ServiceProvider;
        internal ProjectWriteAdmission Project { get; private set; } = project;
        internal CapabilityCatalogItem[] Capabilities { get; private set; } = capabilities;
        internal QueryCounts Queries => queries;
        internal AgentDefinition Agent { get; private set; } = journal.Agent;
        internal Guid PersonId { get; private set; }
        internal string PersonName { get; private set; } = string.Empty;
        internal CrmPlanningAgentRuntimeToolProvider Provider => Services.GetServices<IAgentRuntimeToolProvider>()
            .OfType<CrmPlanningAgentRuntimeToolProvider>().Single();

        internal static async Task<Fixture> CreateAsync(bool grantSearch = true, bool grantSummary = true) {
            var queries = new QueryCounts();
            var application = await TestApplication.CreateAsync(new() {
                ConfigureServices = services => {
                    services.AddScoped<CrmHrAgentQueryService>();
                    services.AddScoped<ICrmHrAgentQueryService>(provider => new CountingQueries(
                        provider.GetRequiredService<CrmHrAgentQueryService>(), queries));
                }
            });
            var scope = application.Services.CreateAsyncScope();
            AgentToolAdmissionJournalFixture? journal = null;
            try {
                var services = scope.ServiceProvider;
                var projectId = Guid.NewGuid();
                Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(projectId,
                    new() { Name = "Original CRM planning surface" })).IsSuccess);
                var project = (await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId))!;
                var catalog = await services.GetRequiredService<ISandboxWorkspaceCatalogStore>().LoadCatalogAsync();
                var keys = new[] { CrmPlanningToolPolicy.SearchCapability, CrmPlanningToolPolicy.SummaryCapability };
                var capabilities = catalog.Capabilities.Where(item => keys.Contains(item.Key)).ToArray();
                Assert.Equal(2, capabilities.Length);
                Assert.All(catalog.Agents, actor => Assert.DoesNotContain(actor.Capabilities, item => keys.Contains(item.CapabilityKey)));
                var profile = services.GetRequiredService<IDatabaseRuntimeState>().GetSnapshot();
                var workspace = WorkspaceScopeDescriptor.Project(projectId.ToString("D"));
                var actorId = Guid.NewGuid();
                var sharedContext = StructureAdmittedContextFixture.Capture(actorId, projectId, new(profile.Generation));
                Assert.Equal(2, sharedContext.Options.TransientContext!.Attachments.Length);
                journal = await AgentToolAdmissionJournalFixture.CreateAsync(profileBinding:
                    new(profile.ActiveProfileId!.Value, profile.ActiveFingerprint!, new(profile.Generation)),
                    transientContext: sharedContext.Options.TransientContext, storageScope: workspace,
                    contextAttachmentCodecs: services.GetServices<IAgentChatContextAttachmentCodec>().ToArray(),
                    configureAgent: actor => actor with { Id = actorId, Workload = AgentWorkloadKind.General,
                        IsTemplate = false, TemplateKey = string.Empty, Status = AgentLifecycleStatus.Active,
                        ConfigurationJson = AgentThinkingEffortPolicy.WriteAgentOverride(actor.ConfigurationJson, null) });
                Assert.Equal(AgentToolAdmissionSupport.Recoverable, journal.Detail.Run.ToolAdmission!.Support);
                Assert.Equal(AgentToolJournalRecord.TypedContextSchemaVersion, journal.Detail.Run.ToolAdmission.SchemaVersion);
                var restored = journal.NewJournal(journal.NewStore()).RestoreRuntimeContext(journal.Detail.Run.ToolAdmission.RuntimeContext!);
                Assert.Equal(AgentChatContextDigest.Compute(sharedContext.Options.TransientContext), AgentChatContextDigest.Compute(restored));
                var fixture = new Fixture(application, scope, journal, project, capabilities, queries);
                await fixture.SaveActorAsync(grantSearch, grantSummary);
                fixture.PersonName = $"Planning fixture {Guid.NewGuid():N}";
                var created = await services.GetRequiredService<ICrmPartyCommandService>().CreatePartyAsync(
                    new(PartyType.Person, fixture.PersonName), "crm-planning-fixture");
                Assert.True(created.IsSuccess);
                fixture.PersonId = created.Value!.PartyId;
                Assert.Null(await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().GetExecutionRunAsync(journal.Session.ExecutionRunId));
                Assert.NotNull(await journal.NewStore().GetExecutionRunAsync(journal.Session.ExecutionRunId));
                return fixture;
            } catch {
                if (journal is not null) {
                    await journal.DisposeAsync();
                }
                await scope.DisposeAsync();
                await application.DisposeAsync();
                throw;
            }
        }

        internal async Task SaveActorAsync(bool grantSearch = true, bool grantSummary = true, bool crmScope = true) {
            var configuration = AgentProjectStructureAccessMetadata.Write(Agent.ConfigurationJson, new() {
                CanRead = true, AllowedProjectIds = [Project.ProjectId],
                AllowedProjectLifetimes = [new(Project.DatabaseProfileId, Project.ProjectId, Project.LifetimeId)]
            });
            configuration = AgentMemoryAccessMetadata.Write(configuration,
                new() { AllowedSourceScopes = crmScope ? [MemorySourceScope.Crm] : [] });
            var actor = Agent with {
                ConfigurationJson = AgentManagedSeedCustomizationMetadata.MarkCustomized(configuration),
                Permissions = Agent.Permissions with { CanUseTools = true },
                Capabilities = Capabilities.Where(item => item.Key == CrmPlanningToolPolicy.SearchCapability ? grantSearch : grantSummary)
                    .Select(item => new AgentCapabilityAssignment(item.Id, item.Key, item.Kind,
                        item.ProofStatus, item.LastVerifiedAtUtc, item.ProofNotes)).ToArray()
            };
            var store = Services.GetRequiredService<ISandboxWorkspaceCatalogStore>();
            await store.UpdateCatalogAsync(current => current with {
                Agents = current.Agents.Where(item => item.Id != actor.Id).Append(actor).ToArray()
            });
            Agent = (await store.LoadCatalogAsync()).Agents.Single(item => item.Id == actor.Id);
            Assert.Equal(actor.Capabilities.Select(item => item.CapabilityId), Agent.Capabilities.Select(item => item.CapabilityId));
        }

        internal async Task ReplaceProjectAsync() {
            var projects = Services.GetRequiredService<ProjectsService>();
            await projects.DeleteAsync(Project.ProjectId);
            Assert.True((await projects.CreateAsync(Project.ProjectId, new() { Name = "Independent replacement" })).IsSuccess);
            var originalLifetime = Project.LifetimeId;
            Project = (await Services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(Project.ProjectId))!;
            Agent = (await Services.GetRequiredService<ISandboxWorkspaceCatalogStore>().LoadCatalogAsync())
                .Agents.Single(item => item.Id == Agent.Id);
            Assert.DoesNotContain(AgentProjectStructureAccessMetadata.Read(Agent.ConfigurationJson).AllowedProjectLifetimes,
                binding => binding.LifetimeId == originalLifetime);
        }

        internal async Task ReplaceSearchCapabilityAsync() {
            var original = Capabilities.Single(item => item.Key == CrmPlanningToolPolicy.SearchCapability);
            var replacement = original with { Id = Guid.NewGuid() };
            Capabilities = Capabilities.Select(item => item.Id == original.Id ? replacement : item).ToArray();
            await Services.GetRequiredService<ISandboxWorkspaceCatalogStore>().UpdateCatalogAsync(current => current with {
                Capabilities = current.Capabilities.Where(item => item.Id != original.Id).Append(replacement).ToArray()
            });
        }

        internal object Request(string tool) => tool == CrmPlanningToolPolicy.Search
            ? new CrmHrAgentSearchQuery(PersonName, CrmHrAgentRecordKind.Party, 1)
            : new CrmHrAgentItemReference(CrmHrAgentRecordKind.Party, PersonId);

        internal ScriptClient Client(string tool) => new(tool, Request(tool));

        internal AgentRuntimeToolProviderContext Context() => new(Agent, journal.Provider, Capabilities, false,
            AgentRuntimeToolProviderPurpose.InteractiveChat, "crm-planning-fixture", Intent(), new Dictionary<string, string>()) {
            AdmittedToolSession = journal.Session, ToolAdmissionSupport = AgentToolAdmissionSupport.Recoverable,
            Governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(journal.Detail.Run.MetadataJson)
        };

        private AgentRuntimeContextIntent Intent() => AgentRuntimeContextIntent.Empty with {
            Purpose = AgentRuntimeContextPurpose.InteractiveChat, SourceKind = AgentChatTrustedSourceKinds.ProjectStructure,
            SourceId = Project.ProjectId.ToString("D"), WorkspaceScope = journal.StorageScope,
            RuntimeToolProvidersEnabled = true, WorkspaceToolsEnabled = false, ToolCapabilitiesEnabled = true
        };

        internal Task<AgentRuntimeResponse> ExecuteAsync(ScriptClient client) {
            var policies = Services.GetRequiredService<AgentToolPolicyCatalog>();
            var dependencies = MafAgentRuntimeDependencies.FromServices(Services);
            dependencies = dependencies with {
                ProviderAgentFactory = new ScriptAgentFactory(client),
                RuntimeToolProviderComposer = new RuntimeToolProviderComposer(new RuntimeToolProviderAccessFilter(policies), policies),
                ToolPolicies = policies, ToolAdmissionJournal = journal.NewJournal(journal.NewStore()),
                CapabilityDependencies = dependencies.CapabilityDependencies with {
                    RuntimeToolProviders = [Provider], ContextContributors = [], ToolPolicies = policies
                }
            };
            var options = new AgentRuntimeExecutionOptions(null, AgentFinalizerMode.Disabled, true, 0) {
                AdmittedToolSession = journal.Session, ToolAdmissionSupport = AgentToolAdmissionSupport.Recoverable,
                Governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(journal.Detail.Run.MetadataJson),
                AuthorityPolicyFingerprint = "fixture-authority", ModelContextDigest = "fixture-context", ContextIntent = Intent(),
                TransientContext = journal.NewJournal(journal.NewStore()).RestoreRuntimeContext(journal.Detail.Run.ToolAdmission!.RuntimeContext!)
            };
            return new MafAgentRuntime(journal.WorkspaceRoot, journal.StorageScope, dependencies).ExecutionPort.ExecuteAsync(
                new(Agent, journal.Provider with { Kind = ProviderKind.Ollama, Transport = ProviderTransportKind.ChatCompletions },
                    journal.Detail.ChatSession!, Capabilities, [], "Read the original authorized planning facts.", string.Empty,
                    (_, _, _) => Task.CompletedTask, ExecutionOptions: options));
        }

        internal async Task<(MafToolRunContext Context, IReadOnlyList<AITool> Tools)> OpenManualAsync(
            AgentToolAdmissionJournal admissionJournal, AgentToolRunLease lease, ScriptClient transport) {
            var provider = Provider;
            var context = Context();
            var tools = await provider.CreateToolsAsync(context, default);
            var capabilities = new RuntimeCapabilityState { ToolPolicies = Services.GetRequiredService<AgentToolPolicyCatalog>() };
            capabilities.Tools.AddRange(tools);
            capabilities.RuntimeToolMetadata.AddRange(provider.GetToolMetadata(context));
            var agentOptions = MafChatClientAgentOptionsFactory.Create(new ChatOptions {
                ModelId = journal.Provider.DefaultModel, Tools = tools.ToList(), AllowMultipleToolCalls = false
            });
            agentOptions.ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions {
                JsonSerializerOptions = MafToolProtocolCodec.SerializationOptions
            });
            agentOptions.RequirePerServiceCallChatHistoryPersistence = true;
            var agent = new ChatClientAgent(new MafToolAdmissionChatClient(transport), agentOptions);
            var options = new AgentRuntimeExecutionOptions(null, AgentFinalizerMode.Disabled, true, 0) {
                AdmittedToolSession = journal.Session, RequireDurableToolProtocol = true,
                Governance = context.Governance, AuthorityPolicyFingerprint = "fixture-authority", ModelContextDigest = "fixture-context",
                CapabilityPolicyFingerprint = "fixture-planning-capabilities", HistoryMode = AgentChatHistoryMode.FrameworkManaged,
                ToolsetFingerprint = MafToolsetFingerprint.ComputeContractFingerprint(capabilities.Tools, capabilities.ToolPolicies),
                ContextIntent = Intent()
            };
            var opened = await MafToolRunContext.OpenAsync(admissionJournal, lease, agent, await agent.CreateSessionAsync(),
                Agent, journal.Provider, journal.Provider.DefaultModel, journal.Detail.ChatSession!, options, capabilities,
                [new(ChatRole.User, "Read the original planning request.")], new MafRuntimeSessionPersistenceDriver(), false,
                (_, _, _) => Task.CompletedTask, default);
            return (opened.Context, tools);
        }

        internal async Task<AgentToolProposalRecord> CompleteBeforeLostReplyAsync(string tool) {
            var client = Client(tool);
            client.StopAfterResult = true;
            var failure = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(client));
            AgentToolAdmissionJournalFixture.RequireScriptedFault(failure, ScriptClient.AfterResultFault);
            Assert.Equal(2, client.Requests);
            var proposal = await ProposalAsync();
            Assert.Equal(AgentToolProposalState.Completed, proposal.State);
            return proposal;
        }

        internal async Task<AgentToolProposalRecord> ProposalAsync() => Assert.Single(
            (await journal.NewStore().GetExecutionRunAsync(journal.Session.ExecutionRunId))!.ToolAdmission!.Batches
                .SelectMany(batch => batch.Proposals));

        internal Task<CrmHrDbContext> OwnerAsync() => Services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();

        internal async Task SetPrivacyAsync(bool sensitive) {
            await using var owner = await OwnerAsync();
            (await owner.Set<Party>().SingleAsync(item => item.Id == PersonId)).IsSensitive = sensitive;
            await owner.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync() {
            await scope.DisposeAsync();
            await journal.DisposeAsync();
            await application.DisposeAsync();
        }
    }

    private sealed class QueryCounts {
        internal int Reads { get; set; }
    }

    private sealed class CountingQueries(CrmHrAgentQueryService owner, QueryCounts counts) : ICrmHrAgentQueryService {
        public Task<Result<IReadOnlyList<CrmHrAgentQueryItem>>> SearchAsync(CrmHrAgentSearchQuery query, CancellationToken cancellationToken = default) {
            counts.Reads++;
            return owner.SearchAsync(query, cancellationToken);
        }
        public Task<Result<CrmHrAgentQueryItem>> GetSummaryAsync(CrmHrAgentItemReference reference, CancellationToken cancellationToken = default) {
            counts.Reads++;
            return owner.GetSummaryAsync(reference, cancellationToken);
        }
    }

    private sealed class ScriptAgentFactory(ScriptClient client) : IMafProviderAgentFactory {
        public AIAgent CreateFrameworkAgent(ProviderProfile provider, string model, ChatClientAgentOptions options,
            bool frameworkManagedHistory, bool allowBackgroundResponses) {
            Assert.True(frameworkManagedHistory);
            Assert.NotNull(options.ChatHistoryProvider);
            var tool = Assert.Single(options.ChatOptions!.Tools!, item => item.Name == client.ToolName);
            Assert.IsNotType<ApprovalRequiredAIFunction>(tool);
            return new ChatClientAgent(new MafToolAdmissionChatClient(client), options);
        }
    }

    private sealed class ScriptClient(string toolName, object request) : IChatClient {
        internal const string BeforeProposalFault = "Fixture stops after the SDK checkpoint and before any proposal.";
        internal const string AfterResultFault = "Fixture stops after the durable CRM result and before the next response.";
        internal string ToolName => toolName;
        internal int Requests { get; private set; }
        internal bool StopBeforeProposal { get; set; }
        internal bool StopAfterResult { get; set; }
        internal Func<Task>? BeforeProposal { get; set; }
        internal List<string> Inputs { get; } = [];

        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) {
            Requests++;
            var input = messages.ToArray();
            Inputs.Add(JsonSerializer.Serialize(input, MafToolProtocolCodec.SerializationOptions));
            if (input.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Any()) {
                if (StopAfterResult) {
                    throw new IOException(AfterResultFault);
                }
                return new(new ChatMessage(ChatRole.Assistant, "completed"));
            }
            if (StopBeforeProposal) {
                throw new IOException(BeforeProposalFault);
            }
            if (BeforeProposal is not null) {
                await BeforeProposal();
            }
            return new(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("crm-planning-read", toolName, new Dictionary<string, object?> { ["request"] = request })]));
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
