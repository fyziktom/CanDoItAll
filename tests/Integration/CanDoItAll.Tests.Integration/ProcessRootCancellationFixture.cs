using System.Text.Json;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

internal static class ProcessRootCancellationFixture {
    internal static RuntimeCommandContext Command(ProcessRuntimeStateSnapshot state)
        => new(RuntimeCommandId.New(), new(ProcessEventActorKind.System, new("root-cancellation-fixture")),
            new("root-cancellation-fixture"), state.UpdatedAtUtc.AddSeconds(1));

    internal static async Task<ProcessRuntimeStateSnapshot> StopAsync(IProcessRuntimeUnitOfWork owner, ProcessRuntimeStateSnapshot state) {
        var result = await new ProcessRuntimeEngine(owner).BeginRootCancellationAsync(state, Command(state));
        Assert.True(result.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.CancelRequested, result.State.Status);
        return result.State;
    }

    internal static async Task<ProcessRuntimeStateSnapshot> LegacyCancelledAsync(ProcessPersistenceDbContext context, ProcessRuntimeStateSnapshot state) {
        // This deliberately retains an old finalized root with unsettled work, which new commits must refuse.
        context.ChangeTracker.Clear();
        var row = await context.RuntimeStates.SingleAsync(item => item.RunId == state.RunId.Value);
        row.Status = ProcessRuntimeStatus.Cancelled;
        row.ConcurrencyToken = Guid.NewGuid();
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return (await new EfProcessRuntimeUnitOfWork(context).LoadAsync(state.RunId))!;
    }

    internal static ProcessRuntimeCommitRequest LocalBudget() {
        var initial = ProcessProjectAdmissionFixture.Initial();
        var assignment = initial.InitialAssignments!.Single();
        return initial with { InitialAssignments = [assignment with {
            DecisionDataPolicy = ProcessDecisionPolicyFixture.Create(initial.InitialPlan!.Definition.VersionId, assignment.StepInstanceId)
        }] };
    }

    internal static ProcessRuntimeCommitRequest Descendant(ProcessRuntimeStateSnapshot parent) {
        var initial = ProcessProjectAdmissionFixture.Initial();
        var state = initial.OriginalState with { RootRunId = parent.RootRunId };
        return initial with { OriginalState = state, Mutation = initial.Mutation with { State = state,
            Events = initial.Mutation.Events.Select(item => item with { RootRunId = parent.RootRunId }).ToArray() },
            ParentStepPrecondition = new(parent.RunId, Step(parent)), InitialAssignments = initial.InitialAssignments!.Select(item => item with {
                LaunchVariables = new Dictionary<string, string> {
                    [ProcessRuntimeLaunchVariables.ParentProcessRunId] = parent.RunId.Value.ToString("D"),
                    [ProcessRuntimeLaunchVariables.ParentProcessStepId] = Step(parent).Value.ToString("D")
                }
            }).ToArray() };
    }

    internal static async Task<string> RetainedAsync(ProcessPersistenceDbContext context, ProcessRuntimeStateSnapshot state) {
        context.ChangeTracker.Clear();
        return JsonSerializer.Serialize(new {
            State = await new EfProcessRuntimeUnitOfWork(context).LoadAsync(state.RunId),
            Events = await context.RuntimeEvents.CountAsync(), Outbox = await context.OutboxMessages.CountAsync(),
            Receipts = await context.IdempotencyKeys.CountAsync(), Ledger = await context.ArtifactLedgerEvents.CountAsync(),
            Budgets = await context.DecisionBudgets.AsNoTracking().OrderBy(item => item.BudgetId).ToArrayAsync(),
            Allocations = await context.DecisionBudgetAllocations.AsNoTracking().OrderBy(item => item.BudgetId).ThenBy(item => item.AllocationId).ToArrayAsync(),
            Heads = await context.DecisionHeads.AsNoTracking().OrderBy(item => item.DecisionId).ToArrayAsync(),
            Generations = await context.DecisionGenerations.AsNoTracking().OrderBy(item => item.DecisionId).ThenBy(item => item.Generation).ToArrayAsync(),
            Children = await context.DecisionChildren.AsNoTracking().OrderBy(item => item.DecisionId).ThenBy(item => item.ChildId).ToArrayAsync(),
            Materials = await context.DecisionMaterials.CountAsync(), Scopes = await context.DecisionRunScopes.AsNoTracking().OrderBy(item => item.ScopeId).ToArrayAsync()
        });
    }
}
