using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

internal static class ProcessDecisionPersistenceFixture {
    internal static async Task<ProcessRuntimeCommitResult> StartAsync(ProcessPersistenceDbContext context)
        => await new EfProcessRuntimeUnitOfWork(context).CommitAsync(await StartRequestAsync(context));

    internal static async Task<ProcessRuntimeCommitRequest> StartRequestAsync(ProcessPersistenceDbContext context,
        Func<ProcessDecisionDataPolicyAdmission, ProcessDecisionDataPolicyAdmission>? configureAdmission = null) {
        var initial = ProcessProjectAdmissionFixture.Initial();
        var assignment = initial.InitialAssignments!.Single();
        var admission = ProcessDecisionPolicyFixture.Create(initial.InitialPlan!.Definition.VersionId, assignment.StepInstanceId);
        initial = initial with { InitialAssignments = [assignment with { DecisionDataPolicy = configureAdmission?.Invoke(admission) ?? admission }] };
        var state = (await new EfProcessRuntimeUnitOfWork(context).CommitAsync(initial)).State;
        var reserved = await ReserveCaptureAsync(context, state, 0);
        var budget = reserved.DecisionBudget!.Change.Balance;
        var start = new ProcessDecisionStart(Step(state), new(Guid.NewGuid()), new(Guid.NewGuid()));
        return Request(reserved.State, start,
            new ProcessDecisionBudgetSettlement(new(Step(state), budget.BudgetId, budget.Revision), reserved.DecisionBudget.Command.AllocationId, new(1, new(5, 0))));
    }

    internal static async Task<ProcessRuntimeCommitRequest> EnrollmentRequestAsync(ProcessPersistenceDbContext context,
        ProcessRuntimeStateSnapshot state, ProcessDecisionChildOwner owner = ProcessDecisionChildOwner.Agent) {
        var current = await DecisionAsync(context, state);
        var budget = await BudgetAsync(context, state);
        var child = new ProcessDecisionChild(current.Reference, new(Guid.NewGuid()), owner, new(Guid.NewGuid()));
        return Request(state, new ProcessDecisionEnrollment(Step(state), current.Expectation, child),
            new ProcessDecisionBudgetReservation(new(Step(state), budget.BudgetId, budget.Revision), child.AllocationId, new(20, 0)));
    }

    internal static async Task<ProcessRuntimeCommitResult> EnrollAsync(ProcessPersistenceDbContext context,
        ProcessRuntimeStateSnapshot state, ProcessDecisionChildOwner owner = ProcessDecisionChildOwner.Agent)
        => await new EfProcessRuntimeUnitOfWork(context).CommitAsync(await EnrollmentRequestAsync(context, state, owner));

    internal static async Task<ProcessRuntimeCommitRequest> ClosureRequestAsync(ProcessPersistenceDbContext context,
        ProcessRuntimeStateSnapshot state, ProcessDecisionChild child, ProcessDecisionChildClosure closure, long work) {
        var current = await DecisionAsync(context, state);
        var budget = await BudgetAsync(context, state);
        return Request(state, new ProcessDecisionClosure(Step(state), current.Expectation, child.ChildId, closure),
            new ProcessDecisionBudgetSettlement(new(Step(state), budget.BudgetId, budget.Revision), child.AllocationId, new(closure.OwnerRevision, new(work, 0))));
    }

    internal static async Task<ProcessRuntimeCommitResult> CloseAsync(ProcessPersistenceDbContext context,
        ProcessRuntimeStateSnapshot state, ProcessDecisionChild child, ProcessDecisionChildClosure closure, long work)
        => await new EfProcessRuntimeUnitOfWork(context).CommitAsync(await ClosureRequestAsync(context, state, child, closure, work));

    internal static async Task<ProcessRuntimeCommitResult> PhaseAsync(ProcessPersistenceDbContext context,
        ProcessRuntimeStateSnapshot state, ProcessDecisionPhaseChange change) {
        var current = await DecisionAsync(context, state);
        return await new EfProcessRuntimeUnitOfWork(context).CommitAsync(Request(state, new ProcessDecisionChangePhase(Step(state), current.Expectation, change)));
    }

    internal static async Task<ProcessRuntimeCommitResult> ReserveCaptureAsync(ProcessPersistenceDbContext context, ProcessRuntimeStateSnapshot state, int replans) {
        var budget = await BudgetAsync(context, state);
        return await new EfProcessRuntimeUnitOfWork(context).CommitAsync(Request(state, null,
            new ProcessDecisionBudgetReservation(new(Step(state), budget.BudgetId, budget.Revision), new(Guid.NewGuid()), new(100, replans))));
    }

    internal static async Task<ProcessRuntimeCommitResult> RefreshAsync(ProcessPersistenceDbContext context,
        ProcessRuntimeStateSnapshot state, ProcessDecisionRefreshKind kind) {
        var replans = kind == ProcessDecisionRefreshKind.AutomaticReplan ? 1 : 0;
        var reserved = await ReserveCaptureAsync(context, state, replans);
        var current = await DecisionAsync(context, state);
        var budget = reserved.DecisionBudget!.Change.Balance;
        return await new EfProcessRuntimeUnitOfWork(context).CommitAsync(Request(reserved.State,
            new ProcessDecisionRefresh(Step(state), current.Expectation, new(Guid.NewGuid()), kind),
            new ProcessDecisionBudgetSettlement(new(Step(state), budget.BudgetId, budget.Revision), reserved.DecisionBudget.Command.AllocationId, new(2, new(5, replans)))));
    }

    internal static ProcessRuntimeCommitRequest Request(ProcessRuntimeStateSnapshot state, ProcessDecisionCommand? decision, ProcessDecisionBudgetCommand? budget = null) {
        var step = state.Steps.Single();
        var status = decision switch {
            ProcessDecisionChangePhase { Change: ProcessDecisionPhaseChange.WaitForReview or ProcessDecisionPhaseChange.BeginReconciliation } => ProcessRuntimeStepStatus.Waiting,
            ProcessDecisionChangePhase { Change: ProcessDecisionPhaseChange.Cancel } => ProcessRuntimeStepStatus.Cancelled,
            ProcessDecisionChangePhase { Change: ProcessDecisionPhaseChange.Complete } => ProcessRuntimeStepStatus.Completed,
            ProcessDecisionRefresh => ProcessRuntimeStepStatus.Ready,
            _ => step.Status
        };
        return new(RuntimeCommandId.New(), state, new(ProcessRuntimeTransitionOutcome.Applied,
            state with { UpdatedAtUtc = state.UpdatedAtUtc.AddSeconds(1), Steps = [step with { Status = status }] }, [], [], [], [])) { Decision = decision, DecisionBudget = budget };
    }
    internal static ProcessStepInstanceId Step(ProcessRuntimeStateSnapshot state) => state.Steps.Single().StepInstanceId;
    internal static ProcessDecisionChildClosure NoEffect() => new(9, ProcessDecisionChildOutcome.NoEffect, null);
    internal static async Task<ProcessDecisionGeneration> DecisionAsync(ProcessPersistenceDbContext context, ProcessRuntimeStateSnapshot state)
        => (await new EfProcessDecisionStore(context).ReadAsync(state.RunId, Step(state)))!;
    internal static async Task<DecisionBudgetBalance> BudgetAsync(ProcessPersistenceDbContext context, ProcessRuntimeStateSnapshot state)
        => (await new EfProcessDecisionBudgetStore(context).ReadAsync(state.RunId, Step(state)))!;

    internal static ProcessPersistenceDbContext Context(IServiceProvider services, params IInterceptor[] interceptors) => new(Options(services, interceptors));

    internal static DbContextOptions<ProcessPersistenceDbContext> Options(IServiceProvider services, params IInterceptor[] interceptors) {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        return options.AddInterceptors(interceptors).Options;
    }

}
