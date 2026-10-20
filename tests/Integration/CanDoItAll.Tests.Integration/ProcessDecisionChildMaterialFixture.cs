using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionRunScopeFixture;

namespace CanDoItAll.Tests.Integration.Processes;

internal sealed record ProcessDecisionChildMaterialFixture(ProcessRuntimeCommitResult Enrolled,
    ProcessDecisionChildMaterialBinding Binding, ProcessDecisionMaterial Material) {
    internal static async Task<ProcessDecisionChildMaterialFixture> CreateAsync(ProcessPersistenceDbContext context,
        IServiceProvider services, ReadBasisMode mode = ReadBasisMode.RunSnapshot, bool descendant = false,
        ProcessDecisionChildOwner executor = ProcessDecisionChildOwner.Agent) {
        var owner = Owner(context, services);
        var initial = Initial(services);
        if (mode == ReadBasisMode.StepSnapshot) {
            var root = ProcessDecisionRunPolicyFixture.Root(initial.OriginalState.RunId, initial.InitialPlan!.Definition.VersionId,
                services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id);
            initial = initial with { InitialRunDecisionAdmission = root, InitialAssignments = initial.InitialAssignments!.Select(item => item with {
                DecisionDataPolicy = ProcessDecisionRunPolicyFixture.Child(root, item.StepInstanceId, executor: executor)
            }).ToArray() };
        }
        initial = initial with { InitialAssignments = initial.InitialAssignments!.Select(item => item with {
            ExecutorKind = executor == ProcessDecisionChildOwner.Agent ? ProcessLaunchExecutorKinds.Agent : ProcessLaunchExecutorKinds.Workflow,
            DecisionDataPolicy = item.DecisionDataPolicy! with { ExecutionContract = ProcessDecisionPolicyFixture.Contract(executor) }
        }).ToArray() };
        var state = (await owner.CommitAsync(initial)).State;
        ProcessRuntimeCommitResult joined;
        ProcessDecisionMaterial material;
        if (mode == ReadBasisMode.RunSnapshot) {
            var captured = await CaptureAsync(context, services, state,
                (await Scopes(context, services).ReadAsync(initial.InitialRunDecisionAdmission!.MaterialScope!.ScopeId))!);
            joined = await owner.CommitAsync(await JoinRequestAsync(context, services, captured.State, captured.DecisionRunScope!.State));
            if (descendant) {
                var parent = (await owner.CommitAsync(Claim(joined.State))).State;
                var nested = Descendant(parent, initial.InitialRunDecisionAdmission!, executor);
                nested = nested with { InitialAssignments = nested.InitialAssignments!.Select(item => item with {
                    ExecutorKind = executor == ProcessDecisionChildOwner.Agent ? ProcessLaunchExecutorKinds.Agent : ProcessLaunchExecutorKinds.Workflow
                }).ToArray() };
                var child = (await owner.CommitAsync(nested)).State;
                var scope = (await Scopes(context, services).ReadAsync(captured.DecisionRunScope.State.Admission.ScopeId))!;
                joined = await owner.CommitAsync(await JoinRequestAsync(context, services, child, scope));
            }
            material = (await new EfProcessDecisionMaterialStore(context, services.GetRequiredService<ICanonicalRuntimeDatabase>())
                .ReadAsync(captured.DecisionRunScope.State.Admission.MaterialScope, captured.DecisionRunScope.State.Material!.Stamp.Reference))!;
        } else {
            var root = initial.InitialRunDecisionAdmission!;
            ProcessDecisionMaterialScope scope = new(new(Guid.NewGuid()), root.DatabaseProfileId, root.WorkspaceNamespaceId,
                state.RootRunId, state.RunId, Step(state));
            material = ProcessDecisionMaterial.FromContent(scope, new(scope.Id, 1, new(Guid.NewGuid())),
                ProcessDecisionPolicyFixture.ContentContract(), ProcessProjectAdmissionFixture.Now, "{\"facts\":[\"original\"]}");
            var budget = await BudgetAsync(context, state);
            var allocation = new DecisionBudgetAllocationId(Guid.NewGuid());
            var reserved = await owner.CommitAsync(Request(state, null,
                new ProcessDecisionBudgetReservation(new(Step(state), budget.BudgetId, budget.Revision), allocation, new(10, 0))));
            budget = reserved.DecisionBudget!.Change.Balance;
            joined = await owner.CommitAsync(Request(reserved.State,
                new ProcessDecisionStart(Step(state), new(Guid.NewGuid()), material.Reference.MaterialId) { Material = material.Stamp },
                new ProcessDecisionBudgetSettlement(new(Step(state), budget.BudgetId, budget.Revision), allocation, new(1, new(2, 0))))
                with { DecisionMaterial = material });
        }
        var enrolled = await owner.CommitAsync(await EnrollmentRequestAsync(context, joined.State, executor));
        ProcessDecisionChildAddress address = new(enrolled.State.RootRunId, enrolled.State.RunId, Step(enrolled.State),
            enrolled.Decision!.Current.Reference, enrolled.Decision.Child!.ChildId);
        var childAdmission = (await services.GetRequiredService<IProcessDecisionChildReader>().ReadAsync(address))!.Admission;
        return new(enrolled, new(childAdmission, material.Scope, material.Stamp, ProcessDecisionPolicyFixture.Contract(executor)), material);
    }
}
