using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class AgentInheritedPlanningRunReaderTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Reads_the_original_waiting_detail_without_an_execution_runtime(ReadBasisMode mode) {
        await using var source = await AgentInheritedPlanningFixture.CreateForPublicationAsync(parentMode: mode);
        var waiting = await source.WaitForApprovalAsync();
        var store = source.Files.NewStore();
        var retained = (await store.GetExecutionRunDetailAsync(waiting.Id))!;
        var approvals = ExecutionRunStateTransitions.SynchronizePendingApprovals(retained.Approvals,
            waiting, waiting.PendingApprovals, source.Clock.Now).RunApprovals;
        waiting = (await store.SaveExecutionRunDetailAsync(retained with { Approvals = approvals })).Run;
        var participant = new AgentInheritedPlanningCoordinatorFixture.Participant(source.Basis);
        var reader = new AgentInheritedPlanningRunReader(store, source.NewJournal(store), [participant]);

        var detail = await reader.ReadAsync(waiting.Id);
        Assert.Equal(AgentPlanningDecisionExpectation.From(waiting), AgentPlanningDecisionExpectation.From(detail.Run));
        Assert.Equal(waiting.PendingApprovals.Single().ApprovalId, Assert.Single(detail.Approvals).ApprovalId);
        Assert.Equal(approvals, detail.Approvals);
        Assert.Null(detail.PlanningReview);
        Assert.Null(detail.EffectContinuation);
        Assert.Equal(0, participant.Reads);
        Assert.Equal(0, participant.Preparations);
        Assert.Equal(waiting.Revision, (await reader.ReadAsync(waiting.Id)).Run.Revision);
        Assert.Equal(waiting.Revision, (await source.ReadAsync()).Revision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rechecks_current_background_and_participant_disclosure(bool participantDenied) {
        await using var source = await AgentInheritedPlanningFixture.CreateForPublicationAsync();
        var waiting = await source.WaitForApprovalAsync();
        var store = source.Files.NewStore();
        var participant = new AgentInheritedPlanningCoordinatorFixture.Participant(source.Basis);
        var reader = new AgentInheritedPlanningRunReader(store, source.NewJournal(store), [participant]);
        _ = await reader.ReadAsync(waiting.Id);
        participant.DenyRead = participantDenied;
        source.Policy.ReadAllowed = participantDenied;

        if (participantDenied) {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reader.ReadAsync(waiting.Id));
        } else {
            await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => reader.ReadAsync(waiting.Id));
        }
        Assert.Equal(waiting.Revision, (await source.ReadAsync()).Revision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_ambiguous_participants_refuse_disclosure(bool ambiguous) {
        await using var source = await AgentInheritedPlanningFixture.CreateForPublicationAsync();
        var waiting = await source.WaitForApprovalAsync();
        var store = source.Files.NewStore();
        var participant = new AgentInheritedPlanningCoordinatorFixture.Participant(source.Basis);
        var reader = new AgentInheritedPlanningRunReader(store, source.NewJournal(store), ambiguous ? [participant, participant] : []);

        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => reader.ReadAsync(waiting.Id));
        Assert.Equal(ambiguous ? "planning.ambiguous-participant" : "planning.unsupported", failure.Code);
        Assert.Equal(waiting.Revision, (await source.ReadAsync()).Revision);
    }

    [Fact]
    public async Task Missing_run_does_not_create_an_execution() {
        await using var source = await AgentInheritedPlanningFixture.CreateForPublicationAsync();
        var store = source.Files.NewStore();
        var reader = new AgentInheritedPlanningRunReader(store, source.NewJournal(store), []);
        var missing = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadAsync(missing));
        Assert.Null(await store.GetExecutionRunAsync(missing));
    }
}
