using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRootCancellationConfirmationTests {
    [Fact]
    public void Confirmation_is_explicit_and_absent_from_existing_request_wire() {
        var request = Request();
        EfProcessRootCancellationGuard.ValidateConfirmation(request);
        Assert.Contains("\"ConfirmRootCancellation\":true", JsonSerializer.Serialize(request), StringComparison.Ordinal);
        Assert.DoesNotContain("ConfirmRootCancellation", JsonSerializer.Serialize(request with { ConfirmRootCancellation = false }), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Change.NotCancelled)]
    [InlineData(Change.Descendant)]
    [InlineData(Change.BusinessState)]
    [InlineData(Change.Duplicate)]
    [InlineData(Change.UnchangedTime)]
    [InlineData(Change.ImpreciseTime)]
    [InlineData(Change.InitialPlan)]
    [InlineData(Change.Assignments)]
    [InlineData(Change.Launch)]
    [InlineData(Change.Parent)]
    [InlineData(Change.Dispatch)]
    [InlineData(Change.Budget)]
    [InlineData(Change.Decision)]
    [InlineData(Change.Scope)]
    [InlineData(Change.Events)]
    [InlineData(Change.Outbox)]
    [InlineData(Change.Ledger)]
    [InlineData(Change.Diagnostics)]
    public void Confirmation_cannot_bypass_business_or_effect_guards(Change change) {
        var request = Request();
        var initial = ProcessProjectAdmissionFixture.Initial();
        var step = request.OriginalState.Steps.Single().StepInstanceId;
        request = change switch {
            Change.NotCancelled => request with { OriginalState = request.OriginalState with { Status = ProcessRuntimeStatus.CancelRequested } },
            Change.Descendant => request with { OriginalState = request.OriginalState with { RootRunId = ProcessRunId.New() } },
            Change.BusinessState => request with { Mutation = request.Mutation with { State = request.Mutation.State with { PlanHash = new string('b', 64) } } },
            Change.Duplicate => request with { Mutation = request.Mutation with { Outcome = ProcessRuntimeTransitionOutcome.Duplicate } },
            Change.UnchangedTime => request with { Mutation = request.Mutation with { State = request.OriginalState } },
            Change.ImpreciseTime => request with { Mutation = request.Mutation with { State = request.Mutation.State with { UpdatedAtUtc = request.Mutation.State.UpdatedAtUtc.AddTicks(1) } } },
            Change.InitialPlan => request with { InitialPlan = initial.InitialPlan },
            Change.Assignments => request with { InitialAssignments = [] },
            Change.Launch => request with { InitialLaunchAdmission = new(new(Guid.NewGuid()), new string('a', 64), false) },
            Change.Parent => request with { ParentStepPrecondition = new(ProcessRunId.New(), step) },
            Change.Dispatch => request with { DispatchPrecondition = new(step, new(Guid.NewGuid()), new("confirmation-test")) },
            Change.Budget => request with { DecisionBudget = new ProcessDecisionBudgetReservation(new(step, new(Guid.NewGuid()), 1), new(Guid.NewGuid()), new(1, 0)) },
            Change.Decision => request with { Decision = new ProcessDecisionStart(step, new(Guid.NewGuid()), new(Guid.NewGuid())) },
            Change.Scope => request with { DecisionRunScope = new(new(new(Guid.NewGuid()), 1), ProcessDecisionRunScopeCommandKind.Cancel) },
            Change.Events => request with { Mutation = request.Mutation with { Events = initial.Mutation.Events } },
            Change.Outbox => request with { Mutation = request.Mutation with { OutboxMessages = initial.Mutation.OutboxMessages } },
            Change.Ledger => request with { Mutation = request.Mutation with { ArtifactLedgerEvents = initial.Mutation.ArtifactLedgerEvents } },
            _ => request with { Mutation = request.Mutation with { Diagnostics = [new("confirmation-test", "Unresolved work.")] } }
        };
        Assert.Throws<InvalidOperationException>(() => EfProcessRootCancellationGuard.ValidateConfirmation(request));
    }

    private static ProcessRuntimeCommitRequest Request() {
        var state = ProcessProjectAdmissionFixture.Initial().OriginalState with { Status = ProcessRuntimeStatus.Cancelled };
        return new(RuntimeCommandId.New(), state, new(ProcessRuntimeTransitionOutcome.Applied,
            state with { UpdatedAtUtc = state.UpdatedAtUtc.AddSeconds(1) }, [], [], [], [])) { ConfirmRootCancellation = true };
    }

    public enum Change { NotCancelled, Descendant, BusinessState, Duplicate, UnchangedTime, ImpreciseTime,
        InitialPlan, Assignments, Launch, Parent, Dispatch, Budget, Decision, Scope, Events, Outbox, Ledger, Diagnostics }
}
