using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using static CanDoItAll.Processes.Persistence.ProcessDecisionRows;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessDecisionBudgetStore(ProcessPersistenceDbContext dbContext, ICanonicalRuntimeDatabase? runtimeDatabase = null) {
    private readonly EfProcessDecisionFundingStore funding = new(dbContext, runtimeDatabase);

    internal Task RequireOriginalInitialAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken)
        => funding.RequireOriginalInitialAsync(request, cancellationToken);

    public async Task<DecisionBudgetBalance?> ReadAsync(ProcessRunId runId, ProcessStepInstanceId? stepInstanceId,
        CancellationToken cancellationToken = default) {
        var stepId = stepInstanceId?.Value;
        var entity = await dbContext.DecisionBudgets.AsNoTracking()
            .SingleOrDefaultAsync(item => item.RunId == runId.Value && item.StepInstanceId == stepId, cancellationToken).ConfigureAwait(false);
        return entity is null ? null : ReadBalance(entity);
    }

    public async Task<DecisionBudgetAllocation?> ReadAllocationAsync(DecisionBudgetId budgetId, DecisionBudgetAllocationId allocationId,
        CancellationToken cancellationToken = default) {
        var entity = await dbContext.DecisionBudgetAllocations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BudgetId == budgetId.Value && item.AllocationId == allocationId.Value, cancellationToken).ConfigureAwait(false);
        return entity is null ? null : ReadAllocation(entity);
    }

    internal async Task StageInitialAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken) {
        var root = await funding.StageInitialAsync(request, cancellationToken).ConfigureAwait(false);
        foreach (var assignment in request.InitialAssignments!) {
            if (assignment.DecisionDataPolicy is not { } admission) {
                continue;
            }
            admission.Validate();
            var entity = NewBudget(new(Guid.NewGuid()), request.Mutation.State.RootRunId.Value, assignment.RunId.Value,
                assignment.StepInstanceId.Value, admission.Policy.Budget);
            entity.ParentBudgetId = root?.BudgetId.Value;
            dbContext.DecisionBudgets.Add(entity);
        }
    }

    internal async Task<ProcessDecisionBudgetReceipt?> StageAsync(ProcessRuntimeStateSnapshot originalState,
        ProcessDecisionBudgetCommand? command, CancellationToken cancellationToken) {
        if (command is null) {
            return null;
        }
        command.Validate();
        if (dbContext.Database.IsRelational() && dbContext.Database.CurrentTransaction is null) {
            throw new InvalidOperationException("Process accounting requires the runtime owner's fenced transaction.");
        }
        var expectation = command.Expectation;
        var step = expectation.StepInstanceId is { } stepId
            ? originalState.Steps.SingleOrDefault(item => item.StepInstanceId == stepId)
                ?? throw new InvalidOperationException("The accounting step is not part of the original Process state.")
            : null;
        if (command is ProcessDecisionBudgetReservation && (step is not null && ProcessRuntimeTerminalStates.IsStepTerminal(step.Status) ||
                ProcessRuntimeTerminalStates.IsRunTerminal(originalState.Status) || originalState.Status == ProcessRuntimeStatus.CancelRequested)) {
            throw new InvalidOperationException("A stopped Process cannot reserve new decision work.");
        }
        var head = await dbContext.DecisionBudgets.SingleOrDefaultAsync(item => item.BudgetId == expectation.BudgetId.Value,
            cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("The original Process budget was not found.");
        if (head.RootRunId != originalState.RootRunId.Value || head.RunId != originalState.RunId.Value ||
                head.StepInstanceId != expectation.StepInstanceId?.Value || head.Revision != expectation.Revision) {
            throw new InvalidOperationException("The Process budget identity or revision no longer matches the accounting command.");
        }
        if (step is null) {
            await funding.RequireDirectRootAsync(head, originalState, command, cancellationToken).ConfigureAwait(false);
        }
        var entity = await dbContext.DecisionBudgetAllocations.SingleOrDefaultAsync(item =>
            item.BudgetId == head.BudgetId && item.AllocationId == command.AllocationId.Value, cancellationToken).ConfigureAwait(false);
        var change = ApplyAccounting(head, entity, command);
        var funded = await funding.StageAsync(originalState, head, entity, command, cancellationToken).ConfigureAwait(false);
        if (funded is not null && funded.Receipt.Change.Changed != change.Changed) {
            throw new InvalidDataException("Parent funding and local accounting disagree about the original transition.");
        }
        if (change.Changed) {
            WriteBalance(head, change.Balance);
            if (entity is null) {
                entity = new() { BudgetId = head.BudgetId, AllocationId = command.AllocationId.Value,
                    FundingBudgetId = funded?.Receipt.Change.Balance.BudgetId.Value,
                    FundingAllocationId = funded?.Receipt.Command.AllocationId.Value };
                dbContext.DecisionBudgetAllocations.Add(entity);
            }
            WriteAllocation(entity, change.Allocation);
        }
        return new(command, change) { Funding = funded };
    }
}
