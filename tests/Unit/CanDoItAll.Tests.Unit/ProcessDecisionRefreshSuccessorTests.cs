using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;
using static CanDoItAll.Tests.Unit.ProcessDecisionReviewTestData;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDecisionRefreshSuccessorTests {
    [Theory]
    [InlineData(false, ProcessRuntimeStatus.Active)]
    [InlineData(false, ProcessRuntimeStatus.Blocked)]
    [InlineData(false, ProcessRuntimeStatus.Waiting)]
    [InlineData(true, ProcessRuntimeStatus.Active)]
    [InlineData(true, ProcessRuntimeStatus.Blocked)]
    [InlineData(true, ProcessRuntimeStatus.Waiting)]
    public void Successor_preserves_attempts_claims_and_results_and_emits_only_original_actor_readiness(bool shared, ProcessRuntimeStatus status) {
        var fixture = new Fixture(shared);
        fixture.Data.State = fixture.Data.State with {
            Status = status,
            Steps = [fixture.Data.State.Steps.Single() with { AttemptNumber = 7 }],
            Claims = [fixture.Data.State.Claims.Single() with { AttemptNumber = 7 }]
        };
        var state = fixture.Data.State;
        var request = fixture.Request();
        fixture.Successor.RequireMutation(request, fixture.Intent);
        var mutation = request.Mutation;
        Assert.Equal(ProcessRuntimeStatus.Active, mutation.State.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Ready, mutation.State.Steps.Single().Status);
        Assert.Equal(7, mutation.State.Steps.Single().AttemptNumber);
        Assert.Same(state.Claims, mutation.State.Claims);
        Assert.Same(state.AppliedResults, mutation.State.AppliedResults);
        Assert.Equal(state, mutation.State with { Status = state.Status, Steps = state.Steps, UpdatedAtUtc = state.UpdatedAtUtc });
        Assert.Equal(state.Steps.Single(), mutation.State.Steps.Single() with { Status = state.Steps.Single().Status });
        Assert.Equal(status == ProcessRuntimeStatus.Active ? 2 : 3, mutation.Events.Count);
        Assert.All(mutation.Events, item => Assert.Equal(fixture.Intent.Actor, item.Actor));
        Assert.All(mutation.OutboxMessages, item => Assert.Equal(ProcessOutboxSubscriberKind.RuntimeProjection, item.SubscriberKind));
        Assert.Empty(mutation.ArtifactLedgerEvents);
        Assert.Empty(mutation.Diagnostics);
    }

    public enum ChangedState { CancelRequested, Cancelled, Completed, Failed, MissingWaiting, Claimed, ClaimAttempt, StepAttempt, Ready, Root, LaterResult }

    [Theory]
    [InlineData(ChangedState.CancelRequested)]
    [InlineData(ChangedState.Cancelled)]
    [InlineData(ChangedState.Completed)]
    [InlineData(ChangedState.Failed)]
    [InlineData(ChangedState.MissingWaiting)]
    [InlineData(ChangedState.Claimed)]
    [InlineData(ChangedState.ClaimAttempt)]
    [InlineData(ChangedState.StepAttempt)]
    [InlineData(ChangedState.Ready)]
    [InlineData(ChangedState.Root)]
    [InlineData(ChangedState.LaterResult)]
    public void Changed_waiting_authority_cannot_be_resumed(ChangedState change) {
        var fixture = new Fixture();
        var state = fixture.Data.State;
        fixture.Data.State = change switch {
            ChangedState.CancelRequested => state with { Status = ProcessRuntimeStatus.CancelRequested },
            ChangedState.Cancelled => state with { Status = ProcessRuntimeStatus.Cancelled },
            ChangedState.Completed => state with { Status = ProcessRuntimeStatus.Completed },
            ChangedState.Failed => state with { Status = ProcessRuntimeStatus.Failed },
            ChangedState.MissingWaiting => state with { AppliedResults = [] },
            ChangedState.Claimed => state with { Claims = [state.Claims[0] with { Status = DispatchClaimStatus.Claimed }] },
            ChangedState.ClaimAttempt => state with { Claims = [state.Claims[0] with { AttemptNumber = 2 }] },
            ChangedState.StepAttempt => state with { Steps = [state.Steps[0] with { AttemptNumber = 2 }] },
            ChangedState.Ready => state with { Steps = [state.Steps[0] with { Status = ProcessRuntimeStepStatus.Ready }] },
            ChangedState.Root => state with { RootRunId = new(Id(99)) },
            ChangedState.LaterResult => state with { AppliedResults = [state.AppliedResults[0], state.AppliedResults[0] with {
                IdempotencyKey = new(Id(99)), AppliedSequence = 5
            }] },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        Assert.Throws<InvalidOperationException>(() => fixture.Request());
    }

    public enum ChangedMutation { Plan, Claims, History, Attempt, StepDefinition, Actor, EventPayload, ProjectionPayload,
        ProjectionSubscriber, EmptyProjectionId, ExtraProjection, Command, ExtraCommand, Timestamp, Accounting, Scope }

    [Theory]
    [InlineData(ChangedMutation.Plan)]
    [InlineData(ChangedMutation.Claims)]
    [InlineData(ChangedMutation.History)]
    [InlineData(ChangedMutation.Attempt)]
    [InlineData(ChangedMutation.StepDefinition)]
    [InlineData(ChangedMutation.Actor)]
    [InlineData(ChangedMutation.EventPayload)]
    [InlineData(ChangedMutation.ProjectionPayload)]
    [InlineData(ChangedMutation.ProjectionSubscriber)]
    [InlineData(ChangedMutation.EmptyProjectionId)]
    [InlineData(ChangedMutation.ExtraProjection)]
    [InlineData(ChangedMutation.Command)]
    [InlineData(ChangedMutation.ExtraCommand)]
    [InlineData(ChangedMutation.Timestamp)]
    [InlineData(ChangedMutation.Accounting)]
    [InlineData(ChangedMutation.Scope)]
    public void Successor_cannot_smuggle_changes_through_a_ready_step(ChangedMutation change) {
        var fixture = new Fixture();
        var request = fixture.Request();
        var mutation = request.Mutation;
        var state = mutation.State;
        request = change switch {
            ChangedMutation.Plan => request with { Mutation = mutation with { State = state with { PlanHash = Hash('f') } } },
            ChangedMutation.Claims => request with { Mutation = mutation with { State = state with { Claims = [] } } },
            ChangedMutation.History => request with { Mutation = mutation with { State = state with { AppliedResults = [] } } },
            ChangedMutation.Attempt => request with { Mutation = mutation with { State = state with { Steps = [state.Steps[0] with { AttemptNumber = 0 }] } } },
            ChangedMutation.StepDefinition => request with { Mutation = mutation with { State = state with {
                Steps = [state.Steps[0] with { StepDefinitionId = new(Id(99)) }]
            } } },
            ChangedMutation.Actor => request with { Mutation = mutation with { Events = [mutation.Events[0] with {
                Actor = new(ProcessEventActorKind.Manager, new("other-manager"))
            }, mutation.Events[1]] } },
            ChangedMutation.EventPayload => request with { Mutation = mutation with { Events = [mutation.Events[0] with { PayloadHash = Hash('f') }, mutation.Events[1]] } },
            ChangedMutation.ProjectionPayload => request with { Mutation = mutation with { OutboxMessages = [mutation.OutboxMessages[0] with { PayloadHash = Hash('f') }, mutation.OutboxMessages[1]] } },
            ChangedMutation.ProjectionSubscriber => request with { Mutation = mutation with { OutboxMessages = [mutation.OutboxMessages[0] with {
                SubscriberKind = ProcessOutboxSubscriberKind.ExternalAdapter
            }, mutation.OutboxMessages[1]] } },
            ChangedMutation.EmptyProjectionId => request with { Mutation = mutation with { OutboxMessages = [mutation.OutboxMessages[0] with {
                MessageId = default
            }, mutation.OutboxMessages[1]] } },
            ChangedMutation.ExtraProjection => request with { Mutation = mutation with { OutboxMessages = [.. mutation.OutboxMessages, mutation.OutboxMessages[0]] } },
            ChangedMutation.Command => request with { CommandId = new(Id(99)) },
            ChangedMutation.ExtraCommand => request with { ConfirmRootCancellation = true },
            ChangedMutation.Timestamp => request with { Mutation = mutation with { State = state with { UpdatedAtUtc = fixture.Data.State.UpdatedAtUtc } } },
            ChangedMutation.Accounting => request with { RefreshAccounting = new(fixture.Intent.Request.CommandId) },
            ChangedMutation.Scope => request with { DecisionRunScope = new(new(new(Id(99)), 1), ProcessDecisionRunScopeCommandKind.BeginReconciliation) },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        Assert.Throws<InvalidOperationException>(() => fixture.Successor.RequireMutation(request, fixture.Intent));
    }

    [Fact]
    public void Successor_identity_is_stable_and_distinct_from_accounting_admission_and_other_actions() {
        var fixture = new Fixture();
        Assert.Equal(fixture.Successor.CommandId, new ProcessDecisionRefreshSuccessor(fixture.Intent.Request.CommandId).CommandId);
        Assert.NotEqual(fixture.Intent.Request.CommandId, fixture.Successor.CommandId);
        Assert.NotEqual(new ProcessDecisionRefreshAccounting(fixture.Intent.Request.CommandId).CommandId, fixture.Successor.CommandId);
        Assert.NotEqual(new ProcessDecisionRefreshSuccessor(new(Id(99))).CommandId, fixture.Successor.CommandId);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProcessDecisionRefreshSuccessor(default).CommandId);
    }

    private sealed class Fixture {
        internal Fixture(bool shared = false) {
            var address = Data.Result.Child;
            if (shared) {
                address = address with { RootRunId = address.RunId };
                Data.State = Data.State with { RootRunId = address.RunId };
            }
            ProcessDecisionRunScopeExpectation? scope = shared ? new(new(Id(40)), 2) : null;
            ProcessDecisionRefreshRequest original = new(new(Id(30)), address, Data.Result.StrategyId, Data.Result.WaitingResultKey,
                new(address.Reference, 2), new(address.StepInstanceId, new(Id(31)), 4), scope, ProcessDecisionRefreshKind.ManagerRefresh);
            Intent = new(original, Data.Context.Actor, new(Id(32)), Hash('a'), Hash('b'));
            Successor = new(original.CommandId);
            Command = new(address.StepInstanceId, new(address.Reference, 4), new(Id(33)),
                shared ? ProcessDecisionRefreshKind.ParentRefresh : ProcessDecisionRefreshKind.ManagerRefresh) {
                AdmissionCommandId = original.CommandId,
                InheritedScope = scope is null ? null : scope with { Revision = 5 },
                Material = scope is null ? null : new(new(scope.ScopeId, 2, new(Id(33))), new string('C', 64))
            };
        }

        internal ProcessDecisionReviewTestData Data { get; } = new();
        internal ProcessDecisionRefreshIntent Intent { get; }
        internal ProcessDecisionRefreshSuccessor Successor { get; }
        internal ProcessDecisionRefresh Command { get; }
        internal ProcessRuntimeCommitRequest Request() => new(Successor.CommandId, Data.State,
            Successor.Prepare(Data.State, Data.Context, Intent, Command)) {
            Decision = Command,
            DecisionBudget = new ProcessDecisionBudgetSettlement(Intent.Request.Budget, new(Id(34)), new(1, new(0, 0)))
        };
    }
}
