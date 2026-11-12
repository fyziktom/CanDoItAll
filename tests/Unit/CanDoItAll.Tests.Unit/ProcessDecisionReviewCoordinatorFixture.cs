using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Unit.ProcessDecisionReviewTestData;

namespace CanDoItAll.Tests.Unit;

internal sealed class ProcessDecisionReviewCoordinatorFixture : IProcessAcceptedExecutionInputReader,
    IProcessDecisionResultQuery, IProcessDecisionChildOutcomeReader, IProcessIdempotencyStore, IProcessRuntimeUnitOfWork {
    internal ProcessDecisionReviewCoordinatorFixture() {
        var runtime = new ProcessDecisionReviewTestData();
        State = runtime.State;
        Context = runtime.Context;
        var address = runtime.Result.Child;
        var ceiling = new DecisionDataPolicyBudget(0, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
        var reserved = DecisionBudgetTransitions.Reserve(DecisionBudgetTransitions.Create(new(Id(18)), ceiling),
            new(Id(19)), new(TimeSpan.FromSeconds(10).Ticks, 0));
        ProcessDecisionChildAdmission child = new(address, new(Id(15)), ProcessDecisionChildOwner.Agent,
            reserved.Balance.BudgetId, reserved.Allocation.Id, reserved.Allocation.Grant);
        ProcessDecisionChildMaterialBinding binding = new(child,
            new(new(Id(20)), Id(16), Id(17), State.RootRunId, State.RunId, address.StepInstanceId),
            new(new(new(Id(20)), 1, child.MaterialId), new string('A', 64)), Contract);
        ProcessDecisionGeneration head = new(address.Reference, 3, ProcessDecisionPhase.WaitingForReview,
            child.MaterialId, child.BudgetId, 1, 1);
        Selection = new(State.RootRunId, State.RunId, address.StepInstanceId, reserved.Balance, head,
            new(binding, new(child, head, null, null, true)), false, false);
        Request = new(address, runtime.Result.StrategyId, runtime.Result.WaitingResultKey, runtime.Result.CompletionKey,
            head.Expectation, new(address.StepInstanceId, child.BudgetId, reserved.Balance.Revision));
        var policy = DecisionDataPolicy.PlanningDefault(ceiling.MaximumWorkDuration, ceiling.MaximumBasisAge);
        ProcessDecisionDataPolicyAdmission admission = new(Id(17), Id(21), new(Id(22)), address.StepInstanceId,
            policy, null, null, null, new(true, true, true, true, true, false, PublicationFreshness.RequireCurrent, ceiling), [], policy) {
            ExecutionContract = Contract, MaterialProtocol = ProcessDecisionMaterialProtocol.CanonicalSnapshotV1
        };
        ProcessRuntimeStepAssignment assignment = new(State.RunId, State.PlanId, address.StepInstanceId,
            "review-step", "review-role", "review-resource", "Review role", "agent", Id(23).ToString("D"), "Review agent",
            "Original review input", Hash('c'), "Original selection", [], [], [], string.Empty, new Dictionary<string, string>(), null, Now) {
            DecisionDataPolicy = admission
        };
        Input = new(new(Id(11)), State.RootRunId, Hash('d'), Hash('e'),
            new(new ProcessLaunchPrincipal.LocalOperator(ProcessLaunchOperatorSurface.UserInterface), Id(16), null, true, false, "review-fixture"),
            assignment) {
            Requirements = new(State.Steps[0].StepDefinitionId, ProcessStepKind.Activity, false, [], [], [], [], [])
        };
        Closed = new(ProcessDecisionChildTerminalStatus.Succeeded, runtime.Result.Closure, new(runtime.Result.Closure.OwnerRevision, new(TimeSpan.FromSeconds(2).Ticks, 0)));
        Outcome = Closed;
    }

    internal ProcessRuntimeStateSnapshot State { get; set; }
    internal RuntimeCommandContext Context { get; set; }
    internal ProcessAcceptedExecutionInput Input { get; set; }
    internal ProcessDecisionResultSelection Selection { get; set; }
    internal ProcessDecisionReviewRequest Request { get; set; }
    internal ProcessDecisionChildResult.Closed Closed { get; }
    internal ProcessDecisionChildResult Outcome { get; set; }
    internal Exception? OutcomeFailure { get; set; }
    internal int InputReads { get; private set; }
    internal int OutcomeReads { get; private set; }
    internal int Queries { get; private set; }
    internal int Commits { get; private set; }
    internal ProcessRuntimeCommitRequest? LastCommit { get; private set; }
    internal RuntimeCommandId Lookup { get; private set; }
    internal Dictionary<RuntimeCommandId, ProcessRuntimeCommitResult> Saved { get; } = [];
    public ProcessDecisionExecutionContract Contract { get; } = new(Id(14), 1, ProcessDecisionChildOwner.Agent);

    internal Task<ProcessRuntimeCommitResult> CompleteAsync()
        => new ProcessDecisionReviewCoordinator(this, this, [this], this, this, NullLogger<ProcessDecisionReviewCoordinator>.Instance)
            .CompleteAsync(State, Context, Request);

    internal void SettleChildWithoutCompletingStep() {
        var child = Selection.Child!;
        var change = ProcessDecisionTransitions.CloseChild(Selection.Head!, Selection.Head!.Expectation,
            new(child.Original.Child.Address.Reference, child.Original.Child.Address.ChildId, child.Original.Child.Owner,
                child.Original.Child.AllocationId), Closed.Closure);
        var budget = DecisionBudgetTransitions.Settle(Selection.Budget,
            new(child.Original.Child.BudgetId, child.Original.Child.AllocationId, child.Original.Child.Grant), Closed.Work);
        Selection = Selection with {
            Head = change.Generation, Budget = budget.Balance,
            Child = child with { Observation = child.Observation with {
                Generation = change.Generation, Closure = Closed.Closure, Settlement = Closed.Work
            } }
        };
        Request = Request with { Decision = change.Generation.Expectation, Budget = Request.Budget with { Revision = budget.Balance.Revision } };
    }

    public Task<ProcessAcceptedExecutionInput> ReadAsync(ProcessRunId runId, ProcessStepInstanceId stepId,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        InputReads++;
        Assert.Equal(Request.Child.RunId, runId);
        Assert.Equal(Request.Child.StepInstanceId, stepId);
        return Task.FromResult(Input);
    }

    public Task<ProcessDecisionResultSelection?> ReadAsync(ProcessRunId runId, ProcessStepInstanceId stepId,
        ProcessExecutionRunId? executionRunId, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        Queries++;
        Assert.Equal(State.RunId, runId);
        Assert.Equal(Request.Child.StepInstanceId, stepId);
        Assert.Equal(new ProcessExecutionRunId(Request.Child.ChildId.Value), executionRunId);
        return Task.FromResult<ProcessDecisionResultSelection?>(Selection);
    }

    public Task<ProcessDecisionChildResult> ReadAsync(ProcessDecisionChildMaterialBinding original, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        OutcomeReads++;
        Assert.Equal(Selection.Child!.Original, original);
        return OutcomeFailure is null ? Task.FromResult(Outcome) : Task.FromException<ProcessDecisionChildResult>(OutcomeFailure);
    }

    public Task<ProcessRuntimeCommitResult?> FindCompletedCommandAsync(RuntimeCommandId commandId, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        Lookup = commandId;
        return Task.FromResult(Saved.GetValueOrDefault(commandId));
    }

    public Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        Commits++;
        LastCommit = request;
        var closure = Assert.IsType<ProcessDecisionClosure>(request.Decision);
        var accounting = Assert.IsType<ProcessDecisionBudgetSettlement>(request.DecisionBudget);
        var child = Selection.Child!;
        var change = ProcessDecisionTransitions.CloseChild(Selection.Head!, closure.Expectation,
            new(child.Original.Child.Address.Reference, child.Original.Child.Address.ChildId, child.Original.Child.Owner,
                child.Original.Child.AllocationId, child.Observation.Closure), closure.Closure, closure.Completion);
        var budget = DecisionBudgetTransitions.Settle(Selection.Budget,
            new(child.Original.Child.BudgetId, child.Original.Child.AllocationId, child.Original.Child.Grant, child.Observation.Settlement),
            accounting.Settlement);
        var result = ProcessRuntimeCommitResult.FromMutation(request.Mutation) with {
            Decision = new(closure, change.Changed, change.Generation, Child: change.Child),
            DecisionBudget = new(accounting, budget), CommittedNow = true
        };
        Saved.Add(request.CommandId, result);
        return Task.FromResult(result);
    }
}
