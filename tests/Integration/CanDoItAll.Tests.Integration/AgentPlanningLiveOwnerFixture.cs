using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration.Runtime;

internal sealed class AgentPlanningLiveOwnerFixture : IAsyncDisposable {
    private AgentPlanningLiveOwnerFixture(AgentToolAdmissionJournalFixture files, PlanningClock clock) {
        Files = files;
        Clock = clock;
        Journal = new(files.NewStore(stage => {
            OnCommit?.Invoke(stage);
            if (Fault == stage) {
                Fault = null;
                throw new IOException("planning-publication-ack");
            }
        }), files.Profile, clock);
        Owner = new(Journal);
    }

    internal AgentToolAdmissionJournalFixture Files { get; }
    internal PlanningClock Clock { get; }
    internal AgentToolAdmissionJournal Journal { get; }
    internal AgentPlanningDecisionOwner Owner { get; }
    internal ExistingRunDetailCommitStage? Fault { get; set; }
    internal Action<ExistingRunDetailCommitStage>? OnCommit { get; set; }

    internal static async Task<AgentPlanningLiveOwnerFixture> CreateAsync(ReadBasisMode mode = ReadBasisMode.StepSnapshot, bool originalOpenAi = false,
        AgentToolProtocolEnvelope? originalTarget = null, int replans = 0, bool organizationScope = false,
        Func<AgentDefinition, ProviderProfile, AgentToolSemanticDigest>? runtimeContract = null) {
        var profile = organizationScope ? new AgentToolProfileBinding(Guid.NewGuid(), "planning-fixture-profile", new(1)) : null;
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true,
            profileBinding: profile, storageScope: profile is null ? null : WorkspaceScopeDescriptor.Organization(profile.ProfileId.ToString("N")),
            configureAgent: agent => agent with {
                ConfigurationJson = AgentThinkingEffortConfiguration.WriteAgentOverride(agent.ConfigurationJson, null)
            });
        var clock = new PlanningClock(source.Detail.Run.CreatedAtUtc);
        var draft = AgentPlanningDecisionTestData.Create(source.Detail.Run.ToolAdmission!.Session, mode, replans);
        var factory = new AgentToolAdmissionJournal(source.Store, source.Profile, clock);
        var input = source.Detail.Run.ToolAdmission.OriginalInput!;
        var journal = factory.CreateForNewPlanningRun(source.Detail.Run with { ToolAdmission = null }, source.Detail.ChatSession!,
            input, originalTarget ?? draft.PlanningDecision!.OriginalTarget, draft.PlanningDecision!.Provenance);
        var provider = originalOpenAi ? source.Provider with { Kind = ProviderKind.OpenAi, Transport = ProviderTransportKind.ChatCompletions } : source.Provider;
        var run = source.Detail.Run with { ToolAdmission = journal };
        if (runtimeContract is not null) {
            run = run with { MetadataJson = AgentPlanningRunMetadata.Apply(run.MetadataJson, AgentPlanningOwner.ProjectTaskPlan,
                runtimeContract(source.Agent, provider)) };
        }
        var files = await AgentToolAdmissionJournalFixture.RestoreRetainedAsync(source.Agent, provider, source.Profile,
            run, storageScope: source.StorageScope);
        return new(files, clock);
    }

    internal async Task<ExecutionRunRecord> ReadAsync()
        => await Files.NewStore().GetExecutionRunAsync(Files.Session.ExecutionRunId)
            ?? throw new InvalidOperationException("The planning fixture lost its run.");

    internal async Task<AgentPlanningLiveWork> StartAsync(AgentToolRunLease lease)
        => await Owner.StartStepAsync(lease, AgentPlanningDecisionExpectation.From(await ReadAsync()), default);

    internal Task CaptureAsync(AgentPlanningLiveWork work)
        => Owner.CaptureBasisAsync(work, (_, _) => Task.FromResult(AgentPlanningDecisionTestData.Envelope("{\"tasks\":[]}")), default);

    internal Task<AgentToolProviderDispatchRecord> EnterAsync(AgentPlanningLiveWork work)
        => Owner.EnterProviderAsync(work, AgentPlanningDecisionTestData.Envelope("{\"input\":1}").Digest,
            AgentPlanningDecisionTestData.Envelope("{\"output\":1}").Digest, default);

    internal Task<AgentPlanningResponseAdmission> RespondAsync(AgentPlanningLiveWork work, AgentToolProviderDispatchRecord dispatch,
        bool publish = false) => Owner.AdmitResponseAsync(work, dispatch.Id, dispatch.RequestDigest,
            AgentPlanningDecisionTestData.Envelope("{\"actualResponse\":true}"), publish ? Publication() : null, default);

    internal static AgentToolPreparedCall Publication() => new("plan-publication", new("planning_fixture_publish", 2,
        AgentPlanningDecisionTestData.Envelope().Digest, "{}", AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt,
        AgentPlanningDecisionTestData.Envelope("{\"operation\":1}")), true);

    public ValueTask DisposeAsync() => Files.DisposeAsync();

    internal sealed class PlanningClock(DateTimeOffset now) : TimeProvider {
        internal DateTimeOffset Now { get; set; } = now;
        internal long Timestamp { get; set; }
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Timestamp;
        public override DateTimeOffset GetUtcNow() => Now;
        internal void Advance(TimeSpan duration) {
            Now += duration;
            Timestamp += duration.Ticks;
        }
    }
}
