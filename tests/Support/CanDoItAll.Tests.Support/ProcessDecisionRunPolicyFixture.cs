using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Support;

public static class ProcessDecisionRunPolicyFixture {
    public static ProcessDecisionRunAdmission Root(ProcessRunId run, ProcessDefinitionVersionId definition, Guid profile,
        ReadBasisMode read = ReadBasisMode.StepSnapshot, OnBasisChange change = OnBasisChange.PauseForReview) {
        var replans = change == OnBasisChange.Replan ? 2 : 0;
        DecisionDataPolicy workspace = new(read, PublicationFreshness.RequireCurrent, change,
            new(replans, TimeSpan.FromTicks(1000), TimeSpan.FromHours(1)));
        DecisionDataPolicyCapabilities capabilities = new(true, true, false, true, true, true,
            PublicationFreshness.RequireCurrent, new(3, TimeSpan.FromTicks(2000), TimeSpan.FromHours(2)));
        DecisionDataPolicyOverride definitionOverride = new(MaximumWorkDuration: TimeSpan.FromTicks(900));
        DecisionDataPolicyOverride invocation = new(MaximumWorkDuration: TimeSpan.FromTicks(800));
        DecisionDataPolicyBudget enclosing = new(2, TimeSpan.FromTicks(500), TimeSpan.FromMinutes(30));
        var policy = DecisionDataPolicyResolver.Resolve(workspace, capabilities, definition: definitionOverride,
            authorizedInvocation: invocation, enclosingBudgets: [enclosing]);
        return new(run, new(Guid.NewGuid()), profile, Guid.Parse("de502d5c-5370-4bcf-9a26-870bd7505eb8"),
            Guid.Parse("295a060e-51ef-4264-99cb-41a60465d5c6"), definition, workspace, definitionOverride, invocation,
            capabilities, [enclosing], policy);
    }

    public static ProcessDecisionDataPolicyAdmission Child(ProcessDecisionRunAdmission root, ProcessStepInstanceId step,
        ProcessDefinitionVersionId? definition = null, ProcessDecisionChildOwner executor = ProcessDecisionChildOwner.Agent) {
        DecisionDataPolicyBudget[] enclosing = [.. root.EnclosingBudgets, root.Policy.Budget];
        var policy = DecisionDataPolicyResolver.Resolve(root.WorkspacePolicy, root.OwnerCapabilities, definition: root.DefinitionOverride,
            authorizedInvocation: root.AuthorizedInvocationOverride, enclosingBudgets: enclosing);
        return new(root.WorkspaceNamespaceId, root.WorkspaceHeadRevision, definition ?? root.DefinitionVersionId, step,
            root.WorkspacePolicy, root.DefinitionOverride, null, root.AuthorizedInvocationOverride, root.OwnerCapabilities, [.. enclosing], policy) {
            ExecutionContract = ProcessDecisionPolicyFixture.Contract(executor), MaterialProtocol = ProcessDecisionMaterialProtocol.SharedContentV1,
            ContentContract = ProcessDecisionPolicyFixture.ContentContract(), Funding = root.Funding
        };
    }
}
