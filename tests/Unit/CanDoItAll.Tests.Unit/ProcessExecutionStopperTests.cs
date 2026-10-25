using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessExecutionStopperTests {
    [Fact]
    public async Task Selected_original_stop_and_replay_preserve_historical_profile_generation() {
        var fixture = new Fixture();
        var original = fixture.Store.Detail!.Run.ToolAdmission!;
        fixture.Generation.Value++;
        Assert.Equal(ProcessCancellationRecoveryPending.None, await fixture.StopAsync());
        var stopped = fixture.Store.Detail.Run;
        Assert.Equal(RunOutcome.Cancelled, stopped.Outcome);
        Assert.Equal(original.Session, stopped.ToolAdmission!.Session);
        Assert.Equal(3, fixture.Reads);
        Assert.Equal(4, fixture.RootReads);
        Assert.Equal(ProcessCancellationRecoveryPending.None, await fixture.StopAsync());
        Assert.Same(stopped, fixture.Store.Detail.Run);
        Assert.Equal(1, fixture.Store.Writes);
    }

    [Fact]
    public async Task Live_execution_signalling_keeps_the_runtime_outcome_pending() {
        var fixture = new Fixture();
        using var registration = fixture.Store.Registry.Register(fixture.Store.Detail!.Run, default);
        Assert.Equal(ProcessCancellationRecoveryPending.ActiveWork, await fixture.StopAsync());
        Assert.True(registration.Token.IsCancellationRequested);
        Assert.Equal(0, fixture.Store.Writes);
        Assert.Equal(0, fixture.Store.Leases);
        Assert.Equal(1, fixture.Reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_execution_or_journal_stays_pending(bool missingExecution) {
        var fixture = new Fixture();
        fixture.Store.Detail = missingExecution ? null : fixture.Store.Detail! with { Run = fixture.Store.Detail.Run with { ToolAdmission = null } };
        Assert.Equal(ProcessCancellationRecoveryPending.MissingJournal, await fixture.StopAsync());
        Assert.Equal(0, fixture.Reads);
        Assert.Equal(0, fixture.Store.Writes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Missing_barrier_before_selection_signal_lease_or_publication_refuses(int boundary) {
        var fixture = new Fixture { LoseBarrierAt = boundary };
        var original = fixture.Store.Detail;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.StopAsync());
        Assert.Same(original, fixture.Store.Detail);
        Assert.Equal(0, fixture.Store.Writes);
    }

    public enum SourceChange { Missing, Disposition, Execution, Root, Profile, Fingerprint, NoAuthority }

    [Theory]
    [InlineData(SourceChange.Missing)]
    [InlineData(SourceChange.Disposition)]
    [InlineData(SourceChange.Execution)]
    [InlineData(SourceChange.Root)]
    [InlineData(SourceChange.Profile)]
    [InlineData(SourceChange.Fingerprint)]
    [InlineData(SourceChange.NoAuthority)]
    public async Task Changed_original_receipt_cannot_authorize_the_stop(SourceChange change) {
        var fixture = new Fixture { SourceChange = change };
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.StopAsync());
        Assert.Equal(0, fixture.Store.Writes);
        Assert.Equal(0, fixture.Store.Leases);
    }

    public enum ProfileChange { Identity, Fingerprint, Generation }

    [Theory]
    [InlineData(ProfileChange.Identity, 1)]
    [InlineData(ProfileChange.Identity, 2)]
    [InlineData(ProfileChange.Identity, 3)]
    [InlineData(ProfileChange.Fingerprint, 1)]
    [InlineData(ProfileChange.Fingerprint, 2)]
    [InlineData(ProfileChange.Fingerprint, 3)]
    [InlineData(ProfileChange.Generation, 1)]
    [InlineData(ProfileChange.Generation, 2)]
    [InlineData(ProfileChange.Generation, 3)]
    public async Task Current_profile_change_during_each_receipt_observation_refuses(ProfileChange change, int boundary) {
        var fixture = new Fixture();
        fixture.AfterRead = count => {
            if (count != boundary) {
                return;
            }
            switch (change) {
                case ProfileChange.Identity:
                    fixture.Database.Profile.Profile.Id = Guid.NewGuid();
                    break;
                case ProfileChange.Fingerprint:
                    fixture.Database.Profile.Profile.Runtime.Fingerprint = "replaced-profile";
                    break;
                case ProfileChange.Generation:
                    fixture.Generation.Value++;
                    break;
            }
        };
        var original = fixture.Store.Detail;
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.StopAsync());
        Assert.Same(original, fixture.Store.Detail);
        Assert.Equal(0, fixture.Store.Writes);
        Assert.Equal(boundary, fixture.Reads);
    }

    [Fact]
    public async Task Another_selected_process_run_cannot_use_the_original_agent() {
        var fixture = new Fixture();
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Owner.StopAsync(fixture.Root, ProcessRunId.New(), fixture.Execution));
        Assert.Equal(0, fixture.Store.Writes);
    }

    [Fact]
    public async Task A_selected_journal_from_another_database_profile_is_refused() {
        var fixture = new Fixture();
        fixture.Database.Profile.Profile.Id = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.StopAsync());
        Assert.Equal(0, fixture.Reads);
        Assert.Equal(0, fixture.Store.Writes);
    }

    [Fact]
    public async Task Unproven_request_scoped_provider_work_keeps_its_pending_reason() {
        var fixture = new Fixture();
        var run = fixture.Store.Detail!.Run;
        fixture.Store.Detail = fixture.Store.Detail with { Run = run with { ToolAdmission = run.ToolAdmission! with {
            Support = AgentToolAdmissionSupport.RequestScopedInput
        } } };
        Assert.Equal(ProcessCancellationRecoveryPending.UnresolvedEffects, await fixture.StopAsync());
        Assert.Equal(0, fixture.Store.Writes);
    }

    private sealed class Fixture : IProcessOriginalReceiptAuthorityReader, IProcessRootCancellationBarrierReader {
        private const string Fingerprint = "original-process-receipt";
        private readonly ProcessLaunchAuthority source;

        internal Fixture() {
            var step = ProcessStepInstanceId.New();
            var run = Store.Detail!.Run with { Id = Execution.Value, SourceKind = "process-step", ProcessRunId = Run.ToString(),
                ProcessStepId = step.ToString(), CorrelationId = Run.ToString(), CausationId = step.ToString(), RequestedBy = "process-runtime",
                MetadataJson = JsonSerializer.Serialize(new Dictionary<string, string> { [ProcessDispatchClaimExecutionMetadata.MetadataKey] = Guid.NewGuid().ToString("D") }),
                ToolAdmission = null };
            var binding = new AgentToolBackgroundSourceBinding(run.SourceKind, run.SourceId,
                AgentToolProtocolEnvelope.ComputeDigest(Fingerprint), AgentBackgroundExecutionIdentity.Fingerprint(run));
            var session = new AgentToolSessionAdmission(new(run.Id, Guid.Empty, default, binding), run.AgentId,
                AgentRuntimeContextPurpose.GovernedProcessAutomation, new(Database.Profile.Profile.Id,
                    Database.Profile.Profile.Runtime.Fingerprint, Generation.GetGeneration()));
            Store.Detail = new(run with { ToolAdmission = new(AgentToolJournalRecord.BackgroundSchemaVersion, 1, session, [], [],
                BackgroundInput: new("Original Process input")) }, null, [], []);
            source = new(new ProcessLaunchPrincipal.LocalOperator(ProcessLaunchOperatorSurface.UserInterface),
                Database.Profile.Profile.Id, null, true, true, "original-source");
            Owner = new(Store, new(Store, Store, Store, Store.Registry, Store.Clock), this, this, Database, Generation);
        }

        internal ProcessRunId Root { get; } = ProcessRunId.New();
        internal ProcessRunId Run { get; } = ProcessRunId.New();
        internal ProcessExecutionRunId Execution { get; } = ProcessExecutionRunId.New();
        internal AgentInheritedParentStopFixture Store { get; } = new();
        internal CurrentDatabase Database { get; } = new();
        internal CurrentGeneration Generation { get; } = new();
        internal AgentFrameworkProcessExecutionStopper Owner { get; }
        internal int Reads { get; private set; }
        internal int RootReads { get; private set; }
        internal int LoseBarrierAt { get; init; }
        internal SourceChange? SourceChange { get; init; }
        internal Action<int>? AfterRead { get; set; }
        internal Task<ProcessCancellationRecoveryPending> StopAsync() => Owner.StopAsync(Root, Run, Execution);

        public Task<bool> IsStoppedAsync(ProcessRunId rootRunId, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(Root, rootRunId);
            return Task.FromResult(++RootReads != LoseBarrierAt);
        }

        public Task<ProcessExecutionDispatchAuthorityResult> ObserveOriginalForReceiptReadAsync(ProcessExecutionClaimEvidence evidence,
            CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            Reads++;
            var snapshot = new ProcessExecutionDispatchAuthority(evidence, Root, null, Fingerprint, "original-readiness", [],
                "original-target", ProcessCapabilityScope.Empty, source, null, false, Store.Clock.GetUtcNow());
            snapshot = SourceChange switch {
                ProcessExecutionStopperTests.SourceChange.Execution => snapshot with { Evidence = evidence with { ExecutionRunId = Guid.NewGuid() } },
                ProcessExecutionStopperTests.SourceChange.Root => snapshot with { RootRunId = ProcessRunId.New() },
                ProcessExecutionStopperTests.SourceChange.Profile => snapshot with { SourceAuthority = source with { DatabaseProfileId = Guid.NewGuid() } },
                ProcessExecutionStopperTests.SourceChange.Fingerprint => snapshot with { OwnerFingerprint = "rebound-process" },
                ProcessExecutionStopperTests.SourceChange.NoAuthority => snapshot with { SourceAuthority = null },
                _ => snapshot
            };
            AfterRead?.Invoke(Reads);
            return Task.FromResult(new ProcessExecutionDispatchAuthorityResult(SourceChange == ProcessExecutionStopperTests.SourceChange.Disposition
                ? ProcessExecutionAuthorityDisposition.DispatchBindingChanged : ProcessExecutionAuthorityDisposition.Bound,
                SourceChange == ProcessExecutionStopperTests.SourceChange.Missing ? null : snapshot));
        }

        public Task<ProcessExecutionDispatchAuthorityResult> ObserveOriginalDecisionForReceiptReadAsync(ProcessExecutionClaimEvidence original,
            ProcessDecisionChildMaterialBinding binding, ProcessDecisionExecutionContract execution,
            ProcessDecisionContentContract? content, CancellationToken cancellationToken) => throw new InvalidOperationException("Ordinary recovery cannot enter inherited planning.");
    }

    private sealed class CurrentDatabase : ICanonicalRuntimeDatabase {
        public ResolvedDatabaseProfile Profile { get; } = new(new() { Id = Guid.NewGuid(), Runtime = new() { Fingerprint = "original-profile" } },
            DatabaseProfileResolutionSource.ExplicitOverride, "in-memory:process-stop");
        public long Generation => 1;
    }

    private sealed class CurrentGeneration : IAgentExecutionProfileGenerationSource {
        internal long Value = 1;
        public DatabaseProfileGeneration GetGeneration() => new(Value);
    }
}
