using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessDecisionBudgetStore(ProcessPersistenceDbContext dbContext) {
    public async Task<DecisionBudgetBalance?> ReadAsync(ProcessRunId runId, ProcessStepInstanceId stepInstanceId,
        CancellationToken cancellationToken = default) {
        var entity = await dbContext.DecisionBudgets.AsNoTracking()
            .SingleOrDefaultAsync(item => item.RunId == runId.Value && item.StepInstanceId == stepInstanceId.Value, cancellationToken).ConfigureAwait(false);
        return entity is null ? null : ReadBalance(entity);
    }

    public async Task<DecisionBudgetAllocation?> ReadAllocationAsync(DecisionBudgetId budgetId, DecisionBudgetAllocationId allocationId,
        CancellationToken cancellationToken = default) {
        var entity = await dbContext.DecisionBudgetAllocations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BudgetId == budgetId.Value && item.AllocationId == allocationId.Value, cancellationToken).ConfigureAwait(false);
        return entity is null ? null : ReadAllocation(entity);
    }

    internal void StageInitial(ProcessRunId rootRunId, IReadOnlyList<ProcessRuntimeStepAssignment> assignments) {
        foreach (var assignment in assignments) {
            if (assignment.DecisionDataPolicy is not { } admission) {
                continue;
            }
            admission.Validate();
            var balance = DecisionBudgetTransitions.Create(new(Guid.NewGuid()), admission.Policy.Budget);
            var entity = new ProcessDecisionBudgetEntity {
                BudgetId = balance.BudgetId.Value, RootRunId = rootRunId.Value, RunId = assignment.RunId.Value,
                StepInstanceId = assignment.StepInstanceId.Value, MaximumWorkTicks = balance.Ceiling.MaximumWorkDuration.Ticks,
                MaximumReplans = balance.Ceiling.MaximumReplans, MaximumBasisAgeTicks = balance.Ceiling.MaximumBasisAge.Ticks
            };
            WriteBalance(entity, balance);
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
        var step = originalState.Steps.SingleOrDefault(item => item.StepInstanceId == expectation.StepInstanceId)
            ?? throw new InvalidOperationException("The accounting step is not part of the original Process state.");
        if (command is ProcessDecisionBudgetReservation && (ProcessRuntimeTerminalStates.IsStepTerminal(step.Status) ||
                ProcessRuntimeTerminalStates.IsRunTerminal(originalState.Status) || originalState.Status == ProcessRuntimeStatus.CancelRequested)) {
            throw new InvalidOperationException("A stopped Process cannot reserve new decision work.");
        }
        var head = await dbContext.DecisionBudgets.SingleOrDefaultAsync(item => item.BudgetId == expectation.BudgetId.Value,
            cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("The original Process budget was not found.");
        if (head.RootRunId != originalState.RootRunId.Value || head.RunId != originalState.RunId.Value ||
                head.StepInstanceId != expectation.StepInstanceId.Value || head.Revision != expectation.Revision) {
            throw new InvalidOperationException("The Process budget identity or revision no longer matches the accounting command.");
        }
        var current = ReadBalance(head);
        var entity = await dbContext.DecisionBudgetAllocations.SingleOrDefaultAsync(item =>
            item.BudgetId == head.BudgetId && item.AllocationId == command.AllocationId.Value, cancellationToken).ConfigureAwait(false);
        var allocation = entity is null ? null : ReadAllocation(entity);
        var change = command switch {
            ProcessDecisionBudgetReservation reservation => DecisionBudgetTransitions.Reserve(current, command.AllocationId, reservation.Grant, allocation),
            ProcessDecisionBudgetSettlement settlement when allocation is not null => DecisionBudgetTransitions.Settle(current, allocation, settlement.Settlement),
            _ => throw new InvalidOperationException("Settlement requires the original retained Process allocation.")
        };
        if (change.Changed) {
            WriteBalance(head, change.Balance);
            if (entity is null) {
                entity = new() { BudgetId = head.BudgetId, AllocationId = command.AllocationId.Value };
                dbContext.DecisionBudgetAllocations.Add(entity);
            }
            entity.GrantedWorkTicks = change.Allocation.Grant.WorkTicks;
            entity.GrantedReplans = change.Allocation.Grant.Replans;
            entity.OwnerWorkRevision = change.Allocation.Settlement?.OwnerWorkRevision;
            entity.ConsumedWorkTicks = change.Allocation.Settlement?.Consumed.WorkTicks;
            entity.ConsumedReplans = change.Allocation.Settlement?.Consumed.Replans;
        }
        return new(command, change);
    }

    private static DecisionBudgetBalance ReadBalance(ProcessDecisionBudgetEntity entity) {
        var balance = new DecisionBudgetBalance(new(entity.BudgetId), entity.Revision,
            new(entity.MaximumReplans, TimeSpan.FromTicks(entity.MaximumWorkTicks), TimeSpan.FromTicks(entity.MaximumBasisAgeTicks)),
            entity.ConsumedWorkTicks, entity.ReservedWorkTicks, entity.ConsumedReplans, entity.ReservedReplans, entity.AllocationCount, entity.OverrunDetected);
        balance.Validate();
        return balance;
    }

    private static DecisionBudgetAllocation ReadAllocation(ProcessDecisionBudgetAllocationEntity entity) {
        if (entity.OwnerWorkRevision.HasValue != entity.ConsumedWorkTicks.HasValue || entity.OwnerWorkRevision.HasValue != entity.ConsumedReplans.HasValue) {
            throw new InvalidDataException("The retained Process settlement is incomplete.");
        }
        var allocation = new DecisionBudgetAllocation(new(entity.BudgetId), new(entity.AllocationId), new(entity.GrantedWorkTicks, entity.GrantedReplans),
            entity.OwnerWorkRevision is { } revision ? new(revision, new(entity.ConsumedWorkTicks!.Value, entity.ConsumedReplans!.Value)) : null);
        allocation.Validate();
        return allocation;
    }

    private static void WriteBalance(ProcessDecisionBudgetEntity entity, DecisionBudgetBalance balance) {
        entity.Revision = balance.Revision;
        entity.ConsumedWorkTicks = balance.ConsumedWorkTicks;
        entity.ReservedWorkTicks = balance.ReservedWorkTicks;
        entity.ConsumedReplans = balance.ConsumedReplans;
        entity.ReservedReplans = balance.ReservedReplans;
        entity.AllocationCount = balance.AllocationCount;
        entity.OverrunDetected = balance.OverrunDetected;
    }
}
