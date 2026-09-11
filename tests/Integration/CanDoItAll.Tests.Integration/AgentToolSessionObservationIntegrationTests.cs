using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class AgentToolSessionObservationIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Observation_reads_the_active_owner_store_across_workspace_scopes_and_restart(bool projectStorage) {
        var sourceProjectId = Guid.NewGuid();
        var sourceScope = WorkspaceScopeDescriptor.Project(sourceProjectId.ToString("D"));
        var storageScope = projectStorage ? sourceScope : WorkspaceScopeDescriptor.Sandbox;
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(
            transientContext: new("Private admitted context is not an observation field", sourceScope), storageScope: storageScope);
        var unrelatedStore = new FileSandboxWorkspaceStore(fixture.WorkspaceRoot,
            WorkspaceScopeDescriptor.Organization(fixture.Profile.ProfileId.ToString("N")));
        Assert.Null(await unrelatedStore.GetExecutionRunAsync(fixture.Session.ExecutionRunId));
        var originalSource = AgentTurnContextMetadata.TryReadTurnContextReference(fixture.Detail.Run.MetadataJson);
        var originalGovernance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(fixture.Detail.Run.MetadataJson);
        AgentToolSessionObservation? first = null;
        for (var attempt = 0; attempt < 2; attempt++) {
            var journal = fixture.NewJournal(fixture.NewStore());
            await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
            using var bound = lease.Bind();
            var observation = await new AgentToolAdmissionVerifier().RequireSessionObservationAsync(fixture.Session);
            Assert.Equal(fixture.Detail.Run.ToolAdmission!.Session, observation.Session);
            Assert.Equal(originalSource, observation.TurnContext);
            Assert.Equal(originalGovernance, observation.Governance);
            Assert.Equal(sourceScope, observation.Governance!.WorkspaceScope);
            Assert.Equal(sourceProjectId.ToString("D"), observation.TurnContext!.SourceId.Value);
            Assert.Empty((await journal.ReadAsync(lease, default)).Batches);
            if (first is not null) {
                Assert.Equal(first, observation);
            }
            first = observation;
        }
        Assert.Null(await unrelatedStore.GetExecutionRunAsync(fixture.Session.ExecutionRunId));
    }

    [Fact]
    public async Task Observation_cannot_select_a_different_saved_session_or_journal_under_an_active_lease() {
        await using var first = await AgentToolAdmissionJournalFixture.CreateAsync();
        await using var second = await AgentToolAdmissionJournalFixture.CreateAsync(profileBinding: first.Profile);
        var owner = first.NewJournal();
        await using var lease = await owner.AcquireRunAsync(first.Session, default);
        using var bound = lease.Bind();
        var verifier = new AgentToolAdmissionVerifier();
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => verifier.RequireSessionObservationAsync(second.Session).AsTask());
        var foreignOwner = second.NewJournal(second.NewStore());
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() =>
            foreignOwner.RequireSessionObservationAsync(first.Session).AsTask());
        Assert.Equal("tool-admission.lease-lost", denied.Code);
        Assert.Equal(first.Session, (await verifier.RequireSessionObservationAsync(first.Session)).Session.Reference);
        Assert.Empty((await first.NewStore().GetExecutionRunAsync(first.Session.ExecutionRunId))!.ToolAdmission!.Batches);
        Assert.Empty((await second.NewStore().GetExecutionRunAsync(second.Session.ExecutionRunId))!.ToolAdmission!.Batches);
    }

    [Fact]
    public async Task A_cancelled_receipt_reconciliation_lease_cannot_supply_ordinary_session_observation() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        await ((ISandboxWorkspaceExecutionRunMutationStore)fixture.Store).UpdateExecutionRunDetailAsync(fixture.Session.ExecutionRunId,
            detail => detail with { Run = detail.Run with {
                State = ExecutionState.Completed, Outcome = RunOutcome.Cancelled,
                Revision = detail.Run.Revision + 1, UpdatedAtUtc = DateTimeOffset.UtcNow
            } });
        var journal = fixture.NewJournal(fixture.NewStore());
        await using var lease = await journal.AcquireCancelledReconciliationAsync(fixture.Session, default);
        using var bound = lease.Bind();
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() =>
            new AgentToolAdmissionVerifier().RequireSessionObservationAsync(fixture.Session).AsTask());
        Assert.Empty((await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!.Batches);
    }
}
