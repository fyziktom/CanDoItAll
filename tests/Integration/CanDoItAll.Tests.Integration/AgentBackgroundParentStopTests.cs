using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using BackgroundFixture = CanDoItAll.Tests.Integration.Runtime.AgentToolBackgroundJournalIntegrationTests.Fixture;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class AgentBackgroundParentStopTests {
    [Fact]
    public async Task Module_registration_uses_the_selected_scoped_store_and_shared_cancellation_registry() {
        await using var fixture = await BackgroundFixture.CreateAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAgentFrameworkModule(new ConfigurationBuilder().Build());
        services.RemoveAll<ISandboxWorkspaceStore>();
        services.AddScoped<ISandboxWorkspaceStore>(_ => fixture.Source.NewStore());
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<AgentBackgroundExecutionStopper>();
        var registry = scope.ServiceProvider.GetRequiredService<IAgentExecutionCancellationRegistry>();
        var original = await Read(fixture);
        using (var registration = registry.Register(original, default)) {
            Assert.Equal(AgentParentStopOutcome.Signalled, await owner.StopAsync(original.Id, Parent(original)));
            Assert.True(registration.Token.IsCancellationRequested);
        }
        Assert.Equal(AgentParentStopOutcome.Stopped, await owner.StopAsync(original.Id, Parent(original)));
        await using var restartedScope = provider.CreateAsyncScope();
        var restarted = restartedScope.ServiceProvider.GetRequiredService<AgentBackgroundExecutionStopper>();
        Assert.NotSame(owner, restarted);
        Assert.Equal(AgentParentStopOutcome.AlreadyClosed, await restarted.StopAsync(original.Id, Parent(original)));
        Assert.Equal(RunOutcome.Cancelled, (await Read(fixture)).Outcome);
    }

    [Fact]
    public async Task Admitted_but_unstarted_background_run_stops_and_replays_after_store_restart() {
        await using var fixture = await BackgroundFixture.CreateAsync();
        var original = await Read(fixture);
        var parent = Parent(original);
        Assert.Equal(AgentParentStopOutcome.Stopped, await Owner(fixture).StopAsync(original.Id, parent));
        var stopped = await Read(fixture);
        Assert.Equal(RunOutcome.Cancelled, stopped.Outcome);
        Assert.Equal(original.ToolAdmission!.Session, stopped.ToolAdmission!.Session);
        Assert.Equal(original.ToolAdmission.BackgroundInput, stopped.ToolAdmission.BackgroundInput);
        Assert.Equal(original.Revision + 1, stopped.Revision);
        var retained = JsonSerializer.Serialize(stopped);
        Assert.Equal(AgentParentStopOutcome.AlreadyClosed, await Owner(fixture).StopAsync(original.Id, parent));
        Assert.Equal(retained, JsonSerializer.Serialize(await Read(fixture)));
    }

    [Fact]
    public async Task An_admitted_provider_response_is_retained_while_its_undispatched_mutation_is_cancelled() {
        await using var fixture = await BackgroundFixture.CreateAsync();
        var journal = fixture.Journal();
        await using (var lease = await journal.AcquireRunAsync(fixture.Reference, default)) {
            var saved = await journal.BeginSegmentAsync(lease, Envelope(), null, default);
            var dispatch = await journal.BeginProviderDispatchAsync(lease, saved.Segments[0].Id, Envelope().Digest, Envelope().Digest, [], default);
            var payload = AgentToolAdmissionJournalFixture.Payload();
            await journal.CompleteProviderDispatchAsync(lease, dispatch.Id, Envelope().Digest, Envelope("{\"response\":true}"),
                [new("never-dispatched", payload, true)], default);
        }
        var original = await Read(fixture);
        Assert.Equal(AgentParentStopOutcome.Stopped, await Owner(fixture).StopAsync(original.Id, Parent(original)));
        var stopped = await Read(fixture);
        var before = original.ToolAdmission!;
        var after = stopped.ToolAdmission!;
        Assert.Equal(JsonSerializer.Serialize(before.ProviderDispatches), JsonSerializer.Serialize(after.ProviderDispatches));
        Assert.Equal(before.BackgroundInput, after.BackgroundInput);
        var proposal = Assert.Single(Assert.Single(after.Batches).Proposals);
        Assert.Equal(before.Batches[0].Id, after.Batches[0].Id);
        Assert.Equal(before.Batches[0].Response, after.Batches[0].Response);
        Assert.Equal(AgentToolProposalState.Cancelled, proposal.State);
        Assert.Equal(ExecutionApprovalStatus.Rejected, proposal.ApprovalStatus);
        Assert.Equal(AgentToolEffectState.NotCommitted, proposal.EffectState);
        Assert.Equal(AgentToolCancellationDisposition.NotDispatched, proposal.Cancellation!.Disposition);
        Assert.Equal(AgentParentStopOutcome.AlreadyClosed, await Owner(fixture).StopAsync(original.Id, Parent(original)));
    }

    [Fact]
    public async Task Released_lease_does_not_resolve_an_unacknowledged_provider_request() {
        await using var fixture = await BackgroundFixture.CreateAsync();
        var journal = fixture.Journal();
        await using (var lease = await journal.AcquireRunAsync(fixture.Reference, default)) {
            var saved = await journal.BeginSegmentAsync(lease, Envelope(), null, default);
            await journal.BeginProviderDispatchAsync(lease, saved.Segments[0].Id, Envelope().Digest, Envelope().Digest, [], default);
        }
        var original = await Read(fixture);
        Assert.Equal(AgentParentStopOutcome.UnresolvedEffects, await Owner(fixture).StopAsync(original.Id, Parent(original)));
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(await Read(fixture)));
    }

    [Fact]
    public async Task A_legacy_segment_without_provider_evidence_cannot_be_force_failed() {
        await using var fixture = await BackgroundFixture.CreateAsync();
        var journal = fixture.Journal();
        await using (var lease = await journal.AcquireRunAsync(fixture.Reference, default)) {
            await journal.BeginSegmentAsync(lease, Envelope(), null, default);
        }
        var original = await Read(fixture);
        Assert.Equal(AgentToolJournalRecord.BackgroundSchemaVersion, original.ToolAdmission!.SchemaVersion);
        Assert.Equal(AgentParentStopOutcome.UnresolvedEffects, await Owner(fixture).StopAsync(original.Id, Parent(original)));
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(await Read(fixture)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_held_dispatch_lease_reports_busy_or_propagates_caller_cancellation(bool cancelCaller) {
        await using var fixture = await BackgroundFixture.CreateAsync();
        var original = await Read(fixture);
        await using var held = await fixture.Source.NewStore().AcquireToolDispatchLeaseAsync(original.Id);
        using var cancellation = new CancellationTokenSource();
        if (cancelCaller) {
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Owner(fixture).StopAsync(original.Id, Parent(original), cancellation.Token));
        } else {
            Assert.Equal(AgentParentStopOutcome.LeaseBusy, await Owner(fixture).StopAsync(original.Id, Parent(original), cancellation.Token));
        }
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(await Read(fixture)));
    }

    [Theory]
    [InlineData((int)ExistingRunDetailCommitStage.JournalPersisted)]
    [InlineData((int)ExistingRunDetailCommitStage.RunPersisted)]
    public async Task Lost_stop_acknowledgement_recovers_the_original_journal_and_replays_once(int boundaryValue) {
        await using var fixture = await BackgroundFixture.CreateAsync();
        var boundary = (ExistingRunDetailCommitStage)boundaryValue;
        var original = await Read(fixture);
        var fired = false;
        var store = fixture.Source.NewStore(stage => {
            if (!fired && stage == boundary) {
                fired = true;
                throw new IOException("Lost ordinary parent-stop acknowledgement.");
            }
        });
        var parent = Parent(original);
        await Assert.ThrowsAsync<IOException>(() => Owner(fixture, store).StopAsync(original.Id, parent));
        Assert.True(fired);
        var recovered = await Read(fixture);
        Assert.Equal(RunOutcome.Cancelled, recovered.Outcome);
        Assert.Equal(original.ToolAdmission!.Session, recovered.ToolAdmission!.Session);
        Assert.Equal(original.Revision + 1, recovered.Revision);
        Assert.Equal(AgentParentStopOutcome.AlreadyClosed, await Owner(fixture).StopAsync(original.Id, parent));
        Assert.Equal(JsonSerializer.Serialize(recovered), JsonSerializer.Serialize(await Read(fixture)));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Parent_revocation_after_lease_or_before_real_file_publication_preserves_original_bytes(int boundary) {
        await using var fixture = await BackgroundFixture.CreateAsync();
        var original = await Read(fixture);
        var observed = 0;
        var parent = new AgentBackgroundParentStop(original.ToolAdmission!.Session, (_, token) => {
            token.ThrowIfCancellationRequested();
            if (++observed == boundary) {
                throw new UnauthorizedAccessException("Original parent fence changed.");
            }
            return Task.CompletedTask;
        });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Owner(fixture).StopAsync(original.Id, parent));
        Assert.Equal(boundary, observed);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(await Read(fixture)));
    }

    private static async Task<ExecutionRunRecord> Read(BackgroundFixture fixture)
        => (await fixture.Source.NewStore().GetExecutionRunAsync(fixture.Reference.ExecutionRunId))!;
    private static AgentBackgroundParentStop Parent(ExecutionRunRecord original)
        => new(original.ToolAdmission!.Session, (_, token) => {
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        });
    private static AgentBackgroundExecutionStopper Owner(BackgroundFixture fixture, FileSandboxWorkspaceStore? store = null) {
        store ??= fixture.Source.NewStore();
        return new(store, store, store, new AgentExecutionCancellationRegistry(NullLogger<AgentExecutionCancellationRegistry>.Instance), TimeProvider.System);
    }
    private static AgentToolProtocolEnvelope Envelope(string json = "{}") => AgentToolAdmissionJournalFixture.Envelope(json);
}
