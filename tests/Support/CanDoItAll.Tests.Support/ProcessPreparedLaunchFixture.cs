using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Support;

public static class ProcessPreparedLaunchFixture {
    public static ProcessPreparedLaunch Create(ProcessLaunchAuthority? authority = null, ProcessLaunchIntentId? callerIntent = null,
        ProcessLaunchLinkTarget? target = null) {
        var initial = ProcessProjectAdmissionFixture.Initial(authority?.ProjectAdmission);
        var admissionId = new ProcessLaunchAdmissionId(Guid.NewGuid());
        var state = initial.Mutation.State with { LaunchAdmissionId = admissionId };
        initial = initial with { OriginalState = state, Mutation = initial.Mutation with { State = state } };
        var plan = initial.InitialPlan!;
        var assignments = initial.InitialAssignments!;
        var request = new ProcessLaunchRequest("process-admission-fixture", null, null, authority?.ProjectAdmission?.ProjectId,
            target?.SourceNodeKey, "process-admission-fixture", new Dictionary<string, string> { ["Topic"] = "Fixture review" }, false, false) {
            ProjectAdmission = authority?.ProjectAdmission,
            Authority = authority,
            CallerIntentId = callerIntent,
            LinkTarget = target
        };
        var review = new ProcessLaunchPlanView(plan.Header.PlanId, plan.Definition.DefinitionId, plan.Definition.VersionId,
            request.DefinitionKey!, "Process admission fixture", "A retained reviewed launch.", null, plan.PlanHash,
            assignments.Select(item => new ProcessLaunchStepView(item.StepInstanceId, item.StepKey, "Execute", item.RoleKey,
                item.ExecutorKind, item.ExecutorId, item.ExecutorDisplayName, false, null, null)).ToArray(), []);
        return new(admissionId, callerIntent, ProcessLaunchIntentFingerprint.Compute(request), authority, request, initial, review,
            target, ProcessProjectAdmissionFixture.Now);
    }

    public static ProcessRuntimeCommitRequest Commit(ProcessPreparedLaunchSnapshot saved, bool execute = false)
        => saved.Preparation.InitialCommit with {
            InitialLaunchAdmission = new(saved.Preparation.AdmissionId, saved.PreparationFingerprint, execute) {
                CurrentCallerAuthority = saved.Preparation.Authority
            }
        };

    public static ProcessLaunchAuthority Local(Guid profileId, ProcessProjectAdmission? project = null)
        => new(new ProcessLaunchPrincipal.LocalOperator(ProcessLaunchOperatorSurface.UserInterface),
            profileId, project, true, true, "process-admission-fixture");
}
