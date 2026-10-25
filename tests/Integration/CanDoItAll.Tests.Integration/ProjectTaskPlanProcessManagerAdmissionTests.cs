using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProjectTaskPlanProcessManagerAdmissionTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Waiting_process_review_publishes_after_claim_closure_without_another_provider(bool agentSource) {
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync(agentSource: agentSource);
        var waiting = await fixture.PrepareAsync();
        var blocked = await WaitForReviewAsync(fixture);
        fixture.Source.ExpireClaim();
        var observed = await fixture.Source.SourcePolicy.ObserveAsync(waiting, fixture.Source.Profile);
        Assert.True(observed.ReadAllowed);
        Assert.False(observed.DispatchAllowed);
        var result = await fixture.PublishAsync(waiting, Continuation(fixture, waiting));
        Assert.Equal(AgentToolEffectState.Committed, result.EffectState);
        var outcome = ProjectTaskPlanAgentFixture.Result(result);
        Assert.Null(outcome.Refusal);
        await fixture.Source.Sql.VerifyAsync(1, 1);
        Assert.Single(fixture.Wire.Requests);
        Assert.Equal(RunOutcome.Succeeded, (await fixture.Source.ReadAsync()).Outcome);
        var retained = await fixture.Source.Sql.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(blocked.RunId);
        var receipt = Assert.Single(retained!.AppliedResults);
        Assert.Equal(blocked.AppliedResults.Single().IdempotencyKey, receipt.IdempotencyKey);
        Assert.Equal(StrategyOutcome.NeedsManager, receipt.Outcome);
        Assert.Equal(new ProcessExecutionRunId(waiting.Id), receipt.ExecutionRunId);
        Assert.Null(retained.Steps.Single().ActiveClaimToken);
        var child = await fixture.Source.Sql.Services.GetRequiredService<IProcessDecisionChildReader>()
            .ReadAsync(fixture.Source.Process.Parent.Child.Address);
        Assert.Null(child!.Closure);
        Assert.Null(child.Settlement);
    }

    [Fact]
    public async Task Review_purpose_cannot_publish_while_the_Process_is_still_dispatching() {
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync();
        var waiting = await fixture.PrepareAsync();
        var result = await fixture.PublishAsync(waiting, Continuation(fixture, waiting, requireWaiting: false));
        Assert.Equal(AgentToolEffectState.NotCommitted, result.EffectState);
        Assert.Equal(ProjectTaskPlanRefusal.SourceChanged, ProjectTaskPlanAgentFixture.Result(result).Refusal);
        await fixture.Source.Sql.VerifyAsync(0, 0);
        Assert.Single(fixture.Wire.Requests);
    }

    [Fact]
    public async Task Profile_revocation_after_review_publication_flush_rolls_back_tasks_and_receipt() {
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync(agentSource: false);
        var waiting = await fixture.PrepareAsync();
        await WaitForReviewAsync(fixture);
        var flushed = false;
        fixture.Source.Sql.Faults.Save.AfterFlush = _ => {
            flushed = true;
            fixture.Source.Generations.Advance();
            return Task.CompletedTask;
        };
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() =>
            fixture.PublishAsync(waiting, Continuation(fixture, waiting)));
        Assert.True(flushed);
        var retained = await fixture.Source.ReadAsync();
        var proposal = AgentToolJournalTransitions.RequireProposal(retained.ToolAdmission!, ProjectTaskPlanAgentFixture.Binding(waiting));
        Assert.Equal(AgentToolProposalState.Completed, proposal.State);
        Assert.Equal(AgentToolEffectState.NotCommitted, proposal.EffectState);
        Assert.NotNull(retained.ToolAdmission!.PlanningDecision!.Work.Open);
        await fixture.Source.Sql.VerifyAsync(0, 0);
        Assert.Single(fixture.Wire.Requests);
        var child = await fixture.Source.Sql.Services.GetRequiredService<IProcessDecisionChildReader>()
            .ReadAsync(fixture.Source.Process.Parent.Child.Address);
        Assert.Null(child!.Closure);
        Assert.Null(child.Settlement);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Current_manager_loss_before_staging_or_after_flush_rolls_back_tasks_and_receipt(bool afterFlush, bool unavailable) {
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync();
        var waiting = await fixture.PrepareAsync();
        await WaitForReviewAsync(fixture);
        var allowed = afterFlush;
        var flushed = false;
        fixture.Source.Sql.Faults.Save.AfterFlush = _ => {
            flushed = true;
            allowed = false;
            return Task.CompletedTask;
        };
        var result = await fixture.PublishAsync(waiting, Continuation(fixture, waiting, currentManager: token => {
            token.ThrowIfCancellationRequested();
            if (!allowed) {
                if (unavailable) {
                    throw new AgentToolReceiptReadUnavailableException(new IOException("Manager authority observation unavailable."));
                }
                throw new AgentToolReceiptAccessDeniedException();
            }
            return Task.CompletedTask;
        }));
        Assert.Equal(afterFlush, flushed);
        Assert.Equal(AgentToolEffectState.NotCommitted, result.EffectState);
        Assert.Equal(unavailable ? ProjectTaskPlanRefusal.Unavailable : ProjectTaskPlanRefusal.AuthorityDenied,
            ProjectTaskPlanAgentFixture.Result(result).Refusal);
        await fixture.Source.Sql.VerifyAsync(0, 0);
        Assert.Single(fixture.Wire.Requests);
    }

    private static AgentInheritedSavedEffectContinuation Continuation(ProjectTaskPlanProcessRuntimeFixture fixture,
        ExecutionRunRecord run, bool requireWaiting = true, Func<CancellationToken, Task>? currentManager = null)
        => AgentInheritedSavedEffectContinuation.ForManagerReview(run.ToolAdmission!.PlanningDecision!.InheritedOrigin!,
            AgentPlanningDecisionExpectation.From(run), ProjectTaskPlanAgentFixture.Binding(run), async (current, token) => {
                var parent = fixture.Source.Process.Parent;
                Assert.Equal(parent.Child.Address.ChildId.Value, current.Id);
                var child = await fixture.Source.Sql.Services.GetRequiredService<IProcessDecisionChildReader>().ReadAsync(parent.Child.Address, token);
                if (child?.Admission != parent.Child || !child.IsCurrent || child.Closure is not null || child.Settlement is not null ||
                    requireWaiting && child.Generation.Phase != ProcessDecisionPhase.WaitingForReview) {
                    throw new AgentToolReceiptAccessDeniedException();
                }
            }, currentManager ?? AllowManager);

    private static Task AllowManager(CancellationToken token) {
        token.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private static async Task<ProcessRuntimeStateSnapshot> WaitForReviewAsync(ProjectTaskPlanProcessRuntimeFixture fixture) {
        var source = fixture.Source;
        var state = source.Process.State;
        var step = state.Steps.Single();
        var claim = state.Claims.Single(item => item.ClaimToken == step.ActiveClaimToken);
        var key = Guid.NewGuid();
        StrategyResultEnvelope result = new(new("manager-admission-fixture"), "1.0.0", key, StrategyOutcome.NeedsManager,
            [], [], [], [], "sha256:" + new string('e', 64)) { ExecutionRunId = new(source.Original.Id) };
        SubmitStrategyResultCommand command = new(step.StepInstanceId, claim.OwnerId, claim.ClaimToken, new(key), result);
        RuntimeCommandContext context = new(RuntimeCommandId.New(), new(ProcessEventActorKind.System, new("manager-admission-fixture")),
            new("manager-admission-fixture"), source.Clock.GetUtcNow().AddSeconds(1));
        var committed = await source.Sql.Services.GetRequiredService<ProcessDecisionResultCoordinator>()
            .SubmitAsync(state, context, command);
        Assert.True(committed.Succeeded);
        Assert.Equal(ProcessDecisionPhase.WaitingForReview, committed.Decision!.Current.Phase);
        Assert.Null(committed.State.Steps.Single().ActiveClaimToken);
        return committed.State;
    }
}
