using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Runtime;

internal sealed class AgentInheritedPlanningWorkspaceFixture : IAsyncDisposable {
    private readonly ProjectTaskPlanExecutionEntryTests.EntryFixture entry;
    private readonly SourcePolicy source;
    private readonly ReadBasisMode mode;

    private AgentInheritedPlanningWorkspaceFixture(ProjectTaskPlanExecutionEntryTests.EntryFixture entry, SourcePolicy source,
        AgentInheritedPlanningCoordinatorFixture.Participant participant, ReadBasisMode mode) {
        this.entry = entry;
        this.source = source;
        this.mode = mode;
        Participant = participant;
        var database = entry.Files.Sql.Services.GetRequiredService<ICanonicalRuntimeDatabase>();
        Profile = new(database.Profile.Profile.Id, database.Profile.Profile.Runtime.Fingerprint, new(database.Generation));
        source.AgentId = entry.Files.Agent.Id;
        source.Profile = Profile;
        var context = ExecutionInvocationContext.Empty with {
            SourceKind = source.SourceKind, SourceId = source.SourceId, MetadataJson = "{}",
            ProcessRunId = Guid.NewGuid().ToString("D"), ProcessStepId = Guid.NewGuid().ToString("D")
        };
        Request = new(Guid.NewGuid(), source.AgentId, "Prepare the original inherited plan.", AgentExecutionOperationId.New(), context,
            AgentPlanningOwner.ProjectTaskPlan, WorkspaceScopeDescriptor.Project(entry.Files.Sql.Project.ProjectId.ToString("D")),
            RequireParentAsync, BindAsync);
    }

    internal AgentToolProfileBinding Profile { get; }
    internal ProjectTaskPlanAgentFixture Files => entry.Files;
    internal MafPlanningProviderFixture.Wire Wire => entry.Wire;
    internal IAgentFrameworkWorkspaceService Workspace => entry.Workspace;
    internal IAgentInheritedPlanningWorkspaceService Inherited => Files.Sql.Services.GetRequiredService<IAgentInheritedPlanningWorkspaceService>();
    internal AgentInheritedPlanningCoordinatorFixture.Participant Participant { get; }
    internal AgentInheritedPlanningWorkspaceRequest Request { get; }
    internal int Bindings { get; private set; }
    internal int ParentReads { get; private set; }
    internal bool DenyParent { get; set; }
    internal bool ReadAllowed { get => source.ReadAllowed; set => source.ReadAllowed = value; }

    internal static async Task<AgentInheritedPlanningWorkspaceFixture> CreateAsync(ReadBasisMode mode = ReadBasisMode.RunSnapshot) {
        var source = new SourcePolicy();
        var participant = new AgentInheritedPlanningCoordinatorFixture.Participant(AgentInheritedPlanningTestData.Basis(AgentInheritedPlanningTestData.Create()));
        var entry = await ProjectTaskPlanExecutionEntryTests.EntryFixture.CreateAsync(configureServices: services => {
            services.AddSingleton<IAgentToolBackgroundSourcePolicy>(source);
            services.AddSingleton<IAgentInheritedPlanningParticipant>(participant);
        });
        entry.Wire.ResponseText = "{\"tasks\":[\"review\"]}";
        return new(entry, source, participant, mode);
    }

    internal Task<AgentInheritedPlanningWorkspaceResult> ExecuteAsync(AgentInheritedPlanningWorkspaceRequest? request = null)
        => Inherited.ExecuteInheritedPlanningAsync(request ?? Request with { ActivityOperationId = AgentExecutionOperationId.New() });

    internal async Task<ExecutionRunRecord> SeedInitialAsync() {
        var provider = (await Files.Sql.Services.GetRequiredService<IProviderRuntimeProfileSource>().GetProviderAsync(Files.Provider.Id))!;
        var agent = (await Files.Store.LoadCatalogSnapshotAsync()).Catalog.Agents.Single(item => item.Id == Request.AgentId);
        var runtime = Files.Sql.Services.GetRequiredService<IAgentPlanningStepRuntime>();
        var candidate = Files.Original with {
            Id = Request.ExecutionRunId, ChatSessionId = null, ToolAdmission = null, MetadataJson = "{}",
            SourceKind = Request.Context.SourceKind, SourceId = Request.Context.SourceId,
            ProcessRunId = Request.Context.ProcessRunId!, ProcessStepId = Request.Context.ProcessStepId!,
            ProviderName = provider.Name, ProviderProfileId = provider.Id, Model = agent.Model,
            InitialActivityOperationId = Request.ActivityOperationId
        };
        var journal = new AgentToolAdmissionJournal(Files.Store, Profile, backgroundSources: [source]);
        return (await new AgentInheritedPlanningRunAdmission(Files.Store, Files.Store, journal).AdmitAsync(Request.ExecutionRunId,
            Request.RequireOriginalParent, _ => Task.FromResult(new AgentInheritedPlanningRunPreparation(candidate,
                Request.OriginalInput, Request.Owner, runtime.CaptureRuntimeContract(agent, provider, Participant), Request.ContextScope, BindAsync)))).Run;
    }

    private Task RequireParentAsync(ExecutionRunRecord run, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ParentReads++;
        if (DenyParent) {
            throw new UnauthorizedAccessException("The original parent is unavailable.");
        }
        Assert.Equal(Request.ExecutionRunId, run.Id);
        Assert.Equal(Profile, run.ToolAdmission!.PlanningDecision!.InheritedOrigin!.Profile);
        return Task.CompletedTask;
    }

    private Task<AgentInheritedPlanningBinding> BindAsync(AgentInheritedPlanningBindingContext context, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        Bindings++;
        var template = AgentInheritedPlanningTestData.Create(context.Session, mode);
        var decision = template.PlanningDecision!;
        var origin = decision.InheritedOrigin! with { CapturedAtUtc = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1) };
        Participant.Basis = AgentInheritedPlanningTestData.Basis(template) with { CapturedAtUtc = origin.CapturedAtUtc };
        return Task.FromResult(new AgentInheritedPlanningBinding(decision.OriginalTarget, origin));
    }

    public ValueTask DisposeAsync() => entry.DisposeAsync();

    private sealed class SourcePolicy : IAgentToolBackgroundSourcePolicy {
        public string SourceKind => AgentInheritedPlanningTestData.SourceKind;
        internal string SourceId { get; } = Guid.NewGuid().ToString("N");
        internal Guid AgentId { get; set; }
        internal AgentToolProfileBinding? Profile { get; set; }
        internal bool ReadAllowed { get; set; } = true;
        public ValueTask<AgentToolBackgroundSourceObservation> ObserveAsync(ExecutionRunRecord execution, AgentToolProfileBinding profile,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(Profile, profile);
            Assert.Equal(AgentId, execution.AgentId);
            Assert.Equal(SourceId, execution.SourceId);
            return ValueTask.FromResult(new AgentToolBackgroundSourceObservation(
                AgentPlanningDecisionTestData.Envelope("{\"parent\":1}").Digest, ReadAllowed, true));
        }
    }
}
