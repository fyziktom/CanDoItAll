using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration.Runtime;

internal sealed class AgentInheritedPlanningFixture : IAsyncDisposable {
    private AgentInheritedPlanningFixture(AgentToolAdmissionJournalFixture files, ExecutionRunRecord run,
        AgentPlanningLiveOwnerFixture.PlanningClock clock, SourcePolicy policy) {
        Files = files;
        Original = run;
        Clock = clock;
        Policy = policy;
        Journal = NewJournal(files.NewStore(stage => {
            if (Fault == stage) {
                Fault = null;
                throw new IOException("inherited-planning-ack");
            }
        }));
        Owner = new(Journal);
    }

    internal AgentToolAdmissionJournalFixture Files { get; }
    internal ExecutionRunRecord Original { get; }
    internal AgentPlanningLiveOwnerFixture.PlanningClock Clock { get; }
    internal SourcePolicy Policy { get; }
    internal AgentToolAdmissionJournal Journal { get; }
    internal AgentPlanningDecisionOwner Owner { get; }
    internal ExistingRunDetailCommitStage? Fault { get; set; }
    internal AgentPlanningBasisRecord Basis => AgentInheritedPlanningTestData.Basis(Original.ToolAdmission!);
    internal AgentToolAdmissionJournal NewJournal(FileSandboxWorkspaceStore? store = null)
        => new(store ?? Files.NewStore(), Files.Profile, Clock, backgroundSources: [Policy]);

    internal static async Task<AgentInheritedPlanningFixture> CreateAsync(ReadBasisMode parentMode = ReadBasisMode.StepSnapshot,
        TimeSpan? basisAge = null, WorkspaceScopeDescriptor? scope = null) {
        var files = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true,
            storageScope: scope,
            configureAgent: agent => agent with {
                ConfigurationJson = AgentThinkingEffortConfiguration.WriteAgentOverride(agent.ConfigurationJson, null)
            });
        try {
            var clock = new AgentPlanningLiveOwnerFixture.PlanningClock(files.Detail.Run.CreatedAtUtc);
            var policy = new SourcePolicy(files.Agent.Id, files.Profile);
            var candidate = files.Detail.Run with {
                Id = Guid.NewGuid(), ChatSessionId = null, ToolAdmission = null,
                MetadataJson = AgentPlanningRunMetadata.ApplyInherited("{}", AgentPlanningOwner.ProjectTaskPlan,
                    AgentPlanningDecisionTestData.Envelope().Digest, files.StorageScope),
                SourceKind = policy.SourceKind, SourceId = policy.SourceId,
                ProcessRunId = Guid.NewGuid().ToString("D"), ProcessStepId = Guid.NewGuid().ToString("D")
            };
            var factory = new AgentInheritedPlanningAdmission(new(files.NewStore(), files.Profile, clock, backgroundSources: [policy]));
            var journal = await factory.CreateAsync(candidate, "Prepare a bounded task plan.", AgentPlanningOwner.ProjectTaskPlan, (binding, _) => {
                var draft = AgentInheritedPlanningTestData.Create(binding.Session, parentMode).PlanningDecision!;
                return Task.FromResult(new AgentInheritedPlanningBinding(draft.OriginalTarget,
                    draft.InheritedOrigin! with { CapturedAtUtc = clock.Now - (basisAge ?? TimeSpan.FromMinutes(1)) }));
            });
            var saved = await files.Store.SaveExecutionRunDetailAsync(new(candidate with { ToolAdmission = journal }, null, [], []));
            return new(files, saved.Run, clock, policy);
        } catch {
            await files.DisposeAsync();
            throw;
        }
    }

    internal async Task<ExecutionRunRecord> ReadAsync()
        => await Files.NewStore().GetExecutionRunAsync(Original.Id)
            ?? throw new InvalidOperationException("The inherited fixture lost its original execution.");

    internal async Task<AgentToolRunLease> StartAsync()
        => await Owner.StartInheritedAsync(AgentPlanningDecisionExpectation.From(await ReadAsync()), AllowRead, default);

    internal Task<ExecutionRunRecord> InheritAsync(AgentPlanningLiveWork work)
        => Owner.InheritBasisAsync(work, (_, _) => Task.FromResult(Basis), default);

    internal Task<AgentToolProviderDispatchRecord> EnterAsync(AgentPlanningLiveWork work)
        => Owner.EnterProviderAsync(work, Basis.Value.Digest, AgentPlanningDecisionTestData.Envelope().Digest, default);

    internal Task<AgentPlanningResponseAdmission> RespondAsync(AgentPlanningLiveWork work, AgentToolProviderDispatchRecord dispatch)
        => Owner.AdmitResponseAsync(work, dispatch.Id, dispatch.RequestDigest,
            AgentPlanningDecisionTestData.Envelope("{\"response\":1}"), null, default);

    internal static ValueTask<bool> AllowRead(AgentToolSessionObservation _, CancellationToken token) {
        token.ThrowIfCancellationRequested();
        return ValueTask.FromResult(true);
    }

    public ValueTask DisposeAsync() => Files.DisposeAsync();

    internal sealed class SourcePolicy(Guid agentId, AgentToolProfileBinding originalProfile) : IAgentToolBackgroundSourcePolicy {
        public string SourceKind => AgentInheritedPlanningTestData.SourceKind;
        internal string SourceId { get; } = Guid.NewGuid().ToString("N");
        internal bool ReadAllowed { get; set; } = true;
        internal bool DispatchAllowed { get; set; } = true;
        internal AgentToolSemanticDigest Fingerprint { get; set; } = AgentPlanningDecisionTestData.Envelope("{\"owner\":1}").Digest;

        public ValueTask<AgentToolBackgroundSourceObservation> ObserveAsync(ExecutionRunRecord execution, AgentToolProfileBinding profile,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(agentId, execution.AgentId);
            Assert.Equal(originalProfile, profile);
            Assert.Equal(SourceId, execution.SourceId);
            return ValueTask.FromResult(new AgentToolBackgroundSourceObservation(Fingerprint, ReadAllowed, DispatchAllowed));
        }
    }
}
