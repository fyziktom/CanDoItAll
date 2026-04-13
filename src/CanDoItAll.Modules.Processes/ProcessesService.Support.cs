using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private static Error? ValidateDefinitionEditor(ProcessDefinitionEditorModel model) {
        if (string.IsNullOrWhiteSpace(model.Name)) {
            return Error.Validation("Process name is required.", "processes.name-required");
        }

        if (string.IsNullOrWhiteSpace(model.ValueStatement)) {
            return Error.Validation("Value statement is required.", "processes.value-statement-required");
        }

        if (string.IsNullOrWhiteSpace(model.OwnerName)) {
            return Error.Validation("Owner name is required.", "processes.owner-required");
        }

        if (model.Roles.Count == 0) {
            return Error.Validation("At least one role is required.", "processes.role-required");
        }

        if (model.Steps.Count == 0) {
            return Error.Validation("At least one step is required.", "processes.step-required");
        }

        if (model.Steps.Any(step => string.IsNullOrWhiteSpace(step.Title))) {
            return Error.Validation("Every step requires a title.", "processes.step-title-required");
        }

        var stepsById = model.Steps
            .Where(step => step.Id.HasValue)
            .ToDictionary(step => step.Id!.Value);
        foreach (var step in model.Steps) {
            if (step.BranchOutcomes.Any(outcome => string.IsNullOrWhiteSpace(outcome.Title))) {
                return Error.Validation("Every branch outcome requires a title.", "processes.branch-outcome-title-required");
            }

            if (step.BranchOutcomes.Count > 0 && !step.DecisionRoleRequirementId.HasValue) {
                return Error.Validation("Branching steps require an explicit decision-maker role.", "processes.branch-decision-role-required");
            }

            if (step.DecisionRoleRequirementId.HasValue &&
                model.Roles.All(role => role.Id != step.DecisionRoleRequirementId.Value)) {
                return Error.Validation("Decision-maker role must reference a process role in the same definition.", "processes.branch-decision-role-invalid");
            }

            foreach (var dependency in ProcessCanvasBranching.GetOrderedDependencies(step)) {
                if (!dependency.DependsOnStepId.HasValue) {
                    return Error.Validation("Every dependency must resolve to an upstream step.", "processes.branch-dependency-step-required");
                }

                if (!stepsById.TryGetValue(dependency.DependsOnStepId.Value, out var dependencyStep)) {
                    return Error.Validation("Dependencies must reference a step in the same definition.", "processes.branch-dependency-step-invalid");
                }

                if (!dependency.DependsOnBranchOutcomeId.HasValue) {
                    continue;
                }

                if (dependencyStep.BranchOutcomes.All(outcome => outcome.Id != dependency.DependsOnBranchOutcomeId.Value)) {
                    return Error.Validation("Dependency outcome must belong to the selected dependency step.", "processes.branch-dependency-outcome-invalid");
                }
            }
        }

        return ValidateArtifactInputs(model);
    }

    private static void NormalizeDefinitionEditorForSave(ProcessDefinitionEditorModel model) {
        ProcessCanvasBranching.NormalizeDefinitionEditor(model);
    }

    private static bool HasConcurrencyTokenMismatch(Guid? expectedToken, Guid actualToken) {
        return expectedToken.HasValue && expectedToken.Value != actualToken;
    }

    private static Error CreateDefinitionSaveConflictError() {
        return Error.Validation(
            "Process definition changed before the save completed. Reload the definition and try again.",
            "processes.definition-concurrency-conflict");
    }

    private static Error CreateDefinitionSaveUniqueConflictError() {
        return Error.Validation(
            "Process definition could not be saved because a conflicting definition update already claimed the required unique values. Reload and try again.",
            "processes.definition-unique-conflict");
    }

    private static Error CreateDefinitionPublishConflictError() {
        return Error.Validation(
            "Process definition changed before publish completed. Reload the definition and try again.",
            "processes.publish-concurrency-conflict");
    }

    private static Error CreateDefinitionPublishUniqueConflictError() {
        return Error.Validation(
            "Publish could not complete because another definition update already created conflicting version data. Reload and try again.",
            "processes.publish-unique-conflict");
    }

    private static Error CreateRunStartConflictError() {
        return Error.Validation(
            "Process run creation conflicted with another update. Reload the process and try again.",
            "processes.run-start-conflict");
    }

    private static Error CreateStepTransitionConflictError() {
        return Error.Validation(
            "Process step changed before the transition completed. Reload the run and try again.",
            "processes.step-transition-conflict");
    }

    private static Error? ValidatePublish(
        ProcessDefinition definition,
        ProcessDefinitionVersion version,
        IReadOnlyList<ProcessRoleRequirement> roles,
        IReadOnlyList<ProcessStepDefinition> steps,
        IReadOnlyList<ProcessStepRoleAssignmentRequirement> stepRoleRequirements,
        IReadOnlyList<ProcessStepBranchOutcomeDefinition> branchOutcomes,
        IReadOnlyList<ProcessStepDependencyDefinition> stepDependencies,
        IReadOnlyList<ProcessArtifactExpectation> artifactExpectations,
        IReadOnlyList<ProcessStepArtifactInputDefinition> artifactInputs) {
        if (string.IsNullOrWhiteSpace(definition.OwnerName) ||
            string.IsNullOrWhiteSpace(definition.CustomerName) ||
            string.IsNullOrWhiteSpace(definition.ValueStatement) ||
            string.IsNullOrWhiteSpace(version.GovernancePolicySummary)) {
            return Error.Validation(
                "Publishing requires owner, customer, value statement, and governance policy summary.",
                "processes.publish-governance-required");
        }

        if (roles.Count == 0 || steps.Count == 0) {
            return Error.Validation("Publishing requires at least one role and one step.", "processes.publish-shape-required");
        }

        if (steps.Any(step => !stepRoleRequirements.Any(requirement => requirement.StepDefinitionId == step.Id))) {
            return Error.Validation("Every step must have at least one explicit role requirement before publication.", "processes.publish-step-role-required");
        }

        var branchOutcomesByStepId = branchOutcomes
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.DisplayOrder).ToList());
        var stepDependenciesByStepId = stepDependencies
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.DisplayOrder).ToList());

        var branchingError = ValidatePublishBranching(definition, roles, steps, branchOutcomesByStepId, stepDependenciesByStepId);
        if (branchingError is not null) {
            return branchingError;
        }

        return ValidatePublishedArtifactInputs(steps, artifactExpectations, artifactInputs, stepDependenciesByStepId);
    }

    private static Error? ValidatePublishBranching(
        ProcessDefinition definition,
        IReadOnlyList<ProcessRoleRequirement> roles,
        IReadOnlyList<ProcessStepDefinition> steps,
        IReadOnlyDictionary<Guid, List<ProcessStepBranchOutcomeDefinition>> branchOutcomesByStepId,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> stepDependenciesByStepId) {
        var stepsById = steps.ToDictionary(step => step.Id);

        foreach (var step in steps) {
            branchOutcomesByStepId.TryGetValue(step.Id, out var stepBranchOutcomes);
            stepBranchOutcomes ??= [];

            if (stepBranchOutcomes.Count > 0 && !step.DecisionRoleRequirementId.HasValue) {
                return Error.Validation("Publishing requires a decision-maker role for branching steps.", "processes.publish-branch-decision-role-required");
            }

            if (step.DecisionRoleRequirementId.HasValue && roles.All(role => role.Id != step.DecisionRoleRequirementId.Value)) {
                return Error.Validation("Branch decision-maker roles must resolve to a published process role.", "processes.publish-branch-decision-role-invalid");
            }

            foreach (var dependency in GetPersistedDependencies(step, stepDependenciesByStepId)) {
                if (!stepsById.ContainsKey(dependency.DependsOnStepId)) {
                    return Error.Validation("Publishing requires each dependency to resolve to a published upstream step.", "processes.publish-branch-dependency-step-required");
                }

                if (!dependency.DependsOnBranchOutcomeId.HasValue) {
                    continue;
                }

                branchOutcomesByStepId.TryGetValue(dependency.DependsOnStepId, out var dependencyOutcomes);
                dependencyOutcomes ??= [];

                if (dependencyOutcomes.All(outcome => outcome.Id != dependency.DependsOnBranchOutcomeId.Value)) {
                    return Error.Validation("Dependency outcomes must belong to the selected dependency step before publication.", "processes.publish-branch-dependency-outcome-invalid");
                }
            }
        }

        foreach (var step in steps) {
            if (!branchOutcomesByStepId.TryGetValue(step.Id, out var stepBranchOutcomes) || stepBranchOutcomes.Count == 0) {
                continue;
            }

            foreach (var branchOutcome in stepBranchOutcomes) {
                if (ProcessCanvasBranching.IsSystemOutcome(branchOutcome)) {
                    continue;
                }

                var hasDependent = steps.Any(candidate =>
                    GetPersistedDependencies(candidate, stepDependenciesByStepId)
                        .Any(dependency => dependency.DependsOnStepId == step.Id &&
                            dependency.DependsOnBranchOutcomeId == branchOutcome.Id));
                if (!hasDependent) {
                    return Error.Validation(
                        $"Branch outcome '{branchOutcome.Title}' on process '{definition.Name}' is not routed to any downstream step.",
                        "processes.publish-branch-outcome-unused");
                }
            }
        }

        return null;
    }

    private static List<ProcessStepDependencyEditorModel> BuildEditorDependencies(
        ProcessStepDefinition step,
        IReadOnlyList<ProcessStepDependencyDefinition> allDependencies) {
        return ProcessDependencyCompatibilityBridge.BuildEditorDependencies(step, allDependencies);
    }

    private static IReadOnlyList<ProcessStepDependencyDefinition> GetPersistedDependencies(
        ProcessStepDefinition step,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> dependenciesByStepId) {
        return ProcessDependencyCompatibilityBridge.GetCanonicalPersistedDependencies(step, dependenciesByStepId);
    }

    private static string BuildWorkBrief(ProcessDefinition definition, ProcessStepDefinition step, string? executorName) {
        return $"{definition.Name}: {step.Title}{Environment.NewLine}" +
            $"Customer value: {definition.ValueStatement}{Environment.NewLine}" +
            $"Owner: {definition.OwnerName}{Environment.NewLine}" +
            $"Executor: {(string.IsNullOrWhiteSpace(executorName) ? "Unassigned" : executorName)}{Environment.NewLine}" +
            $"Inputs: {step.InputContractSummary}{Environment.NewLine}" +
            $"Outputs: {step.OutputContractSummary}{Environment.NewLine}" +
            $"Evidence: {step.EvidenceContractSummary}";
    }

    private static string BuildRunRoute(ProcessRun run) {
        return run.ProjectId.HasValue
            ? $"/projects/{run.ProjectId.Value:D}/processes?runId={run.Id:D}"
            : $"/processes?runId={run.Id:D}";
    }

    private ProcessJournalEntry BuildJournalEntry(
        Guid runId,
        Guid? stepRunId,
        string eventType,
        string title,
        string description,
        ProcessOperatingMode operatingMode,
        string policyVersion,
        string replaySummary) {
        return new ProcessJournalEntry {
            ProcessRunId = runId,
            StepRunId = stepRunId,
            EventType = eventType,
            Title = title,
            Description = description,
            CorrelationId = Guid.NewGuid().ToString("N"),
            OperatingMode = operatingMode,
            PolicyVersion = policyVersion,
            EnvironmentMode = operatingMode.ToString(),
            ReplayContextJson = JsonSerializer.Serialize(new {
                RunId = runId,
                StepRunId = stepRunId,
                Summary = replaySummary
            }),
            OccurredAtUtc = clock.GetUtcNow()
        };
    }

    private async Task MaybeCreateImprovementCandidateAsync(
        AppDbContext dbContext,
        ProcessRun run,
        ProcessStepRun stepRun,
        ProcessStepTransitionRequest request,
        CancellationToken cancellationToken) {
        await dbContext.Set<ProcessImprovementCandidate>().AddAsync(
            new ProcessImprovementCandidate {
                ProcessDefinitionId = run.ProcessDefinitionId,
                ProcessRunId = run.Id,
                Title = request.TargetStatus switch {
                    ProcessStepRunStatus.Refused => $"Review refusal path in {stepRun.Title}",
                    ProcessStepRunStatus.Blocked => $"Reduce blocking in {stepRun.Title}",
                    ProcessStepRunStatus.Failed => $"Stabilize failure-prone step {stepRun.Title}",
                    _ => $"Improve {stepRun.Title}"
                },
                Category = request.TargetStatus.ToString(),
                ProblemSummary = request.Reason.Trim(),
                EvidenceSummary = $"{stepRun.Title} / {request.TargetStatus}",
                Status = ProcessImprovementStatus.Open,
                IsTrainingOpportunity = request.TargetStatus == ProcessStepRunStatus.Refused,
                RequiresGovernanceReview = true,
                CreatedAtUtc = clock.GetUtcNow()
            },
            cancellationToken);
    }

}
