using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using static CanDoItAll.Tests.Unit.ProcessDecisionReviewTestData;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDecisionReviewCompletionTests {
    [Theory]
    [InlineData(StrategyOutcome.Succeeded, ProcessRuntimeStepStatus.Completed, ProcessRuntimeStatus.Completed)]
    [InlineData(StrategyOutcome.Failed, ProcessRuntimeStepStatus.Failed, ProcessRuntimeStatus.Failed)]
    [InlineData(StrategyOutcome.Canceled, ProcessRuntimeStepStatus.Cancelled, ProcessRuntimeStatus.Cancelled)]
    public void Closed_review_appends_one_result_and_terminal_events_without_a_second_claim_completion(StrategyOutcome outcome,
        ProcessRuntimeStepStatus stepStatus, ProcessRuntimeStatus runStatus) {
        var fixture = new ProcessDecisionReviewTestData();
        fixture.Result = fixture.Result with { Outcome = outcome };
        var original = fixture.State;
        var result = fixture.Complete();
        Assert.Equal(ProcessRuntimeTransitionOutcome.Applied, result.Outcome);
        Assert.Equal(runStatus, result.State.Status);
        Assert.Equal(stepStatus, result.State.Steps.Single().Status);
        StrategyResultIdempotencyKey? expectedKey = stepStatus == ProcessRuntimeStepStatus.Completed ? fixture.Result.CompletionKey : null;
        Assert.Equal(expectedKey, result.State.Steps.Single().CompletedResultKey);
        Assert.Same(original.Claims, result.State.Claims);
        Assert.Same(original.AvailableArtifactSlots, result.State.AvailableArtifactSlots);
        Assert.Same(original.AppliedResults.Single(), result.State.AppliedResults[0]);
        var receipt = result.State.AppliedResults[1];
        Assert.Equal(5, receipt.AppliedSequence);
        Assert.Equal(fixture.Result.CompletionKey, receipt.IdempotencyKey);
        Assert.Equal(new ProcessExecutionRunId(fixture.Result.Child.ChildId.Value), receipt.ExecutionRunId);
        Assert.Equal(2, result.Events.Count);
        Assert.DoesNotContain(result.Events, item => item.EventType == ProcessRuntimeEventTypes.DispatchClaimCompleted);
        Assert.Equal(result.Events.Select(item => item.EventId), result.OutboxMessages.Select(item => item.EventId));
        Assert.Empty(result.ArtifactLedgerEvents);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, original.Steps.Single().Status);
        fixture.State = result.State;
        var replay = fixture.Complete();
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Outcome);
        Assert.Same(result.State, replay.State);
        Assert.Empty(replay.Events);
        Assert.Empty(replay.OutboxMessages);
    }

    [Theory]
    [InlineData(ProcessRuntimeStatus.Completed)]
    [InlineData(ProcessRuntimeStatus.Failed)]
    [InlineData(ProcessRuntimeStatus.Cancelled)]
    [InlineData(ProcessRuntimeStatus.CancelRequested)]
    [InlineData((ProcessRuntimeStatus)int.MaxValue)]
    public void New_completion_cannot_resurrect_a_stopped_or_invalid_run(ProcessRuntimeStatus status) {
        var fixture = new ProcessDecisionReviewTestData();
        fixture.State = fixture.State with { Status = status };
        AssertRejected(fixture, "Runtime.ReviewRunStopped");
    }

    [Theory]
    [InlineData(DispatchClaimStatus.Claimed, false)]
    [InlineData(DispatchClaimStatus.Claimed, true)]
    [InlineData(DispatchClaimStatus.LeaseRenewed, false)]
    [InlineData(DispatchClaimStatus.LeaseRenewed, true)]
    [InlineData(DispatchClaimStatus.Reclaimed, false)]
    [InlineData(DispatchClaimStatus.Reclaimed, true)]
    public void An_open_claim_is_not_authority_for_review_even_after_its_deadline(DispatchClaimStatus status, bool expired) {
        var fixture = new ProcessDecisionReviewTestData();
        var open = fixture.State.Claims.Single() with {
            ClaimToken = new(Id(70)), Status = status, ResultIdempotencyKey = null,
            ExpiresAtUtc = expired ? Now.AddSeconds(-1) : Now.AddMinutes(1)
        };
        fixture.State = fixture.State with { Claims = [.. fixture.State.Claims, open] };
        AssertRejected(fixture, "Runtime.ReviewClaimChanged");
    }

    [Theory]
    [InlineData(ChangedHistory.OtherChild)]
    [InlineData(ChangedHistory.OtherStrategy)]
    [InlineData(ChangedHistory.OtherWaitingKey)]
    [InlineData(ChangedHistory.WaitingOutcome)]
    [InlineData(ChangedHistory.NewerResult)]
    [InlineData(ChangedHistory.TiedSequence)]
    [InlineData(ChangedHistory.OpenStepToken)]
    [InlineData(ChangedHistory.ChangedClaimKey)]
    [InlineData(ChangedHistory.ChangedAttempt)]
    public void Original_history_and_waiting_authority_cannot_be_substituted(ChangedHistory change) {
        var fixture = new ProcessDecisionReviewTestData();
        var state = fixture.State;
        fixture.Result = change switch {
            ChangedHistory.OtherChild => fixture.Result with { Child = fixture.Result.Child with { ChildId = new(Id(70)) } },
            ChangedHistory.OtherStrategy => fixture.Result with { StrategyId = new("other-strategy") },
            ChangedHistory.OtherWaitingKey => fixture.Result with { WaitingResultKey = new(Id(70)) },
            _ => fixture.Result
        };
        fixture.State = change switch {
            ChangedHistory.WaitingOutcome => state with { AppliedResults = [state.AppliedResults[0] with { Outcome = StrategyOutcome.Waiting }] },
            ChangedHistory.NewerResult or ChangedHistory.TiedSequence => state with {
                AppliedResults = [state.AppliedResults[0] with {
                    IdempotencyKey = new(Id(70)), AppliedSequence = change == ChangedHistory.NewerResult ? 5 : 4
                }, state.AppliedResults[0]]
            },
            ChangedHistory.OpenStepToken => state with { Steps = [state.Steps[0] with { ActiveClaimToken = new(Id(70)) }] },
            ChangedHistory.ChangedClaimKey => state with { Claims = [state.Claims[0] with { ResultIdempotencyKey = new(Id(70)) }] },
            ChangedHistory.ChangedAttempt => state with { Steps = [state.Steps[0] with { AttemptNumber = 2 }] },
            _ => state
        };
        AssertRejected(fixture);
    }

    [Theory]
    [InlineData(ChangedCompletion.Outcome)]
    [InlineData(ChangedCompletion.OwnerRevision)]
    [InlineData(ChangedCompletion.EffectReceipt)]
    [InlineData(ChangedCompletion.Generation)]
    [InlineData(ChangedCompletion.WorkAmount)]
    public void A_used_completion_key_cannot_borrow_a_different_closure_or_disposition(ChangedCompletion change) {
        var fixture = new ProcessDecisionReviewTestData();
        fixture.State = fixture.Complete().State;
        fixture.Result = change switch {
            ChangedCompletion.Outcome => fixture.Result with { Outcome = StrategyOutcome.Failed },
            ChangedCompletion.OwnerRevision => fixture.Result with {
                Closure = fixture.Result.Closure with { OwnerRevision = 13 }, Work = fixture.Result.Work with { OwnerWorkRevision = 13 }
            },
            ChangedCompletion.EffectReceipt => fixture.Result with { Closure = fixture.Result.Closure with { AcceptedReceipt = new(Id(70)) } },
            ChangedCompletion.Generation => fixture.Result with { Child = fixture.Result.Child with { Reference = fixture.Result.Child.Reference with { Generation = 2 } } },
            ChangedCompletion.WorkAmount => fixture.Result with {
                Work = fixture.Result.Work with { Consumed = fixture.Result.Work.Consumed with { WorkTicks = fixture.Result.Work.Consumed.WorkTicks + 1 } }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        AssertRejected(fixture, "Runtime.ReviewResultChanged");
    }

    [Theory]
    [InlineData(RuntimeObligation.Input)]
    [InlineData(RuntimeObligation.Output)]
    [InlineData(RuntimeObligation.Tool)]
    [InlineData(RuntimeObligation.Host)]
    public void Runtime_obligations_cannot_be_silently_discarded(RuntimeObligation obligation) {
        var fixture = new ProcessDecisionReviewTestData();
        var step = fixture.State.Steps[0];
        step = obligation switch {
            RuntimeObligation.Input => step with { RequiredArtifactSlots = new HashSet<ArtifactSlotId> { new(Id(70)) } },
            RuntimeObligation.Output => step with { ProducedArtifactSlots = new HashSet<ArtifactSlotId> { new(Id(70)) } },
            RuntimeObligation.Tool => step with { RequiredRuntimeToolNames = ["required-tool"] },
            RuntimeObligation.Host => step with { RequiredHostCapabilities = new HashSet<ProcessHostCapabilityId> { new("required-host") } },
            _ => throw new ArgumentOutOfRangeException(nameof(obligation))
        };
        fixture.State = fixture.State with { Steps = [step] };
        AssertRejected(fixture, "Runtime.ReviewContractUnsupported");
    }

    [Fact]
    public void Other_steps_artifacts_and_receipts_remain_untouched_and_prevent_early_run_completion() {
        var fixture = new ProcessDecisionReviewTestData();
        var unrelated = fixture.State.Steps[0] with { StepInstanceId = new(Id(70)), Status = ProcessRuntimeStepStatus.Pending };
        var artifact = new ArtifactSlotId(Id(71));
        fixture.State = fixture.State with { Steps = [fixture.State.Steps[0], unrelated], AvailableArtifactSlots = new HashSet<ArtifactSlotId> { artifact } };
        var result = fixture.Complete();
        Assert.Equal(ProcessRuntimeStatus.Active, result.State.Status);
        Assert.Same(unrelated, result.State.Steps[1]);
        Assert.Same(fixture.State.AvailableArtifactSlots, result.State.AvailableArtifactSlots);
        Assert.Single(result.Events);
    }

    [Theory]
    [InlineData(ProcessRuntimeStepStatus.Failed, ProcessRuntimeStepStatus.Pending, ProcessRuntimeStatus.Failed)]
    [InlineData(ProcessRuntimeStepStatus.Cancelled, ProcessRuntimeStepStatus.Pending, ProcessRuntimeStatus.Active)]
    [InlineData(ProcessRuntimeStepStatus.Cancelled, ProcessRuntimeStepStatus.Completed, ProcessRuntimeStatus.Cancelled)]
    [InlineData(ProcessRuntimeStepStatus.Completed, ProcessRuntimeStepStatus.Skipped, ProcessRuntimeStatus.Completed)]
    public void Terminal_aggregation_remains_shared_with_ordinary_results(ProcessRuntimeStepStatus first,
        ProcessRuntimeStepStatus second, ProcessRuntimeStatus expected) {
        var fixture = new ProcessDecisionReviewTestData();
        var state = fixture.State with { Steps = [
            fixture.State.Steps[0] with { Status = first },
            fixture.State.Steps[0] with { StepInstanceId = new(Id(70)), Status = second }
        ] };
        Assert.Equal(expected, ProcessRuntimeTerminalStates.CompleteRunIfTerminal(state, fixture.Context.OccurredAtUtc).Status);
    }

    [Fact]
    public void Sequence_exhaustion_refuses_instead_of_wrapping_to_an_earlier_result() {
        var fixture = new ProcessDecisionReviewTestData();
        Assert.Throws<OverflowException>(() => StrategyResultReceipt.NextSequence(
            [fixture.State.AppliedResults[0] with { AppliedSequence = long.MaxValue }]));
    }

    private static void AssertRejected(ProcessDecisionReviewTestData fixture, string? code = null) {
        var result = fixture.Complete();
        Assert.Equal(ProcessRuntimeTransitionOutcome.Rejected, result.Outcome);
        Assert.Same(fixture.State, result.State);
        Assert.Empty(result.Events);
        Assert.Empty(result.OutboxMessages);
        Assert.Empty(result.ArtifactLedgerEvents);
        if (code is not null) {
            Assert.Equal(code, Assert.Single(result.Diagnostics).Code);
        }
    }

    public enum ChangedHistory { OtherChild, OtherStrategy, OtherWaitingKey, WaitingOutcome, NewerResult, TiedSequence, OpenStepToken, ChangedClaimKey, ChangedAttempt }
    public enum ChangedCompletion { Outcome, OwnerRevision, EffectReceipt, Generation, WorkAmount }
    public enum RuntimeObligation { Input, Output, Tool, Host }
}
