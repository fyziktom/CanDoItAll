using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

internal static class ProcessDecisionRunScopeFixture {
    internal static ProcessRuntimeCommitRequest Initial(IServiceProvider services, OnBasisChange change = OnBasisChange.Replan,
        ProcessRuntimeCommitRequest? request = null) {
        var initial = request ?? ProcessProjectAdmissionFixture.Initial();
        var root = ProcessDecisionRunPolicyFixture.WithSharedScope(ProcessDecisionRunPolicyFixture.Root(initial.OriginalState.RunId,
            initial.InitialPlan!.Definition.VersionId, services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id, ReadBasisMode.RunSnapshot, change));
        return initial with { InitialRunDecisionAdmission = root, InitialAssignments = initial.InitialAssignments!.Select(item => item with {
            DecisionDataPolicy = ProcessDecisionRunPolicyFixture.Child(root, item.StepInstanceId)
        }).ToArray() };
    }

    internal static EfProcessRuntimeUnitOfWork Owner(ProcessPersistenceDbContext context, IServiceProvider services)
        => new(context, timeProvider: new FixtureClock(), runtimeDatabase: services.GetRequiredService<ICanonicalRuntimeDatabase>());

    internal static EfProcessDecisionRunScopeStore Scopes(ProcessPersistenceDbContext context, IServiceProvider services)
        => new(context, services.GetRequiredService<ICanonicalRuntimeDatabase>());

    internal static async Task<ProcessRuntimeCommitResult> CaptureAsync(ProcessPersistenceDbContext context, IServiceProvider services,
        ProcessRuntimeStateSnapshot state, ProcessDecisionRunScopeState scope, ProcessDecisionRunCaptureKind kind = ProcessDecisionRunCaptureKind.Initial) {
        var begun = await Owner(context, services).CommitAsync(await BeginRequestAsync(context, state, scope, kind));
        return await Owner(context, services).CommitAsync(await CompleteRequestAsync(context, begun.State, begun.DecisionRunScope!.State));
    }

    internal static async Task<ProcessRuntimeCommitRequest> BeginRequestAsync(ProcessPersistenceDbContext context, ProcessRuntimeStateSnapshot state,
        ProcessDecisionRunScopeState scope, ProcessDecisionRunCaptureKind kind = ProcessDecisionRunCaptureKind.Initial) {
        var root = (await new EfProcessDecisionBudgetStore(context).ReadAsync(state.RootRunId, null))!;
        var allocation = new DecisionBudgetAllocationId(Guid.NewGuid());
        return ScopeRequest(state, new(scope.Expectation, ProcessDecisionRunScopeCommandKind.BeginCapture, new(allocation, new(Guid.NewGuid()), kind)),
            new ProcessDecisionBudgetReservation(new(null, root.BudgetId, root.Revision), allocation, new(10, kind == ProcessDecisionRunCaptureKind.AutomaticReplan ? 1 : 0)));
    }

    internal static async Task<ProcessRuntimeCommitRequest> CompleteRequestAsync(ProcessPersistenceDbContext context, ProcessRuntimeStateSnapshot state,
        ProcessDecisionRunScopeState scope, long consumed = 3) {
        var material = ProcessDecisionMaterial.FromContent(scope.Admission.MaterialScope,
            new(scope.Admission.ScopeId, (scope.Material?.Stamp.Reference.Generation ?? 0) + 1, scope.Capture!.MaterialId),
            scope.Admission.ContentContract, ProcessProjectAdmissionFixture.Now, "{\"facts\":[]}");
        var root = (await new EfProcessDecisionBudgetStore(context).ReadAsync(state.RootRunId, null))!;
        return ScopeRequest(state, new(scope.Expectation, ProcessDecisionRunScopeCommandKind.CompleteCapture, Material: material.Stamp),
            new ProcessDecisionBudgetSettlement(new(null, root.BudgetId, root.Revision), scope.Capture.AllocationId,
                new(7, new(consumed, scope.Capture.Kind == ProcessDecisionRunCaptureKind.AutomaticReplan ? 1 : 0)))) with { DecisionMaterial = material };
    }

    internal static ProcessRuntimeCommitRequest ScopeRequest(ProcessRuntimeStateSnapshot state, ProcessDecisionRunScopeCommand command,
        ProcessDecisionBudgetCommand? budget = null) => Request(state, null, budget) with { DecisionRunScope = command };

    internal static async Task<ProcessRuntimeCommitRequest> JoinRequestAsync(ProcessPersistenceDbContext context, IServiceProvider services,
        ProcessRuntimeStateSnapshot state, ProcessDecisionRunScopeState scope, ProcessDecisionGeneration? previous = null) {
        var local = await BudgetAsync(context, state);
        var reservation = new ProcessDecisionBudgetReservation(new(Step(state), local.BudgetId, local.Revision), new(Guid.NewGuid()), new(10, 0));
        var reserved = await Owner(context, services).CommitAsync(Request(state, null, reservation));
        local = reserved.DecisionBudget!.Change.Balance;
        ProcessDecisionCommand command = previous is null
            ? new ProcessDecisionStart(Step(state), new(Guid.NewGuid()), scope.Material!.Stamp.Reference.MaterialId)
            : new ProcessDecisionRefresh(Step(state), previous.Expectation, scope.Material!.Stamp.Reference.MaterialId, ProcessDecisionRefreshKind.ParentRefresh);
        return Request(reserved.State, command with { Material = scope.Material.Stamp, InheritedScope = scope.Expectation },
            new ProcessDecisionBudgetSettlement(new(Step(state), local.BudgetId, local.Revision), reservation.AllocationId, new(1, new(2, 0))));
    }

    internal static ProcessRuntimeCommitRequest Descendant(ProcessRuntimeStateSnapshot parent, ProcessDecisionRunAdmission root,
        ProcessDecisionChildOwner executor = ProcessDecisionChildOwner.Workflow) {
        var initial = ProcessProjectAdmissionFixture.Initial();
        var state = initial.OriginalState with { RootRunId = parent.RootRunId };
        return initial with { OriginalState = state, Mutation = initial.Mutation with { State = state,
            Events = initial.Mutation.Events.Select(item => item with { RootRunId = parent.RootRunId }).ToArray() },
            ParentStepPrecondition = new(parent.RunId, Step(parent)),
            InitialAssignments = initial.InitialAssignments!.Select(item => item with {
                DecisionDataPolicy = ProcessDecisionRunPolicyFixture.Child(root, item.StepInstanceId, initial.InitialPlan!.Definition.VersionId, executor),
                LaunchVariables = new Dictionary<string, string> {
                    [ProcessRuntimeLaunchVariables.ParentProcessRunId] = parent.RunId.Value.ToString("D"),
                    [ProcessRuntimeLaunchVariables.ParentProcessStepId] = Step(parent).Value.ToString("D")
                }
            }).ToArray() };
    }

    internal static ProcessRuntimeCommitRequest Claim(ProcessRuntimeStateSnapshot state) {
        var request = Request(state, null);
        var claim = new DispatchClaimState(new(Guid.NewGuid()), Step(state), new("shared-material-fixture"), DispatchClaimStatus.Claimed,
            state.Steps.Single().AttemptNumber + 1, state.UpdatedAtUtc, state.UpdatedAtUtc.AddHours(1), null, null);
        return request with { Mutation = request.Mutation with { State = request.Mutation.State with { Status = ProcessRuntimeStatus.Active,
            Steps = [state.Steps.Single() with { Status = ProcessRuntimeStepStatus.Running, AttemptNumber = claim.AttemptNumber, ActiveClaimToken = claim.ClaimToken }],
            Claims = [.. state.Claims.Select(item => item with { Status = DispatchClaimStatus.Released }), claim]
        } } };
    }

    internal static async Task<ProcessRuntimeCommitResult> QuiesceAsync(ProcessPersistenceDbContext context, IServiceProvider services,
        ProcessRuntimeStateSnapshot state, ProcessDecisionPhaseChange phase = ProcessDecisionPhaseChange.BeginReconciliation) {
        var decision = await DecisionAsync(context, state);
        var request = Request(state, new ProcessDecisionChangePhase(Step(state), decision.Expectation, phase));
        return await Owner(context, services).CommitAsync(request with { Mutation = request.Mutation with { State = request.Mutation.State with {
            Steps = [request.Mutation.State.Steps.Single() with { ActiveClaimToken = null }],
            Claims = state.Claims.Select(item => item with { Status = DispatchClaimStatus.Released }).ToArray()
        } } });
    }

    private sealed class FixtureClock : TimeProvider {
        public override DateTimeOffset GetUtcNow() => ProcessProjectAdmissionFixture.Now;
    }
}
