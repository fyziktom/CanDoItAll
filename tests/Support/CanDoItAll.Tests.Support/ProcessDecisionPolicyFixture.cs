using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Support;

public static class ProcessDecisionPolicyFixture {
    public static ProcessDecisionDataPolicyAdmission Create(ProcessDefinitionVersionId definition, ProcessStepInstanceId step) {
        DecisionDataPolicy workspace = new(ReadBasisMode.StepSnapshot, PublicationFreshness.RequireCurrent,
            OnBasisChange.PauseForReview, new(0, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1)));
        DecisionDataPolicyCapabilities capabilities = new(true, true, false, true, true, true,
            PublicationFreshness.RequireCurrent, new(2, TimeSpan.FromMinutes(10), TimeSpan.FromHours(2)));
        DecisionDataPolicyOverride definitionOverride = new(OnBasisChange: OnBasisChange.Replan, MaximumReplans: 2);
        DecisionDataPolicyOverride stepOverride = new(ReadBasisMode.RunSnapshot);
        DecisionDataPolicyOverride invocation = new(MaximumWorkDuration: TimeSpan.FromMinutes(4));
        DecisionDataPolicyBudget parent = new(1, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(30));
        var policy = DecisionDataPolicyResolver.Resolve(workspace, capabilities, definition: definitionOverride,
            step: stepOverride, authorizedInvocation: invocation, enclosingBudgets: [parent]);
        return new(Guid.Parse("de502d5c-5370-4bcf-9a26-870bd7505eb8"), Guid.Parse("295a060e-51ef-4264-99cb-41a60465d5c6"),
            definition, step, workspace, definitionOverride, stepOverride, invocation, capabilities, [parent], policy);
    }
}
