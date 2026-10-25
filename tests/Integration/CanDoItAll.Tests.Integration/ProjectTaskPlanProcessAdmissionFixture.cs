using System.Text;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Integration.Processes;
using CanDoItAll.Tests.Integration.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration;

internal sealed record ProjectTaskPlanProcessAdmissionFixture(ProcessRuntimeStateSnapshot State,
    ProcessDecisionChildMaterialBinding Parent, ProcessDecisionMaterial Material, DecisionDataPolicy Policy,
    ProcessExecutionDispatchAuthority Source) {
    internal DateTimeOffset ClaimExpiresAtUtc => State.Claims.Single(item => item.ClaimToken.Value == Source.Evidence.DispatchClaimToken).ExpiresAtUtc;

    internal static async Task<ProjectTaskPlanProcessAdmissionFixture> CreateAsync(ProjectTaskPlanOperationFixture sql,
        ProcessLaunchAuthority launch, Guid executorId, AgentPlanningLiveOwnerFixture.PlanningClock clock) {
        var services = sql.Services;
        var basis = Assert.IsType<ProjectTaskPlanBasisReadResult.Captured>(
            await sql.Capture.CaptureAsync(sql.Project, (_, _) => Task.CompletedTask)).Basis;
        clock.Now = basis.CapturedAtUtc;
        await using var context = Context(services);
        var owner = new EfProcessRuntimeUnitOfWork(context, clock, sql.Transactions,
            services.GetRequiredService<IProcessProjectAdmissionPolicy>(),
            services.GetRequiredService<IProcessLaunchAuthorityPolicy>(),
            runtimeDatabase: services.GetRequiredService<ICanonicalRuntimeDatabase>());
        var preparation = ProcessPreparedLaunchFixture.Create(launch);
        var initial = preparation.InitialCommit;
        var state = initial.OriginalState with { UpdatedAtUtc = clock.Now.AddSeconds(-10) };
        var policy = DecisionDataPolicy.PlanningDefault(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10));
        var capabilities = new DecisionDataPolicyCapabilities(true, true, false, true, true, true,
            PublicationFreshness.RequireCurrent, policy.Budget);
        ProcessDecisionRunAdmission root = new(state.RunId, new(Guid.NewGuid()), sql.Project.DatabaseProfileId,
            sql.Binding.Namespace.Value, Guid.NewGuid(), initial.InitialPlan!.Definition.VersionId,
            policy, null, null, capabilities, [], policy);
        var assignment = initial.InitialAssignments!.Single();
        initial = initial with {
            OriginalState = state, Mutation = initial.Mutation with { State = state }, InitialRunDecisionAdmission = root,
            InitialAssignments = [assignment with {
                ExecutorId = executorId.ToString("D"),
                AllowedOperations = [ProcessOperationContractNames.ReadProjectStructure, ProcessOperationContractNames.ExecuteExternalAction],
                DecisionDataPolicy = ProcessDecisionRunPolicyFixture.Child(root, assignment.StepInstanceId) with {
                    ExecutionContract = ProjectTaskPlanProcessContract.Execution, ContentContract = ProjectTaskPlanProcessContract.Content
                }
            }]
        };
        var prepared = await services.GetRequiredService<IProcessPreparedLaunchStore>().PrepareAsync(preparation with {
            InitialCommit = initial,
            Review = preparation.Review with { Steps = preparation.Review.Steps.Select(item => item with { ExecutorId = executorId.ToString("D") }).ToArray() }
        });
        state = (await owner.CommitAsync(ProcessPreparedLaunchFixture.Commit(prepared))).State;
        ProcessDecisionMaterialScope scope = new(new(Guid.NewGuid()), sql.Project.DatabaseProfileId, sql.Binding.Namespace.Value,
            state.RootRunId, state.RunId, Step(state));
        var material = ProcessDecisionMaterial.FromContent(scope, new(scope.Id, 1, new(Guid.NewGuid())),
            ProjectTaskPlanProcessContract.Content, basis.CapturedAtUtc, Encoding.UTF8.GetString(ProjectTaskPlanBasisCodec.Encode(basis)));
        var budget = await BudgetAsync(context, state);
        var capture = new DecisionBudgetAllocationId(Guid.NewGuid());
        var reserved = await owner.CommitAsync(Request(state, null,
            new ProcessDecisionBudgetReservation(new(Step(state), budget.BudgetId, budget.Revision), capture, new(10, 0))));
        budget = reserved.DecisionBudget!.Change.Balance;
        state = (await owner.CommitAsync(Request(reserved.State,
            new ProcessDecisionStart(Step(state), new(Guid.NewGuid()), material.Reference.MaterialId) { Material = material.Stamp },
            new ProcessDecisionBudgetSettlement(new(Step(state), budget.BudgetId, budget.Revision), capture, new(1, new(2, 0))))
            with { DecisionMaterial = material })).State;
        var enrollment = await EnrollmentRequestAsync(context, state);
        var childBudget = Assert.IsType<ProcessDecisionBudgetReservation>(enrollment.DecisionBudget);
        var enrolled = await owner.CommitAsync(enrollment with {
            DecisionBudget = childBudget with { Grant = new(TimeSpan.FromSeconds(30).Ticks, 0) }
        });
        var claimRequest = ProcessDecisionRunScopeFixture.Claim(enrolled.State);
        claimRequest = claimRequest with { Mutation = claimRequest.Mutation with { State = claimRequest.Mutation.State with {
            Claims = claimRequest.Mutation.State.Claims.Select(item => item with { ExpiresAtUtc = clock.Now.AddSeconds(20) }).ToArray()
        } } };
        state = (await owner.CommitAsync(claimRequest)).State;
        ProcessDecisionChildAddress address = new(state.RootRunId, state.RunId, Step(state),
            enrolled.Decision!.Current.Reference, enrolled.Decision.Child!.ChildId);
        var child = (await services.GetRequiredService<IProcessDecisionChildReader>().ReadAsync(address))!.Admission;
        var parent = new ProcessDecisionChildMaterialBinding(child, material.Scope, material.Stamp, ProjectTaskPlanProcessContract.Execution);
        var claim = state.Claims.Single(item => item.ClaimToken == state.Steps.Single().ActiveClaimToken);
        ProcessExecutionClaimEvidence evidence = new(address.ChildId.Value, executorId, state.RunId, Step(state),
            claim.ClaimToken.Value, assignment.StepKey, clock.Now, true);
        var observed = await services.GetRequiredService<IProcessDecisionDispatchAuthorityQuery>().ReadDecisionDispatchAsync(
            evidence, parent, ProjectTaskPlanProcessContract.Execution, ProjectTaskPlanProcessContract.Content);
        var source = Assert.IsType<ProcessExecutionDispatchAuthority>(observed.Snapshot);
        Assert.True(source.ObservedCurrentDispatch);
        return new(state, parent, material, policy, source);
    }
}
