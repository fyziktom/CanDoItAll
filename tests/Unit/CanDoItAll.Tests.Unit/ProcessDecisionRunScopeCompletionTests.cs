using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDecisionRunScopeCompletionTests {
    [Theory]
    [InlineData(StrategyOutcome.Succeeded, ProcessDecisionRunScopeCommandKind.CompleteResult)]
    [InlineData(StrategyOutcome.Failed, ProcessDecisionRunScopeCommandKind.CancelResult)]
    [InlineData(StrategyOutcome.Canceled, ProcessDecisionRunScopeCommandKind.CancelResult)]
    public void Original_final_root_result_selects_its_exact_scope_command(StrategyOutcome outcome, ProcessDecisionRunScopeCommandKind kind) {
        var request = Request(outcome);
        ProcessDecisionRunScopeCompletion.Require(request);
        Assert.Equal(kind, request.DecisionRunScope!.Kind);
        Assert.Null(ProcessDecisionRunScopeCompletion.Prepare(null, request.OriginalState, request.Mutation));
    }

    [Theory]
    [InlineData(Change.Descendant)]
    [InlineData(Change.ActiveRun)]
    [InlineData(Change.PendingStep)]
    [InlineData(Change.NoNewResult)]
    [InlineData(Change.ActiveClaim)]
    [InlineData(Change.WrongCommand)]
    public void A_scope_command_cannot_hide_an_unfinished_or_different_root_result(Change change) {
        var request = Request();
        request = change switch {
            Change.Descendant => request with { OriginalState = request.OriginalState with { RootRunId = CanDoItAll.Processes.Abstractions.ProcessRunId.New() } },
            Change.ActiveRun => request with { Mutation = request.Mutation with { State = request.Mutation.State with { Status = ProcessRuntimeStatus.Active } } },
            Change.PendingStep => request with { Mutation = request.Mutation with { State = request.Mutation.State with {
                Steps = [request.Mutation.State.Steps[0] with { Status = ProcessRuntimeStepStatus.Ready }]
            } } },
            Change.NoNewResult => request with { Mutation = request.Mutation with { State = request.Mutation.State with {
                AppliedResults = request.OriginalState.AppliedResults
            } } },
            Change.ActiveClaim => request with { Mutation = request.Mutation with { State = request.Mutation.State with {
                Claims = [request.Mutation.State.Claims[0] with { Status = DispatchClaimStatus.Claimed }]
            } } },
            Change.WrongCommand => request with { DecisionRunScope = request.DecisionRunScope! with { Kind = ProcessDecisionRunScopeCommandKind.CancelResult } },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        Assert.Throws<InvalidOperationException>(() => ProcessDecisionRunScopeCompletion.Require(request));
    }

    private static ProcessRuntimeCommitRequest Request(StrategyOutcome outcome = StrategyOutcome.Succeeded) {
        var data = new ProcessDecisionReviewTestData();
        data.State = data.State with { RootRunId = data.State.RunId };
        data.Result = data.Result with {
            Child = data.Result.Child with { RootRunId = data.State.RunId }, Outcome = outcome,
            Closure = outcome == StrategyOutcome.Succeeded ? data.Result.Closure
                : new(data.Result.Closure.OwnerRevision, ProcessDecisionChildOutcome.NoEffect, null)
        };
        var mutation = data.Complete();
        Assert.True(mutation.Succeeded);
        return new(RuntimeCommandId.New(), data.State, mutation) {
            DecisionRunScope = ProcessDecisionRunScopeCompletion.Prepare(new(new(Guid.NewGuid()), 3), data.State, mutation)
        };
    }

    public enum Change { Descendant, ActiveRun, PendingStep, NoNewResult, ActiveClaim, WrongCommand }
}
