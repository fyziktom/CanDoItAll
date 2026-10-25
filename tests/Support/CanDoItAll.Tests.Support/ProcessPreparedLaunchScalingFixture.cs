using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Support;

public static class ProcessPreparedLaunchScalingFixture {
    public static ProcessPreparedLaunch Create(int count, int promptCharacters, ProcessLaunchAuthority? authority = null) {
        var original = ProcessPreparedLaunchFixture.Create(authority);
        var initial = original.InitialCommit;
        var plan = initial.InitialPlan!;
        var firstStep = plan.Steps[0];
        var firstState = initial.OriginalState.Steps[0];
        var firstAssignment = initial.InitialAssignments![0];
        var firstReview = original.Review.Steps[0];
        var policy = DecisionDataPolicy.PlanningDefault(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10));
        var capabilities = new DecisionDataPolicyCapabilities(true, true, false, true, true, true, PublicationFreshness.RequireCurrent, policy.Budget);
        var workspaceNamespace = Guid.NewGuid();
        var workspaceRevision = Guid.NewGuid();
        ProcessDecisionExecutionContract contract = new(Guid.NewGuid(), 1, ProcessDecisionChildOwner.Agent);
        var planSteps = new StepInstancePlan[count];
        var stateSteps = new ProcessRuntimeStepState[count];
        var assignments = new ProcessRuntimeStepAssignment[count];
        var reviews = new ProcessLaunchStepView[count];
        for (var index = 0; index < count; index++) {
            var stepId = ProcessStepInstanceId.New();
            var definitionId = ProcessStepDefinitionId.New();
            var key = $"measure-{index}";
            planSteps[index] = firstStep with { StepInstanceId = stepId, StepDefinitionId = definitionId, StepKey = key };
            stateSteps[index] = firstState with { StepInstanceId = stepId, StepDefinitionId = definitionId };
            assignments[index] = firstAssignment with {
                StepInstanceId = stepId, StepKey = key, Prompt = new string('x', promptCharacters),
                DecisionDataPolicy = new(workspaceNamespace, workspaceRevision, plan.Definition.VersionId, stepId,
                    policy, null, null, null, capabilities, [], policy) {
                    ExecutionContract = contract, MaterialProtocol = ProcessDecisionMaterialProtocol.CanonicalSnapshotV1
                }
            };
            reviews[index] = firstReview with { StepInstanceId = stepId, StepKey = key };
        }
        plan = plan with { Steps = planSteps };
        plan = plan with { PlanHash = ProcessPlanHasher.Compute(plan) };
        var state = initial.OriginalState with { Steps = stateSteps, PlanHash = plan.PlanHash };
        return original with {
            InitialCommit = initial with {
                OriginalState = state, Mutation = initial.Mutation with { State = state }, InitialPlan = plan, InitialAssignments = assignments
            },
            Review = original.Review with { PlanHash = plan.PlanHash, Steps = reviews }
        };
    }
}
