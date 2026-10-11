using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessExistingLaunchReconciliationRequiredException(ProcessRunId runId, string reason)
    : InvalidOperationException($"Retained process run '{runId}' requires origin reconciliation: {reason} Inspect the retained run and use its existing recovery or rework controls; automatic replacement is refused.") {
    public ProcessRunId RunId { get; } = runId;
}

internal sealed record ProcessRetainedLaunch(ProcessAuthoringAddress Origin, ProcessLaunchPlanView? Review);

internal sealed class ProcessRetainedLaunchRecovery(IProcessPreparedLaunchStore? preparations, ProcessExecutableDefinitionResolver? definitions) {
    public static IReadOnlyDictionary<string, string> LookupVariables(ProcessExistingLaunchLookupRequest request) {
        var variables = request.RequiredLaunchVariables.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        RemoveSelection(ProcessRuntimeLaunchVariables.SubprocessDefinitionKey, request.DefinitionKey);
        RemoveSelection(ProcessRuntimeLaunchVariables.SubprocessLiveRunProfileKey, request.LiveRunProfileKey);
        return variables.Count > 0 ? variables : throw new ArgumentException("Existing launch lookup requires a stable source identity in addition to a definition or profile.", nameof(request));

        void RemoveSelection(string key, string? expected) {
            if (variables.Remove(key, out var value) && !string.Equals(value.Trim(), expected?.Trim(), StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException("The existing launch selection contradicts its required launch variables.", nameof(request));
            }
        }
    }

    public static bool MatchesProfile(string? requested, ProcessRetainedLaunch retained, ProcessRunId runId,
        IReadOnlyList<ProcessRuntimeStepAssignment> assignments) {
        if (string.IsNullOrWhiteSpace(requested)) {
            return true;
        }
        if (retained.Review is { } review) {
            return string.Equals(requested.Trim(), review.LiveRunProfileKey, StringComparison.OrdinalIgnoreCase);
        }
        var profiles = assignments.Select(item => item.LaunchVariables.GetValueOrDefault(ProcessRuntimeLaunchVariables.SubprocessLiveRunProfileKey))
            .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (profiles.Length != 1) {
            throw new ProcessExistingLaunchReconciliationRequiredException(runId, "The requested live-run profile is not unambiguously recorded in the retained preparation or subprocess identity.");
        }
        return string.Equals(requested.Trim(), profiles[0]!.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static string DefinitionKey(ProcessInstancePlan plan) {
        var key = plan.ExecutableDefinitions?.DefinitionKey;
        if (key is null) {
            var keys = plan.Definition.TemplateComponents.Where(component => component.ComponentId.Value == plan.Definition.DefinitionId.Value)
                .Select(component => component.Key).Distinct(StringComparer.Ordinal).ToArray();
            key = keys.Length == 1 ? keys[0] : null;
        }
        return key is not null && ProcessTemplateKernelBuilder.CreateDefinitionId(key) == plan.Definition.DefinitionId ? key
            : throw new InvalidOperationException("The retained process plan has no unambiguous canonical definition identity.");
    }

    public async Task<ProcessRetainedLaunch?> ReadAsync(Guid? projectId, ProcessRuntimeStateSnapshot state, ProcessInstancePlan plan,
        CancellationToken cancellationToken) {
        if (state.PlanId != plan.Header.PlanId || state.PlanHash != plan.PlanHash) {
            throw Refuse("The runtime state and immutable plan identities disagree.");
        }
        var key = DefinitionKey(plan);
        ProcessAuthoringAddress? origin = null;
        ProcessLaunchPlanView? review = null;
        if (plan.ExecutableDefinitions is { } captured) {
            Add(new(captured.DatabaseProfileId, captured.ProjectId, captured.ProjectLifetimeId, key));
        }
        if (state.ProjectAdmission is { } admission) {
            Add(new(admission.DatabaseProfileId, admission.ProjectId, admission.LifetimeId, key));
        }
        var saved = preparations is null ? null : state.LaunchAdmissionId is { } id
            ? await preparations.GetAsync(id, cancellationToken)
            : await preparations.FindByRunAsync(state.RunId, cancellationToken);
        if (state.LaunchAdmissionId is not null && saved is null) {
            throw Refuse("Its referenced preparation is unavailable.");
        }
        if (saved is not null) {
            var prepared = saved.Preparation;
            var initial = prepared.InitialCommit.Mutation.State;
            if (saved.AcceptedAtUtc is null || initial.RunId != state.RunId || initial.RootRunId != state.RootRunId || initial.PlanId != state.PlanId || initial.PlanHash != plan.PlanHash ||
                    prepared.InitialCommit.InitialPlan?.Header.PlanId != plan.Header.PlanId || prepared.InitialCommit.InitialPlan.PlanHash != plan.PlanHash ||
                    state.LaunchAdmissionId is { } expected && prepared.AdmissionId != expected ||
                    prepared.Review.PlanId != plan.Header.PlanId || prepared.Review.PlanHash != plan.PlanHash || prepared.Review.DefinitionId != plan.Definition.DefinitionId ||
                    prepared.Review.DefinitionVersionId != plan.Definition.VersionId || prepared.Review.DefinitionKey != key) {
                throw Refuse("Its retained accepted preparation does not describe this run and plan.");
            }
            if (initial.ProjectAdmission is { } initialAdmission) {
                Add(new(initialAdmission.DatabaseProfileId, initialAdmission.ProjectId, initialAdmission.LifetimeId, key));
            }
            if (prepared.Authority is { } authority) {
                try {
                    authority.Validate();
                } catch (Exception error) when (error is ArgumentException or InvalidOperationException) {
                    throw Refuse("Its retained preparation has malformed authority.");
                }
                var admitted = authority.ProjectAdmission;
                if (prepared.Request.ProjectId != admitted?.ProjectId) {
                    throw Refuse("Its retained preparation and authority disagree about the project.");
                }
                Add(new(authority.DatabaseProfileId, admitted?.ProjectId ?? Guid.Empty, admitted?.LifetimeId ?? Guid.Empty, key));
            }
            review = prepared.Review;
        }
        if (origin is null) {
            throw Refuse("No retained project admission, captured definition origin or accepted preparation proves its database profile and project lifetime. Public project identifiers and launch variables are insufficient.");
        }
        if (definitions is null) {
            throw Refuse("The current native workspace origin cannot be checked.");
        }
        return await definitions.MatchesRetainedOriginAsync(projectId, origin, cancellationToken) ? new(origin, review) : null;

        void Add(ProcessAuthoringAddress evidence) {
            try {
                evidence.Validate();
            } catch (ArgumentException) {
                throw Refuse("Its retained origin has an invalid database profile or incomplete project lifetime.");
            }
            if (origin is not null && origin != evidence) {
                throw Refuse("Its retained authority records disagree about the database profile or project lifetime.");
            }
            origin = evidence;
        }
        ProcessExistingLaunchReconciliationRequiredException Refuse(string reason) => new(state.RunId, reason);
    }

    public static ProcessLaunchPlanView LegacyReview(ProcessInstancePlan plan, IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
        IReadOnlyList<ProcessLaunchReadinessFinding> findings) {
        var key = DefinitionKey(plan);
        var steps = plan.Steps.Select(step => {
            var assignment = assignments.SingleOrDefault(item => item.StepInstanceId == step.StepInstanceId);
            return new ProcessLaunchStepView(step.StepInstanceId, step.StepKey, step.StepKey, assignment?.RoleKey ?? string.Empty,
                assignment?.ExecutorKind ?? string.Empty, assignment?.ExecutorId ?? string.Empty, assignment?.ExecutorDisplayName ?? string.Empty,
                false, null, assignment?.BranchGate) {
                WorkflowBinding = assignment?.WorkflowBinding,
                AllowedOperations = assignment?.AllowedOperations ?? [],
                OperationTargetScope = assignment?.OperationTargetScope ?? string.Empty,
                RoleResourceKey = assignment?.RoleResourceKey ?? string.Empty,
                RoleDisplayName = assignment?.RoleDisplayName ?? string.Empty,
                CapabilityScope = assignment?.CapabilityScope ?? Contracts.ProcessCapabilityScope.Empty,
                RequiredRuntimeToolNames = step.RequiredRuntimeToolNames
            };
        }).ToArray();
        return new(plan.Header.PlanId, plan.Definition.DefinitionId, plan.Definition.VersionId, key, key,
            "Retained predecessor plan and assignments; original display labels were not stored.", null, plan.PlanHash, steps, findings);
    }
}
