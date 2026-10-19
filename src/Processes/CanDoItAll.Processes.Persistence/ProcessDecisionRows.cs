using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessDecisionRows {
    internal static ProcessDecisionGeneration RequireHead(ProcessDecisionHeadEntity head, ProcessDecisionGenerationEntity entity) {
        var generation = ReadGeneration(entity);
        if (head.DecisionId != entity.DecisionId || head.CurrentGeneration != entity.Generation || head.Revision != entity.Revision || head.BudgetId != entity.BudgetId) {
            throw new InvalidDataException("The current Process decision head and generation disagree.");
        }
        return generation;
    }

    internal static ProcessDecisionGeneration ReadGeneration(ProcessDecisionGenerationEntity entity) {
        var generation = new ProcessDecisionGeneration(new(new(entity.DecisionId), entity.Generation), entity.Revision, entity.Phase,
            new(entity.MaterialId), new(entity.BudgetId), entity.ChildCount, entity.UnclosedChildCount);
        generation.Validate();
        return generation;
    }

    internal static ProcessDecisionChild ReadChild(ProcessDecisionChildEntity entity) {
        if (entity.OwnerRevision.HasValue != entity.Outcome.HasValue || entity.OwnerRevision is null && entity.AcceptedReceiptId is not null) {
            throw new InvalidDataException("The retained Process child closure is incomplete.");
        }
        var child = new ProcessDecisionChild(new(new(entity.DecisionId), entity.Generation), new(entity.ChildId), entity.Owner, new(entity.AllocationId),
            entity.OwnerRevision is { } revision ? new(revision, entity.Outcome!.Value,
                entity.AcceptedReceiptId is { } receipt ? new ProcessDecisionEffectReceiptId(receipt) : null) : null);
        child.Validate();
        return child;
    }

    internal static DecisionBudgetBalance ReadBalance(ProcessDecisionBudgetEntity entity) {
        var balance = new DecisionBudgetBalance(new(entity.BudgetId), entity.Revision,
            new(entity.MaximumReplans, TimeSpan.FromTicks(entity.MaximumWorkTicks), TimeSpan.FromTicks(entity.MaximumBasisAgeTicks)),
            entity.ConsumedWorkTicks, entity.ReservedWorkTicks, entity.ConsumedReplans, entity.ReservedReplans, entity.AllocationCount, entity.OverrunDetected);
        balance.Validate();
        return balance;
    }

    internal static DecisionBudgetAllocation ReadAllocation(ProcessDecisionBudgetAllocationEntity entity) {
        if (entity.OwnerWorkRevision.HasValue != entity.ConsumedWorkTicks.HasValue || entity.OwnerWorkRevision.HasValue != entity.ConsumedReplans.HasValue) {
            throw new InvalidDataException("The retained Process settlement is incomplete.");
        }
        var allocation = new DecisionBudgetAllocation(new(entity.BudgetId), new(entity.AllocationId), new(entity.GrantedWorkTicks, entity.GrantedReplans),
            entity.OwnerWorkRevision is { } revision ? new(revision, new(entity.ConsumedWorkTicks!.Value, entity.ConsumedReplans!.Value)) : null);
        allocation.Validate();
        return allocation;
    }
}
