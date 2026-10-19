using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Integration.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

internal sealed class ProjectTaskPlanAgentFixture : IAsyncDisposable {
    private readonly HttpClient client;
    private readonly MafPlanningStepRuntime runtime;
    private readonly AgentToolSemanticDigest runtimeContract;
    private ProjectTaskPlanAgentFixture(ProjectTaskPlanOperationFixture sql, FileSandboxWorkspaceStore store,
        AgentToolProfileBinding profile, AgentDefinition agent, ProviderProfile provider, ExecutionRunRecord original,
        AgentPlanningLiveOwnerFixture.PlanningClock clock) {
        Sql = sql;
        Store = store;
        Agent = agent;
        Provider = provider;
        Original = original;
        Clock = clock;
        Journal = new(store, profile, clock);
        Owner = new(Journal);
        Participant = sql.Services.GetRequiredService<ProjectTaskPlanAgentParticipant>();
        Wire = new() { ResponseText = Draft };
        client = new(Wire);
        Wire.Client = client;
        runtime = new(new MafProviderAgentFactory(Wire, NoOpMafProviderStreamingDispatchGate.Instance, Wire,
            httpClientSelector: Wire, clock: clock), NullLogger<MafPlanningStepRuntime>.Instance);
        runtimeContract = runtime.CaptureRuntimeContract(agent, provider, Participant);
    }

    internal const string Draft = """{"version":1,"tasks":[{"title":"Review concurrent edits","description":"Check stale basis","notes":"Preserve the original intent","priority":3}]}""";
    internal ProjectTaskPlanOperationFixture Sql { get; }
    internal FileSandboxWorkspaceStore Store { get; }
    internal AgentDefinition Agent { get; }
    internal ProviderProfile Provider { get; }
    internal ExecutionRunRecord Original { get; }
    internal AgentPlanningLiveOwnerFixture.PlanningClock Clock { get; }
    internal AgentToolAdmissionJournal Journal { get; }
    internal AgentPlanningDecisionOwner Owner { get; }
    internal ProjectTaskPlanAgentParticipant Participant { get; }
    internal MafPlanningProviderFixture.Wire Wire { get; }
    internal AgentToolSessionReference Session => Original.ToolAdmission!.Session.Reference;

    internal static async Task<ProjectTaskPlanAgentFixture> CreateAsync(ReadBasisMode mode = ReadBasisMode.StepSnapshot,
        int replans = 0, bool projectContext = false, Action<IServiceCollection>? configureServices = null) {
        var sql = await ProjectTaskPlanOperationFixture.CreateAsync(configureServices: configureServices);
        try {
            var services = sql.Services;
            var database = services.GetRequiredService<ICanonicalRuntimeDatabase>();
            var profile = new AgentToolProfileBinding(database.Profile.Profile.Id, database.Profile.Profile.Runtime.Fingerprint, new(database.Generation));
            var scope = WorkspaceScopeDescriptor.Organization(profile.ProfileId.ToString("N"));
            var store = new FileSandboxWorkspaceStore(services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(), scope,
                new AgentProjectAccessCatalogPolicy(services.GetRequiredService<ProjectWriteAdmissionService>(), profile.ProfileId));
            await using var seed = await AgentToolAdmissionJournalFixture.CreateAsync(profileBinding: profile, includeRecoveryInput: true,
                configureAgent: agent => agent with {
                    Id = Guid.NewGuid(), Name = "Concurrent task planning fixture", TemplateKey = string.Empty,
                    Permissions = agent.Permissions with { CanUseTools = true },
                    ConfigurationJson = AgentThinkingEffortConfiguration.WriteAgentOverride(
                        AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson,
                            new() { CanRead = true, CanWriteTasks = true, AllowAllProjects = true }), null)
                });
            var provider = seed.Provider with { Kind = ProviderKind.OpenAi, Transport = ProviderTransportKind.ChatCompletions };
            await store.UpdateCatalogAsync(current => current with {
                Agents = current.Agents.Where(item => item.Id != seed.Agent.Id).Append(seed.Agent).ToArray(),
                Providers = current.Providers.Where(item => item.Id != provider.Id).Append(provider).ToArray()
            });
            await using (var admittedAgent = await services.GetRequiredService<IAgentCatalogReadLeaseStore>()
                .AcquireAgentReadLeaseAsync(seed.Agent.Id)) {
                Assert.NotNull(admittedAgent.Agent);
                var access = AgentProjectStructureAccessMetadata.Read(admittedAgent.Agent.ConfigurationJson);
                Assert.True(access.CanRead && access.CanWriteTasks && access.AllowAllProjects);
            }
            var clock = new AgentPlanningLiveOwnerFixture.PlanningClock(DateTimeOffset.UtcNow);
            var executionScope = projectContext ? WorkspaceScopeDescriptor.Project(sql.Project.ProjectId.ToString("D")) : scope;
            var authority = new AgentExecutionAuthorityRecord(AgentExecutionAuthorityId.Create(), seed.Agent.Id,
                profile.ProfileId, profile.Generation, executionScope, true, true, "planning-integration", "original-task-plan", clock.GetUtcNow(),
                schemaVersion: projectContext ? AgentExecutionAuthorityRecord.CurrentSchemaVersion : AgentExecutionAuthorityRecord.LegacySchemaVersion,
                sourceProjectLifetime: projectContext ? new(sql.Project.DatabaseProfileId, sql.Project.ProjectId, sql.Project.LifetimeId) : null);
            var turnContext = projectContext ? new AgentTurnContextReference(new(Guid.NewGuid()), new(Guid.NewGuid()),
                new(AgentChatTrustedSourceKinds.ProjectStructure), new(sql.Project.ProjectId.ToString("D")),
                "agents", "chat", 1, "planning-original-project", clock.GetUtcNow())
                : AgentTurnContextMetadata.TryReadTurnContextReference(seed.Detail.Run.MetadataJson)!;
            var run = seed.Detail.Run with {
                ToolAdmission = null, MetadataJson = AgentTurnContextMetadata.Apply("{}", turnContext, authority)
            };
            var journal = new AgentToolAdmissionJournal(store, profile, clock);
            var input = seed.Detail.Run.ToolAdmission!.OriginalInput!;
            var admission = journal.CreateForNewRun(run, seed.Detail.ChatSession!, originalInput: input);
            var participant = services.GetRequiredService<ProjectTaskPlanAgentParticipant>();
            var target = await participant.CaptureTargetAsync(admission.Session,
                AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson)!, sql.Project, default);
            var policy = new DecisionDataPolicy(mode, PublicationFreshness.RequireCurrent,
                replans == 0 ? OnBasisChange.PauseForReview : OnBasisChange.Replan,
                new(replans, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10)));
            var provenance = new AgentPlanningPolicyProvenance(sql.Binding.Namespace.Value, Guid.NewGuid(), seed.Agent.Id,
                (await store.LoadCatalogSnapshotAsync()).Catalog.CatalogDataRevision.Value, policy, null, null, ProjectTaskPlanAgentParticipant.Capabilities, []);
            run = run with { ToolAdmission = journal.CreateForNewPlanningRun(run, seed.Detail.ChatSession!, input, target, provenance) };
            var saved = await store.SaveExecutionRunDetailAsync(new(run, seed.Detail.ChatSession, [], []));
            return new(sql, store, profile, seed.Agent, provider, saved.Run, clock);
        } catch {
            await sql.DisposeAsync();
            throw;
        }
    }

    internal async Task<ExecutionRunRecord> ReadAsync()
        => await Store.GetExecutionRunAsync(Session.ExecutionRunId) ?? throw new InvalidOperationException();

    internal async Task<ExecutionRunRecord> PrepareAsync() {
        await using var lease = await Journal.AcquireRunAsync(Session, default);
        using var bound = lease.Bind();
        var work = await Owner.StartStepAsync(lease, AgentPlanningDecisionExpectation.From(await ReadAsync()), default);
        await Owner.CaptureBasisAsync(work, Participant.CaptureBasisAsync, default);
        var response = await runtime.RunAsync(new(Agent, Provider, Owner, work, Participant, runtimeContract));
        Assert.Null(response.OutputRefusal);
        Assert.Null(response.Admission.Refusal);
        var run = response.Admission.Run;
        var selected = AgentToolJournalTransitions.RequireProposal(run.ToolAdmission!, Binding(run));
        Clock.Advance(TimeSpan.FromSeconds(2));
        return await Owner.PauseForApprovalAsync(work, new(Guid.NewGuid().ToString("D"), selected.CallId,
            selected.Payload.ToolName, "function", "Review the original task plan.", selected.Payload.ArgumentsJson), default);
    }

    internal MafSavedEffectDispatcher Dispatcher() {
        var services = Sql.Services;
        var dependencies = MafAgentRuntimeDependencies.FromServices(services);
        return new(services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(),
            WorkspaceScopeDescriptor.Organization(Sql.Project.DatabaseProfileId.ToString("N")),
            services.GetRequiredService<CanDoItAll.Infrastructure.FileSystem.IPhysicalFileSystemPathPolicyFactory>(),
            services.GetRequiredService<IWorkspaceRuntimeServicesFactory>(), Journal,
            services.GetRequiredService<IAgentCatalogReadLeaseStore>(), services.GetServices<IAgentRuntimeToolProvider>().ToArray(),
            services.GetServices<IAgentToolReceiptReconciliationProvider>().ToArray(),
            dependencies.ToolInvocationPolicyPipeline, dependencies.ToolPolicies,
            NullLogger<MafSavedEffectDispatcher>.Instance);
    }

    internal Task<AgentToolRunLease> ResumeAsync(ExecutionRunRecord run)
        => Owner.ResumeSavedEffectAsync(new(run.Id, run.ChatSessionId, run.Revision, run.ToolAdmission!.Revision,
            Binding(run), run.PendingApprovals.Select(approval => new PendingToolApprovalDecision(approval.ApprovalId, true) {
                ToolAdmission = approval.ToolAdmission
            }).ToArray()), AgentPlanningDecisionExpectation.From(run), Participant.CanReadAsync, default);

    internal async Task<AgentSavedEffectDispatchResult> PublishAsync(ExecutionRunRecord waiting, CancellationToken token = default) {
        await using var lease = await ResumeAsync(waiting);
        using var bound = lease.Bind();
        var result = await Dispatcher().DispatchAsync(new(Session, Binding(waiting)), token);
        var outcome = Result(result);
        if (outcome.Refusal == ProjectTaskPlanRefusal.BasisChanged && result.EffectState == AgentToolEffectState.NotCommitted) {
            await Owner.PauseForBasisChangeAsync(lease.PlanningWork!, result.Binding, default);
        } else {
            await Owner.SettleTerminalAsync(lease.PlanningWork!, result.EffectState == AgentToolEffectState.Committed
                ? AgentPlanningGenerationState.Completed : AgentPlanningGenerationState.Failed, default);
        }
        return result;
    }

    internal static AgentToolApprovalBinding Binding(ExecutionRunRecord run)
        => run.ToolAdmission!.PlanningDecision!.Current.Steps[^1].Publications.Single().Proposal;
    internal static ProjectTaskPlanAgentResult Result(AgentSavedEffectDispatchResult result)
        => ProjectTaskPlanAgentCodec.Read<ProjectTaskPlanAgentResult>(Assert.IsType<AgentToolProtocolEnvelope>(result.OwnerResult),
            ProjectTaskPlanAgentCodec.ResultFormat, 65_536);

    public async ValueTask DisposeAsync() {
        client.Dispose();
        await Sql.DisposeAsync();
    }
}
