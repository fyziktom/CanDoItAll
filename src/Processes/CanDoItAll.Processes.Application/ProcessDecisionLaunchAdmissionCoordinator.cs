using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessDecisionLaunchAdmissionCoordinator {
    private readonly IReadOnlyDictionary<ProcessDecisionExecutionContract, IProcessDecisionLaunchPolicyProvider> providers;

    public ProcessDecisionLaunchAdmissionCoordinator(IEnumerable<IProcessDecisionLaunchPolicyProvider> providers) {
        this.providers = providers.ToDictionary(provider => provider.Contract);
        foreach (var contract in this.providers.Keys) {
            contract.Validate();
        }
    }

    public static void RequireRequest(ProcessLaunchRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Decision is not { } selection) {
            return;
        }
        selection.Validate();
        if (request.CallerIntentId is not { Value: var intent } || intent == Guid.Empty || request.ToolSource is not null ||
                request.RootRunIdOverride is not null || ProcessRuntimeLaunchVariables.TryReadParentStep(request.Variables, out _) ||
                request.Authority is not { CanCreateTasks: true, ProjectAdmission: { } project } authority ||
                authority.Principal is not (ProcessLaunchPrincipal.LocalOperator or ProcessLaunchPrincipal.AuthenticatedOperator) ||
                request.ProjectId != project.ProjectId || request.ProjectAdmission != project) {
            throw new ProcessLaunchAuthorityRejectedException("Decision planning requires an original operator launch intent and current Project task authority.");
        }
        authority.Validate();
    }

    public async Task<(ProcessRuntimeCommitRequest Initial, ProcessLaunchPlanView Review)> PrepareAsync(
        ProcessLaunchRequest request, ProcessRuntimeCommitRequest initial, ProcessLaunchPlanView review, CancellationToken cancellationToken) {
        RequireRequest(request);
        if (request.Decision is not { } selection) {
            return (initial, review);
        }
        if (!providers.TryGetValue(selection.Execution, out var provider)) {
            throw new ProcessLaunchDecisionRejectedException(ProcessLaunchDecisionRejection.UnsupportedContract);
        }
        if (initial.InitialAssignments is not { Count: > 0 } assignments || initial.InitialRunDecisionAdmission is not null ||
                initial.ParentStepPrecondition is not null || assignments.Any(item => item.DecisionDataPolicy is not null)) {
            throw new InvalidDataException("Decision preparation requires the original unfunded root and its assignments.");
        }
        var admissions = await provider.PrepareAsync(request, initial, cancellationToken).ConfigureAwait(false);
        if (admissions.Steps.Count != assignments.Count || assignments.Select(item => item.StepInstanceId).Distinct().Count() != assignments.Count ||
                assignments.Any(item => !admissions.Steps.ContainsKey(item.StepInstanceId))) {
            throw new InvalidDataException("Decision preparation must retain exactly the original assignment coverage.");
        }
        var governed = initial with {
            InitialRunDecisionAdmission = admissions.Root,
            InitialAssignments = assignments.Select(item => item with { DecisionDataPolicy = admissions.Steps[item.StepInstanceId] }).ToArray()
        };
        var effective = new ProcessLaunchDecisionReview(selection.Execution, admissions.Root.Policy);
        RequireBindings(selection, governed, effective);
        return (governed, review with { Decision = effective });
    }

    public static void RequireBindings(ProcessLaunchDecisionOptions selection, ProcessRuntimeCommitRequest initial, ProcessLaunchDecisionReview? review) {
        selection.Validate();
        if (initial.InitialPlan is not { } plan || initial.InitialAssignments is not { Count: > 0 } assignments ||
                initial.InitialRunDecisionAdmission is not { } root || initial.ParentStepPrecondition is not null ||
                root.Policy.ReadBasisMode == ReadBasisMode.RunSnapshot && root.MaterialScope is null ||
                root.AuthorizedInvocationOverride != selection.Policy || review != new ProcessLaunchDecisionReview(selection.Execution, root.Policy) ||
                !plan.Steps.Where(item => item.IsExecutable).Select(item => item.StepInstanceId).ToHashSet()
                    .SetEquals(assignments.Select(item => item.StepInstanceId)) ||
                assignments.Select(item => item.StepInstanceId).Distinct().Count() != assignments.Count) {
            throw new InvalidDataException("The selected Process decision policy must retain its original root, assignments and effective review.");
        }
        root.RequireRoot(initial.Mutation.State, plan.Definition.VersionId);
        foreach (var assignment in assignments) {
            var policy = assignment.DecisionDataPolicy ?? throw new InvalidDataException("Every selected decision assignment requires its original policy.");
            policy.RequireBinding(plan.Definition.VersionId, assignment.StepInstanceId);
            policy.RequireExecutionContract(selection.Execution);
            root.RequireChild(policy);
            if (assignment.RunId != root.RunId || assignment.PlanId != plan.Header.PlanId ||
                    policy.AuthorizedInvocationOverride != selection.Policy || policy.Policy != root.Policy) {
                throw new InvalidDataException("The Process assignment changed its original selected root policy or identity.");
            }
        }
    }
}
