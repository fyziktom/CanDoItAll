using CanDoItAll.Infrastructure.Persistence;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed partial class ProjectStructureResultDisclosureIntegrationTests {
    [Theory]
    [InlineData(ProjectStructureToolPolicy.ProjectStructureRead)]
    [InlineData(ProjectStructureToolPolicy.ProjectStructureProjectsList)]
    [InlineData(ProjectStructureToolPolicy.ProjectStructureHierarchyGet)]
    [InlineData(ProjectStructureToolPolicy.ProjectStructureNodeWorkflowAddOptions)]
    public async Task Actual_MAF_attachment_preserves_parent_only_projection_access_and_rechecks_revocation_before_cached_output(string toolName) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var parent = await CreateProjectAsync(services, "Original parent");
        var child = await CreateProjectAsync(services, "Original related child");
        Assert.True((await services.GetRequiredService<ProjectsService>().AddSubprojectAsync(parent.ProjectId, child.ProjectId)).IsSuccess);
        await using var fixture = await CreateJournalAsync(services, parent.ProjectId);
        var agent = await SaveActorAsync(services, fixture.Agent, parent, canRead: true);
        var originalClient = new ScriptClient(toolName, Arguments(toolName, parent.ProjectId), failAfterResult: true);
        var checkpointFailure = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(fixture, services, agent, originalClient));
        AgentToolAdmissionJournalFixture.RequireScriptedFault(checkpointFailure,
            "Injected final provider acknowledgement loss after the saved tool result.");
        Assert.Equal(2, originalClient.Requests);
        var proposal = await ReadProposalAsync(fixture);
        Assert.Equal(AgentToolProposalState.Completed, proposal.State);
        Assert.Equal(AgentToolProposalRecovery.RevalidateAndRead, proposal.Payload.Recovery);
        var evidence = ProjectStructureDisclosureEvidenceCodec.Read(Assert.IsType<AgentToolProtocolEnvelope>(proposal.DisclosureEvidence));
        Assert.Equal(ProjectStructureDisclosureState.Complete, evidence.State);
        Assert.Equal(parent.ProjectId, Assert.Single(evidence.DirectAccessProjectIds));
        Assert.Contains(parent, evidence.Targets);
        if (toolName is ProjectStructureToolPolicy.ProjectStructureHierarchyGet or ProjectStructureToolPolicy.ProjectStructureNodeWorkflowAddOptions) {
            Assert.Contains(child, evidence.Targets);
            Assert.DoesNotContain(child.ProjectId, AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson).AllowedProjectIds);
            Assert.Contains("Original related child", proposal.Result!.PayloadJson, StringComparison.Ordinal);
        }

        var editor = await services.GetRequiredService<ProjectsService>().GetAsync(parent.ProjectId);
        editor.Name = "Human edited parent";
        Assert.True((await services.GetRequiredService<ProjectsService>().SaveAsync(editor)).IsSuccess);
        if (toolName == ProjectStructureToolPolicy.ProjectStructureHierarchyGet) {
            var childEditor = await services.GetRequiredService<ProjectsService>().GetAsync(child.ProjectId);
            childEditor.Name = "Human edited related child";
            Assert.True((await services.GetRequiredService<ProjectsService>().SaveAsync(childEditor)).IsSuccess);
        }
        await SaveActorAsync(services, agent, parent, canRead: false);
        var deniedClient = new ScriptClient(toolName, Arguments(toolName, parent.ProjectId));
        var denied = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(fixture, services, agent, deniedClient));
        Assert.Contains("project-structure.result-disclosure-denied", Codes(denied));
        Assert.Equal(0, deniedClient.Requests);
        Assert.Equal(proposal, await ReadProposalAsync(fixture));

        await SaveActorAsync(services, agent, parent, canRead: true);
        var restoredClient = new ScriptClient(toolName, Arguments(toolName, parent.ProjectId));
        var response = await ExecuteAsync(fixture, services, agent, restoredClient);
        Assert.Contains("completed", response.ResponseText, StringComparison.Ordinal);
        Assert.Equal(1, restoredClient.Requests);
        var restoredInput = Assert.Single(restoredClient.Inputs);
        if (toolName == ProjectStructureToolPolicy.ProjectStructureHierarchyGet) {
            Assert.Contains("Original related child", restoredInput, StringComparison.Ordinal);
            Assert.DoesNotContain("Human edited related child", restoredInput, StringComparison.Ordinal);
            Assert.Equal("Human edited related child", (await services.GetRequiredService<ProjectsService>().GetAsync(child.ProjectId)).Name);
        } else {
            Assert.Contains("Original parent", restoredInput, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("Human edited parent", restoredInput, StringComparison.Ordinal);
        Assert.Equal("Human edited parent", (await services.GetRequiredService<ProjectsService>().GetAsync(parent.ProjectId)).Name);
        Assert.Equal(proposal, await ReadProposalAsync(fixture));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_reused_direct_or_related_project_id_cannot_authorize_an_original_MAF_result(bool related) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var parent = await CreateProjectAsync(services, "Stable parent");
        var child = await CreateProjectAsync(services, "Retired child");
        var projects = services.GetRequiredService<ProjectsService>();
        Assert.True((await projects.AddSubprojectAsync(parent.ProjectId, child.ProjectId)).IsSuccess);
        await using var fixture = await CreateJournalAsync(services, parent.ProjectId);
        var agent = await SaveActorAsync(services, fixture.Agent, parent, canRead: true);
        const string toolName = ProjectStructureToolPolicy.ProjectStructureHierarchyGet;
        var checkpointFailure = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(fixture, services, agent,
            new(toolName, Arguments(toolName, parent.ProjectId), failAfterResult: true)));
        AgentToolAdmissionJournalFixture.RequireScriptedFault(checkpointFailure,
            "Injected final provider acknowledgement loss after the saved tool result.");
        var saved = await ReadProposalAsync(fixture);
        var original = related ? child : parent;
        await projects.DeleteAsync(original.ProjectId);
        Assert.True((await projects.CreateAsync(original.ProjectId, new() { Name = "Unrelated replacement" })).IsSuccess);
        var replacement = (await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(original.ProjectId))!;
        Assert.NotEqual(original.LifetimeId, replacement.LifetimeId);
        if (!related) {
            agent = await SaveActorAsync(services, agent, replacement, canRead: true);
        }
        var next = new ScriptClient(toolName, Arguments(toolName, parent.ProjectId));
        var denied = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(fixture, services, agent, next));
        Assert.Contains("project-structure.result-disclosure-denied", Codes(denied));
        Assert.Equal(0, next.Requests);
        Assert.Equal(saved, await ReadProposalAsync(fixture));
        Assert.Contains(original, ProjectStructureDisclosureEvidenceCodec.Read(saved.DisclosureEvidence!).Targets);
        Assert.Equal("Unrelated replacement", (await projects.GetAsync(original.ProjectId)).Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_owner_read_is_bracketed_and_cannot_stamp_a_lifetime_created_after_its_result(bool recreate) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var parent = await CreateProjectAsync(services, "Read before replacement");
        var child = await CreateProjectAsync(services, "Old projected child");
        Assert.True((await services.GetRequiredService<ProjectsService>().AddSubprojectAsync(parent.ProjectId, child.ProjectId)).IsSuccess);
        await using var fixture = await CreateJournalAsync(services, parent.ProjectId);
        var agent = await SaveActorAsync(services, fixture.Agent, parent, canRead: true);
        var disclosure = Disclosure(services, fixture);
        var context = Context(fixture, agent, parent.ProjectId);
        var journal = fixture.NewJournal(fixture.NewStore());
        AgentToolProposalRecord saved;
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            var claim = await ClaimReadAsync(journal, lease, ProjectStructureToolPolicy.ProjectStructureHierarchyGet);
            var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            var function = AIFunctionFactory.Create(async (Guid projectId, CancellationToken cancellationToken) => {
                calls++;
                var result = await services.GetRequiredService<ProjectStructureAgentService>().GetHierarchyAsync(projectId, cancellationToken);
                ProjectStructureResultEvidenceScope.RecordResult(result);
                reached.SetResult();
                await release.Task.WaitAsync(cancellationToken);
                return result;
            }, ProjectStructureToolPolicy.ProjectStructureHierarchyGet);
            var wrapped = Assert.IsAssignableFrom<AIFunction>(Assert.Single(disclosure.Wrap(context, [function], null)));
            using var effect = AgentToolInvocationEffectScope.Begin();
            var invocation = wrapped.InvokeAsync(new() { ["projectId"] = parent.ProjectId }).AsTask();
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            try {
                await using var independent = application.Services.CreateAsyncScope();
                var projects = independent.ServiceProvider.GetRequiredService<ProjectsService>();
                await projects.DeleteAsync(child.ProjectId);
                if (recreate) {
                    Assert.True((await projects.CreateAsync(child.ProjectId, new() { Name = "New unrelated child" })).IsSuccess);
                }
            } finally {
                release.TrySetResult();
            }
            var result = await invocation;
            Assert.Contains("Old projected child", JsonSerializer.Serialize(result), StringComparison.Ordinal);
            Assert.Equal(1, calls);
            var evidence = ProjectStructureDisclosureEvidenceCodec.Read(effect.DisclosureEvidence!);
            Assert.Equal(ProjectStructureDisclosureState.LifetimeChangedDuringOperation, evidence.State);
            Assert.Contains(child, evidence.Targets);
            await journal.CompleteInvocationAsync(claim, AgentToolAdmissionJournalFixture.Envelope(JsonSerializer.Serialize(result)),
                AgentToolEffectState.None, default, disclosureEvidence: effect.DisclosureEvidence);
            saved = Assert.Single((await journal.ReadAsync(lease, default)).Batches[0].Proposals);
        }
        await using var restarted = await fixture.NewJournal(fixture.NewStore()).AcquireRunAsync(fixture.Session, default);
        using var rebound = restarted.Bind();
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => disclosure.AuthorizeAsync(context,
            Disclosure(saved), CancellationToken.None).AsTask());
        Assert.Equal("tool-admission.disclosure-authorization-unavailable", failure.Code);
        Assert.Equal(saved, await ReadProposalAsync(fixture));
    }

    [Fact]
    public async Task Actual_creation_coordinator_binds_the_reserved_lifetime_to_the_committed_file_result() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var parent = await CreateProjectAsync(services, "Source parent");
        await using var fixture = await CreateJournalAsync(services, parent.ProjectId);
        var agent = await SaveActorAsync(services, fixture.Agent, parent, canRead: true, canCreate: true);
        var coordinator = services.GetRequiredService<ProjectStructureAgentProjectCreationCoordinator>();
        var owner = services.GetRequiredService<ProjectsService>();
        var disclosure = Disclosure(services, fixture);
        var context = Context(fixture, agent, parent.ProjectId);
        var journal = fixture.NewJournal(fixture.NewStore());
        ProjectWriteAdmission created;
        AgentToolProposalRecord saved;
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            var claim = await ClaimReadAsync(journal, lease, ProjectStructureToolPolicy.ProjectStructureProjectCreate, mutation: true);
            var function = AIFunctionFactory.Create(async (CancellationToken cancellationToken) => {
                var id = await coordinator.CreateAsync(agent, async (reservation, token) => {
                    var result = await owner.CreateAsync(reservation, new() { Name = "Created once" }, token);
                    Assert.True(result.IsSuccess);
                    return result.Value;
                }, id => id, cancellationToken);
                var result = new ProjectSummary(id, "Created once", ProjectStatus.Draft, string.Empty, 0, 0, 0, DateTimeOffset.UtcNow);
                ProjectStructureResultEvidenceScope.RecordResult(result);
                return result;
            }, ProjectStructureToolPolicy.ProjectStructureProjectCreate);
            using var effect = AgentToolInvocationEffectScope.Begin();
            var wrapped = Assert.IsAssignableFrom<AIFunction>(Assert.Single(disclosure.Wrap(context, [function], null)));
            var result = await wrapped.InvokeAsync(new());
            var evidence = ProjectStructureDisclosureEvidenceCodec.Read(effect.DisclosureEvidence!);
            Assert.Equal(ProjectStructureDisclosureState.Complete, evidence.State);
            created = Assert.Single(evidence.Targets, target => target.ProjectId != parent.ProjectId);
            Assert.Equal(created, await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(created.ProjectId));
            await journal.CompleteInvocationAsync(claim, AgentToolAdmissionJournalFixture.Envelope(JsonSerializer.Serialize(result)),
                AgentToolEffectState.Unknown, default, disclosureEvidence: effect.DisclosureEvidence);
            saved = Assert.Single((await journal.ReadAsync(lease, default)).Batches[0].Proposals);
        }
        await using var next = await fixture.NewJournal(fixture.NewStore()).AcquireRunAsync(fixture.Session, default);
        using var reentry = next.Bind();
        await using (await disclosure.AuthorizeAsync(context, Disclosure(saved), CancellationToken.None)) {
            Assert.Equal(saved, await ReadProposalAsync(fixture));
        }
        Assert.Equal(created, await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(created.ProjectId));
        Assert.Equal("Created once", (await owner.GetAsync(created.ProjectId)).Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Legacy_or_historical_results_remain_unavailable_without_reexecuting_or_restamping(bool historical) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var project = await CreateProjectAsync(services, "Currently readable project");
        await using var fixture = await CreateJournalAsync(services, project.ProjectId);
        var agent = await SaveActorAsync(services, fixture.Agent, project, canRead: true);
        var journal = fixture.NewJournal(fixture.NewStore());
        AgentToolProposalRecord saved;
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            var claim = await ClaimReadAsync(journal, lease, ProjectStructureToolPolicy.ProjectStructureRead);
            var evidence = historical ? ProjectStructureDisclosureEvidenceCodec.Write(new(
                ProjectStructureToolPolicy.ProjectStructureRead, project.DatabaseProfileId, agent.Id,
                ProjectStructureDisclosureState.HistoricalSourceUnavailable, [project], [project.ProjectId])) : null;
            await journal.CompleteInvocationAsync(claim, AgentToolAdmissionJournalFixture.Envelope("{\"name\":\"Old private result\"}"),
                AgentToolEffectState.None, default, disclosureEvidence: evidence);
            saved = Assert.Single((await journal.ReadAsync(lease, default)).Batches[0].Proposals);
        }
        await using var restarted = await fixture.NewJournal(fixture.NewStore()).AcquireRunAsync(fixture.Session, default);
        using var resumed = restarted.Bind();
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => Disclosure(services, fixture).AuthorizeAsync(
            Context(fixture, agent, project.ProjectId), Disclosure(saved), CancellationToken.None).AsTask());
        Assert.Equal("tool-admission.disclosure-authorization-unavailable", denied.Code);
        Assert.Equal(saved, await ReadProposalAsync(fixture));
    }

    private static async Task<ProjectWriteAdmission> CreateProjectAsync(IServiceProvider services, string name) {
        var id = Guid.NewGuid();
        Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(id, new() { Name = name })).IsSuccess);
        return (await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(id))!;
    }

    private static async Task<AgentToolAdmissionJournalFixture> CreateJournalAsync(IServiceProvider services, Guid projectId, bool requireApproval = false) {
        var profile = services.GetRequiredService<IDatabaseRuntimeState>().GetSnapshot();
        var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(profileBinding: new(profile.ActiveProfileId!.Value,
            profile.ActiveFingerprint!, new(profile.Generation)), transientContext: new("Admitted Structure scope",
                workspaceScope: WorkspaceScopeDescriptor.Project(projectId.ToString("D"))),
            storageScope: WorkspaceScopeDescriptor.Project(projectId.ToString("D")),
            configureAgent: requireApproval ? agent => agent with {
                Permissions = agent.Permissions with { RequiresApprovalForExternalCalls = true, AutoApproveExternalCallsByDefault = false }
            } : null);
        Assert.Null(await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().GetExecutionRunAsync(fixture.Session.ExecutionRunId));
        Assert.NotNull(await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId));
        return fixture;
    }

    private static async Task<AgentDefinition> SaveActorAsync(IServiceProvider services, AgentDefinition original,
        ProjectWriteAdmission project, bool canRead, bool canCreate = false) {
        var configuration = AgentProjectStructureAccessMetadata.Write(
            AgentProjectStructureAccessMetadata.Write(original.ConfigurationJson, new()), new() {
            CanRead = canRead, CanCreateProjects = canCreate,
            AllowedProjectIds = canRead ? [project.ProjectId] : [],
            AllowedProjectLifetimes = canRead ? [new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId)] : []
        });
        var agent = original with {
            IsTemplate = false, Status = AgentLifecycleStatus.Active, TemplateKey = string.Empty,
            Permissions = original.Permissions with { CanUseTools = true },
            ConfigurationJson = AgentManagedSeedCustomizationMetadata.MarkCustomized(configuration)
        };
        var catalog = services.GetRequiredService<ISandboxWorkspaceCatalogStore>();
        await catalog.UpdateCatalogAsync(current => current with {
            Agents = current.Agents.Where(item => item.Id != agent.Id).Append(agent).ToArray()
        });
        var saved = (await catalog.LoadCatalogSnapshotAsync()).Catalog.Agents.Single(item => item.Id == agent.Id);
        var savedAccess = AgentProjectStructureAccessMetadata.Read(saved.ConfigurationJson);
        Assert.Equal(canRead, savedAccess.CanRead);
        if (canRead) {
            Assert.Equal(project.ProjectId, Assert.Single(savedAccess.AllowedProjectIds));
            Assert.Equal(new AgentProjectStructureLifetime(project.DatabaseProfileId, project.ProjectId, project.LifetimeId),
                Assert.Single(savedAccess.AllowedProjectLifetimes));
        } else {
            Assert.Empty(savedAccess.AllowedProjectIds);
            Assert.Empty(savedAccess.AllowedProjectLifetimes);
        }
        await using var held = await services.GetRequiredService<IAgentCatalogReadLeaseStore>().AcquireAgentReadLeaseAsync(agent.Id);
        Assert.Equal(saved.Id, held.Agent!.Id);
        Assert.Equal(WorkspaceScopeDescriptor.Organization(project.DatabaseProfileId.ToString("N")), held.Scope);
        return saved;
    }

    private static ProjectStructureResultDisclosureService Disclosure(IServiceProvider services, AgentToolAdmissionJournalFixture fixture)
        => new(services.GetRequiredService<ProjectWriteAdmissionService>(), services.GetRequiredService<ProjectWriteSelectionQuery>(),
            services.GetRequiredService<ICanonicalRuntimeDatabase>(), services.GetRequiredService<IDatabaseRuntimeState>(),
            services.GetRequiredService<IAgentToolAdmissionVerifier>(),
            services.GetRequiredService<ISandboxWorkspaceCatalogStore>(), services.GetRequiredService<IAgentCatalogReadLeaseStore>(),
            services.GetRequiredService<IAgentExecutionAuthorityResolver>());

    private static AgentToolResultDisclosure Disclosure(AgentToolProposalRecord saved)
        => new(saved.IntentId, saved.Payload, saved.EffectState, JsonSerializer.Deserialize<JsonElement>(saved.Result!.PayloadJson), saved.DisclosureEvidence);

    private static AgentRuntimeToolProviderContext Context(AgentToolAdmissionJournalFixture fixture, AgentDefinition agent, Guid projectId)
        => new(agent, fixture.Provider, [], false, AgentRuntimeToolProviderPurpose.InteractiveChat, "structure-disclosure",
            Intent(projectId), new Dictionary<string, string>()) {
            AdmittedToolSession = fixture.Session, ToolAdmissionSupport = AgentToolAdmissionSupport.Recoverable,
            Governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(fixture.Detail.Run.MetadataJson)
        };

    private static AgentRuntimeContextIntent Intent(Guid projectId) => AgentRuntimeContextIntent.Empty with {
        Purpose = AgentRuntimeContextPurpose.InteractiveChat, SourceKind = "project-structure", SourceId = projectId.ToString("D"),
        RuntimeToolProvidersEnabled = true, WorkspaceToolsEnabled = false, ToolCapabilitiesEnabled = true
    };

    private static Dictionary<string, object?> Arguments(string toolName, Guid projectId) {
        if (toolName == ProjectStructureToolPolicy.ProjectStructureProjectsList) {
            return [];
        }
        Dictionary<string, object?> result = new() { ["projectId"] = projectId };
        if (toolName == ProjectStructureToolPolicy.ProjectStructureRead) {
            result["request"] = new ProjectStructureReadRequest(Source: ProjectStructureReadSource.CanonicalCurrent);
        } else if (toolName == ProjectStructureToolPolicy.ProjectStructureNodeWorkflowAddOptions) {
            result["nodeId"] = $"project:{projectId:D}";
            result["request"] = new ProjectStructureWorkflowAddOptionsInput(InputSettings: new() { IncludeParentSubtree = true });
        }
        return result;
    }

    private static async Task<AgentToolProposalRecord> ReadProposalAsync(AgentToolAdmissionJournalFixture fixture)
        => Assert.Single((await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!.Batches
            .SelectMany(batch => batch.Proposals));

    private static async Task<AgentToolInvocationClaim> ClaimReadAsync(AgentToolAdmissionJournal journal, AgentToolRunLease lease,
        string toolName, bool mutation = false) {
        await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
        var payload = new AgentToolPreparedPayload(toolName, 1, AgentToolProtocolEnvelope.ComputeDigest("{}"), "{}",
            mutation ? AgentToolProposalEffect.Mutation : AgentToolProposalEffect.Read,
            mutation ? AgentToolProposalRecovery.ReconcileBeforeRetry : AgentToolProposalRecovery.RevalidateAndRead);
        var admitted = await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
            [new("original-diagnostic-call", payload, false)], default);
        return await journal.ClaimInvocationAsync(lease, admitted.Batches[0].Id, "original-diagnostic-call", payload, default);
    }

    private static Task<AgentRuntimeResponse> ExecuteAsync(AgentToolAdmissionJournalFixture fixture, IServiceProvider services,
        AgentDefinition agent, ScriptClient client) {
        var projectId = Guid.Parse(AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(fixture.Detail.Run.MetadataJson)!.WorkspaceScope.Key);
        var policies = new AgentToolPolicyCatalog(ProjectStructureToolPolicy.Capabilities);
        var provider = ActivatorUtilities.CreateInstance<ProjectStructureAgentRuntimeToolProvider>(services, Disclosure(services, fixture));
        var dependencies = MafAgentRuntimeDependencies.FromServices(services);
        dependencies = dependencies with {
            ProviderAgentFactory = new ScriptAgentFactory(client), ToolPolicies = policies,
            RuntimeToolProviderComposer = new RuntimeToolProviderComposer(new RuntimeToolProviderAccessFilter(policies), policies),
            ToolAdmissionJournal = fixture.NewJournal(fixture.NewStore()),
            CapabilityDependencies = dependencies.CapabilityDependencies with {
                RuntimeToolProviders = [provider], ContextContributors = [], ToolPolicies = policies
            }
        };
        var runtime = new MafAgentRuntime(fixture.WorkspaceRoot, fixture.StorageScope, dependencies);
        var options = new AgentRuntimeExecutionOptions(null, AgentFinalizerMode.Disabled, true, 0) {
            AdmittedToolSession = fixture.Session, ToolAdmissionSupport = AgentToolAdmissionSupport.Recoverable,
            Governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(fixture.Detail.Run.MetadataJson),
            AuthorityPolicyFingerprint = "fixture-authority", ModelContextDigest = "fixture-context", ContextIntent = Intent(projectId)
        };
        var model = ManagedSeedProviderFallbacks.ResolveModel(agent, fixture.Provider);
        return runtime.ExecutionPort.ExecuteAsync(new(agent,
            fixture.Provider with {
                    ConfigurationJson = ProviderModelThinkingConfiguration.Write(fixture.Provider.ConfigurationJson, model,
                        new(model, AgentThinkingEffortSupportStatus.Supported,
                            AgentThinkingEffortControlMode.EffortLevels, [AgentReasoningEffortLevel.Medium], AgentReasoningEffortLevel.Medium))
                },
            fixture.Detail.ChatSession!, [], [], "Read the authorized project.", string.Empty,
            (_, _, _) => Task.CompletedTask, ExecutionOptions: options));
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

    private sealed class ScriptAgentFactory(ScriptClient client) : IMafProviderAgentFactory {
        public AIAgent CreateFrameworkAgent(ProviderProfile provider, string model, ChatClientAgentOptions options,
            bool frameworkManagedHistory, bool allowBackgroundResponses) {
            Assert.True(frameworkManagedHistory);
            Assert.Equal(ProviderTransportKind.Responses, provider.Transport);
            Assert.Equal(AgentThinkingEffortCapabilitySource.Configured, AgentThinkingEffortPolicy.ResolveCapability(provider, model).Source);
            Assert.Contains(options.ChatOptions!.Tools!, tool => tool.Name == client.ToolName);
            if (client.RequiresTaskApproval) {
                Assert.Equal(ProviderKind.OpenAi, provider.Kind);
                Assert.IsType<ApprovalRequiredAIFunction>(Assert.Single(options.ChatOptions.Tools!,
                    tool => tool.Name == ProjectStructureToolPolicy.ProjectTaskCreate));
            }
            return new ChatClientAgent(new MafToolAdmissionChatClient(client), options);
        }
    }

    private sealed class ScriptClient(string toolName, Dictionary<string, object?> arguments, bool failAfterResult = false,
        ProjectStructureTaskCreateRequest? followupTask = null) : IChatClient {
        internal string ToolName => toolName;
        internal bool RequiresTaskApproval => followupTask is not null;
        internal int Requests { get; private set; }
        internal List<string> Inputs { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) {
            Requests++;
            var input = messages.ToArray();
            Inputs.Add(JsonSerializer.Serialize(input, MafToolProtocolCodec.SerializationOptions));
            var results = input.SelectMany(message => message.Contents).OfType<FunctionResultContent>().ToArray();
            if (results.Length > 0) {
                if (followupTask is not null && results.All(result => result.CallId != FailureFollowupTaskCall)) {
                    return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                        [new FunctionCallContent(FailureFollowupTaskCall, ProjectStructureToolPolicy.ProjectTaskCreate,
                            new Dictionary<string, object?> { ["projectId"] = arguments["projectId"], ["request"] = followupTask })])));
                }
                if (failAfterResult) {
                    throw new IOException("Injected final provider acknowledgement loss after the saved tool result.");
                }
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "completed")));
            }
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("diagnostic-structure-read", toolName, arguments)])));
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
