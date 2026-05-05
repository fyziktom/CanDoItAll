using System.Text;
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

        var roleIds = model.Roles
            .Where(role => role.Id.HasValue && role.Id.Value != Guid.Empty)
            .Select(role => role.Id!.Value)
            .ToHashSet();
        var duplicateMessagingPolicy = model.MessagingPolicies
            .Where(item => item.SourceRoleRequirementId.HasValue && item.TargetRoleRequirementId.HasValue)
            .GroupBy(item => (item.SourceRoleRequirementId!.Value, item.TargetRoleRequirementId!.Value))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateMessagingPolicy is not null) {
            return Error.Validation("Messaging policy links must remain unique per source and target role pair.", "processes.messaging-policy-duplicate");
        }

        foreach (var messagingPolicy in model.MessagingPolicies) {
            if (!messagingPolicy.SourceRoleRequirementId.HasValue || !messagingPolicy.TargetRoleRequirementId.HasValue) {
                return Error.Validation("Messaging policy links must resolve both the source and target process role.", "processes.messaging-policy-role-required");
            }

            if (messagingPolicy.SourceRoleRequirementId == messagingPolicy.TargetRoleRequirementId) {
                return Error.Validation("Messaging policy links cannot target the same role as both source and destination.", "processes.messaging-policy-self-reference");
            }

            if (!roleIds.Contains(messagingPolicy.SourceRoleRequirementId.Value) ||
                !roleIds.Contains(messagingPolicy.TargetRoleRequirementId.Value)) {
                return Error.Validation("Messaging policy links must reference roles in the same process definition.", "processes.messaging-policy-role-invalid");
            }
        }

        var stepsById = model.Steps
            .Where(step => step.Id.HasValue)
            .ToDictionary(step => step.Id!.Value);
        foreach (var step in model.Steps) {
            if (step.StepKind == ProcessStepKind.Subprocess &&
                (!step.SubprocessDefinitionId.HasValue || step.SubprocessDefinitionId.Value == Guid.Empty)) {
                return Error.Validation(
                    $"Subprocess step '{step.Title}' must reference a process definition.",
                    "processes.subprocess-target-required");
            }

            if (step.StepKind == ProcessStepKind.Subprocess &&
                model.Id.HasValue &&
                step.SubprocessDefinitionId == model.Id.Value) {
                return Error.Validation(
                    $"Subprocess step '{step.Title}' cannot reference its own process definition.",
                    "processes.subprocess-self-reference");
            }

            if (step.BranchOutcomes.Any(outcome => string.IsNullOrWhiteSpace(outcome.Title))) {
                return Error.Validation("Every branch outcome requires a title.", "processes.branch-outcome-title-required");
            }

            if (step.RoleAssignments.Any(assignment => !assignment.RoleRequirementId.HasValue || assignment.RoleRequirementId.Value == Guid.Empty)) {
                return Error.Validation(
                    $"Step '{step.Title}' contains a role assignment that does not resolve to a process role.",
                    "processes.step-role-assignment-role-required");
            }

            if (step.RoleAssignments.Any(assignment => assignment.RoleRequirementId.HasValue && !roleIds.Contains(assignment.RoleRequirementId.Value))) {
                return Error.Validation(
                    $"Step '{step.Title}' references a role that is not part of the same process definition.",
                    "processes.step-role-assignment-role-invalid");
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

        var graphIssue = FindDefinitionGraphIssue(model);
        if (graphIssue is not null) {
            return CreateDefinitionGraphValidationError(graphIssue);
        }

        return ValidateArtifactInputs(model);
    }

    private static void NormalizeDefinitionEditorForSave(ProcessDefinitionEditorModel model) {
        ProcessCanvasBranching.NormalizeDefinitionEditor(model);
        model.ManagerAgentOverrideName = model.ManagerAgentOverrideName.Trim();
        if (!model.ManagerAgentOverrideId.HasValue || model.ManagerAgentOverrideId.Value == Guid.Empty) {
            model.ManagerAgentOverrideId = null;
            model.ManagerAgentOverrideName = string.Empty;
        }

        foreach (var step in model.Steps) {
            if (step.StepKind != ProcessStepKind.Subprocess) {
                step.SubprocessDefinitionId = null;
                step.SubprocessDefinitionSnapshotName = string.Empty;
                continue;
            }

            step.SubprocessDefinitionSnapshotName = step.SubprocessDefinitionSnapshotName.Trim();
        }
    }

    private async Task<Result> ResolveImportedSubprocessReferencesAsync(
        ProcessDefinitionEditorModel model,
        CancellationToken cancellationToken)
    {
        var unresolvedSteps = model.Steps
            .Where(step =>
                step.StepKind == ProcessStepKind.Subprocess &&
                (!step.SubprocessDefinitionId.HasValue || step.SubprocessDefinitionId.Value == Guid.Empty) &&
                !string.IsNullOrWhiteSpace(step.SubprocessDefinitionSnapshotName))
            .ToList();
        if (unresolvedSteps.Count == 0)
        {
            return Result.Success();
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = await dbContext.Set<ProcessDefinition>()
            .AsNoTracking()
            .Where(definition => definition.ProjectId == model.ProjectId || definition.ProjectId == null)
            .Select(definition => new
            {
                definition.Id,
                definition.ProjectId,
                definition.Name,
                definition.Slug,
                definition.ActivePublishedVersionId
            })
            .ToListAsync(cancellationToken);

        foreach (var step in unresolvedSteps)
        {
            var reference = step.SubprocessDefinitionSnapshotName.Trim();
            var matches = candidates
                .Where(definition =>
                    string.Equals(definition.Name, reference, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(definition.Slug, reference, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var preferredMatches = matches
                .Where(definition => model.ProjectId.HasValue && definition.ProjectId == model.ProjectId)
                .ToList();
            if (preferredMatches.Count == 0)
            {
                preferredMatches = matches
                    .Where(definition => definition.ProjectId == null)
                    .ToList();
            }

            if (preferredMatches.Count == 0)
            {
                return Result.Failure(Error.Validation(
                    $"Subprocess step '{step.Title}' references '{reference}', but no matching process definition exists in the current scope.",
                    "processes.subprocess-import-target-not-found"));
            }

            if (preferredMatches.Count > 1)
            {
                var exactSlugMatches = preferredMatches
                    .Where(definition => string.Equals(definition.Slug, BuildSlug(reference), StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (exactSlugMatches.Count == 1)
                {
                    preferredMatches = exactSlugMatches;
                }
            }

            if (preferredMatches.Count > 1)
            {
                var publishedMatches = preferredMatches
                    .Where(definition => definition.ActivePublishedVersionId.HasValue)
                    .ToList();
                if (publishedMatches.Count == 1)
                {
                    preferredMatches = publishedMatches;
                }
            }

            if (preferredMatches.Count > 1)
            {
                return Result.Failure(Error.Validation(
                    $"Subprocess step '{step.Title}' references '{reference}', but multiple process definitions match that reference.",
                    "processes.subprocess-import-target-ambiguous"));
            }

            var resolvedDefinition = preferredMatches[0];
            step.SubprocessDefinitionId = resolvedDefinition.Id;
            step.SubprocessDefinitionSnapshotName = resolvedDefinition.Name;
        }

        return Result.Success();
    }

    private static bool HasConcurrencyTokenMismatch(Guid? expectedToken, Guid actualToken) {
        return expectedToken.HasValue && expectedToken.Value != actualToken;
    }

    private static Error CreateDefinitionSaveConflictError() {
        return Error.Validation(
            "Process definition changed before the save completed. Reload the definition and try again.",
            "processes.definition-concurrency-conflict");
    }

    private static Error CreateDefinitionSaveUniqueConflictError(DbUpdateException? exception = null) {
        if (IsDependencyUniqueConflict(exception)) {
            return Error.Validation(
                "Process definition contains duplicate dependency routes. Remove the duplicate dependency and try again.",
                "processes.dependency-unique-conflict");
        }

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

    private static Error CreateRunStartGraphError(ProcessDependencyGraphIssue? issue = null) {
        if (issue is null) {
            return Error.Validation(
                "Process run cannot start because the published process graph has no legal root step. Publish a corrected definition and try again.",
                "processes.run-invalid-graph");
        }

        return issue.Kind switch {
            ProcessDependencyGraphIssueKind.SelfDependency => Error.Validation(
                $"Process run cannot start because published step '{issue.StepLabels[0]}' depends on itself. Publish a corrected definition and try again.",
                "processes.run-invalid-graph"),
            _ => Error.Validation(
                $"Process run cannot start because the published process graph contains a dependency cycle: {string.Join(" -> ", issue.StepLabels)}. Publish a corrected definition and try again.",
                "processes.run-invalid-graph")
        };
    }

    private static Error CreateAssignmentUniqueConflictError() {
        return Error.Validation(
            "Another assignment update already claimed this run role scope. Reload the run and try again.",
            "processes.assignment-unique-conflict");
    }

    private static Error CreateStepTransitionConflictError() {
        return Error.Validation(
            "Process step changed before the transition completed. Reload the run and try again.",
            "processes.step-transition-conflict");
    }

    private static bool IsDependencyUniqueConflict(DbUpdateException? exception) {
        if (exception is null || !DbUpdateExceptionClassifier.IsUniqueConstraintViolation(exception)) {
            return false;
        }

        var constraintName = DbUpdateExceptionClassifier.GetConstraintName(exception);
        if (string.Equals(constraintName, ProcessPersistenceConstraintNames.StepDependencyUnconditionalUniqueIndex, StringComparison.Ordinal) ||
            string.Equals(constraintName, ProcessPersistenceConstraintNames.StepDependencyConditionalUniqueIndex, StringComparison.Ordinal)) {
            return true;
        }

        var providerMessage = DbUpdateExceptionClassifier.GetProviderMessage(exception);
        if (providerMessage.Contains(ProcessPersistenceConstraintNames.StepDependencyUnconditionalUniqueIndex, StringComparison.OrdinalIgnoreCase) ||
            providerMessage.Contains(ProcessPersistenceConstraintNames.StepDependencyConditionalUniqueIndex, StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        return providerMessage.Contains("Processes_StepDependencies.StepDefinitionId", StringComparison.OrdinalIgnoreCase) &&
            providerMessage.Contains("Processes_StepDependencies.DependsOnStepId", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDefinitionSlugConflict(DbUpdateException? exception) {
        if (exception is null || !DbUpdateExceptionClassifier.IsUniqueConstraintViolation(exception)) {
            return false;
        }

        var constraintName = DbUpdateExceptionClassifier.GetConstraintName(exception);
        if (string.Equals(constraintName, ProcessPersistenceConstraintNames.DefinitionSlugUniqueIndex, StringComparison.Ordinal)) {
            return true;
        }

        var providerMessage = DbUpdateExceptionClassifier.GetProviderMessage(exception);
        return providerMessage.Contains(ProcessPersistenceConstraintNames.DefinitionSlugUniqueIndex, StringComparison.OrdinalIgnoreCase) ||
            providerMessage.Contains("Processes_Definitions.Slug", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRunAssignmentUniqueConflict(DbUpdateException? exception) {
        if (exception is null || !DbUpdateExceptionClassifier.IsUniqueConstraintViolation(exception)) {
            return false;
        }

        var constraintName = DbUpdateExceptionClassifier.GetConstraintName(exception);
        if (string.Equals(constraintName, ProcessPersistenceConstraintNames.RunAssignmentRunScopedUniqueIndex, StringComparison.Ordinal) ||
            string.Equals(constraintName, ProcessPersistenceConstraintNames.RunAssignmentStepScopedUniqueIndex, StringComparison.Ordinal)) {
            return true;
        }

        var providerMessage = DbUpdateExceptionClassifier.GetProviderMessage(exception);
        if (providerMessage.Contains(ProcessPersistenceConstraintNames.RunAssignmentRunScopedUniqueIndex, StringComparison.OrdinalIgnoreCase) ||
            providerMessage.Contains(ProcessPersistenceConstraintNames.RunAssignmentStepScopedUniqueIndex, StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        return providerMessage.Contains("Processes_RunAssignments.ProcessRunId", StringComparison.OrdinalIgnoreCase) &&
            providerMessage.Contains("Processes_RunAssignments.RoleRequirementId", StringComparison.OrdinalIgnoreCase);
    }

    private static Error? ValidatePublish(
        ProcessDefinition definition,
        ProcessDefinitionVersion version,
        IReadOnlyList<ProcessRoleRequirement> roles,
        IReadOnlyList<ProcessRoleMessagingPolicyDefinition> messagingPolicies,
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

        var publishMessagingPolicyError = ValidatePublishMessagingPolicies(roles, messagingPolicies);
        if (publishMessagingPolicyError is not null) {
            return publishMessagingPolicyError;
        }

        if (steps.Any(step => !stepRoleRequirements.Any(requirement => requirement.StepDefinitionId == step.Id))) {
            return Error.Validation("Every step must have at least one explicit role requirement before publication.", "processes.publish-step-role-required");
        }

        if (steps.Any(step => step.StepKind == ProcessStepKind.Subprocess &&
                              (!step.SubprocessDefinitionId.HasValue || step.SubprocessDefinitionId.Value == Guid.Empty))) {
            return Error.Validation(
                "Publishing requires every subprocess step to reference a process definition.",
                "processes.publish-subprocess-target-required");
        }

        if (steps.Any(step => step.StepKind == ProcessStepKind.Subprocess &&
                              step.SubprocessDefinitionId == definition.Id)) {
            return Error.Validation(
                "Publishing requires subprocess steps to reference a different process definition.",
                "processes.publish-subprocess-self-reference");
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

        var graphIssue = FindPublishedGraphIssue(steps, stepDependenciesByStepId);
        if (graphIssue is not null) {
            return CreatePublishGraphValidationError(graphIssue);
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
        var roleIds = roles.Select(role => role.Id).ToHashSet();
        var dependencyOutcomeIdsByStepId = branchOutcomesByStepId.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Select(outcome => outcome.Id).ToHashSet());
        var routedBranchOutcomeIdsByDependsOnStepId = stepDependenciesByStepId.Values
            .SelectMany(dependencies => dependencies)
            .Where(dependency => dependency.DependsOnBranchOutcomeId.HasValue)
            .GroupBy(dependency => dependency.DependsOnStepId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(dependency => dependency.DependsOnBranchOutcomeId!.Value)
                    .ToHashSet());

        foreach (var step in steps) {
            branchOutcomesByStepId.TryGetValue(step.Id, out var stepBranchOutcomes);
            stepBranchOutcomes ??= [];

            if (stepBranchOutcomes.Count > 0 && !step.DecisionRoleRequirementId.HasValue) {
                return Error.Validation("Publishing requires a decision-maker role for branching steps.", "processes.publish-branch-decision-role-required");
            }

            if (step.DecisionRoleRequirementId.HasValue && !roleIds.Contains(step.DecisionRoleRequirementId.Value)) {
                return Error.Validation("Branch decision-maker roles must resolve to a published process role.", "processes.publish-branch-decision-role-invalid");
            }

            foreach (var dependency in GetPersistedDependencies(step, stepDependenciesByStepId)) {
                if (!stepsById.ContainsKey(dependency.DependsOnStepId)) {
                    return Error.Validation("Publishing requires each dependency to resolve to a published upstream step.", "processes.publish-branch-dependency-step-required");
                }

                if (!dependency.DependsOnBranchOutcomeId.HasValue) {
                    continue;
                }

                if (!dependencyOutcomeIdsByStepId.TryGetValue(dependency.DependsOnStepId, out var dependencyOutcomeIds) ||
                    !dependencyOutcomeIds.Contains(dependency.DependsOnBranchOutcomeId.Value)) {
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

                if (!routedBranchOutcomeIdsByDependsOnStepId.TryGetValue(step.Id, out var routedBranchOutcomeIds) ||
                    !routedBranchOutcomeIds.Contains(branchOutcome.Id)) {
                    return Error.Validation(
                        $"Branch outcome '{branchOutcome.Title}' on process '{definition.Name}' is not routed to any downstream step.",
                        "processes.publish-branch-outcome-unused");
                }
            }
        }

        return null;
    }

    private static Error? ValidatePublishMessagingPolicies(
        IReadOnlyList<ProcessRoleRequirement> roles,
        IReadOnlyList<ProcessRoleMessagingPolicyDefinition> messagingPolicies) {
        if (messagingPolicies.Count == 0) {
            return null;
        }

        var roleIds = roles
            .Select(role => role.Id)
            .ToHashSet();
        var duplicatePolicy = messagingPolicies
            .GroupBy(item => (item.SourceRoleRequirementId, item.TargetRoleRequirementId))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicatePolicy is not null) {
            return Error.Validation("Publishing requires messaging policy links to remain unique per source and target role pair.", "processes.publish-messaging-policy-duplicate");
        }

        foreach (var messagingPolicy in messagingPolicies) {
            if (messagingPolicy.SourceRoleRequirementId == messagingPolicy.TargetRoleRequirementId) {
                return Error.Validation("Publishing requires messaging policy links to connect two distinct roles.", "processes.publish-messaging-policy-self-reference");
            }

            if (!roleIds.Contains(messagingPolicy.SourceRoleRequirementId) ||
                !roleIds.Contains(messagingPolicy.TargetRoleRequirementId)) {
                return Error.Validation("Publishing requires messaging policy links to reference published roles.", "processes.publish-messaging-policy-role-invalid");
            }
        }

        return null;
    }

    private static List<ProcessStepDependencyEditorModel> BuildEditorDependencies(
        ProcessStepDefinition step,
        IReadOnlyList<ProcessStepDependencyDefinition> allDependencies) {
        return ProcessStepDependencyCollection.BuildEditorDependencies(step.Id, allDependencies);
    }

    private static IReadOnlyList<ProcessStepDependencyDefinition> GetPersistedDependencies(
        ProcessStepDefinition step,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> dependenciesByStepId) {
        return ProcessStepDependencyCollection.GetPersistedDependencies(step.Id, dependenciesByStepId);
    }

    private static Error CreateDefinitionGraphValidationError(ProcessDependencyGraphIssue issue) {
        return issue.Kind switch {
            ProcessDependencyGraphIssueKind.SelfDependency => Error.Validation(
                $"Step '{issue.StepLabels[0]}' cannot depend on itself.",
                "processes.branch-dependency-self-reference"),
            _ => Error.Validation(
                $"Process graph contains a dependency cycle: {string.Join(" -> ", issue.StepLabels)}. Remove the cycle and try again.",
                "processes.branch-dependency-cycle-invalid")
        };
    }

    private static Error CreatePublishGraphValidationError(ProcessDependencyGraphIssue issue) {
        return issue.Kind switch {
            ProcessDependencyGraphIssueKind.SelfDependency => Error.Validation(
                $"Publishing requires every step to depend on an upstream step instead of itself. Step '{issue.StepLabels[0]}' is self-referential.",
                "processes.publish-branch-dependency-self-reference"),
            _ => Error.Validation(
                $"Publishing requires an acyclic dependency graph. Remove the cycle: {string.Join(" -> ", issue.StepLabels)}.",
                "processes.publish-branch-dependency-cycle-invalid")
        };
    }

    private static ProcessDependencyGraphIssue? FindDefinitionGraphIssue(ProcessDefinitionEditorModel model) {
        return FindDependencyGraphIssue(
            model.Steps,
            step => step.Id,
            ResolveDefinitionStepLabel,
            step => ProcessCanvasBranching.GetOrderedDependencies(step)
                .Where(dependency => dependency.DependsOnStepId.HasValue)
                .Select(dependency => dependency.DependsOnStepId!.Value));
    }

    private static ProcessDependencyGraphIssue? FindPublishedGraphIssue(
        IReadOnlyList<ProcessStepDefinition> steps,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> stepDependenciesByStepId) {
        return FindDependencyGraphIssue(
            steps,
            step => step.Id,
            step => ResolvePersistedStepLabel(step.Title, step.Key, step.Id),
            step => GetPersistedDependencies(step, stepDependenciesByStepId)
                .Select(dependency => dependency.DependsOnStepId));
    }

    private static ProcessDependencyGraphIssue? FindDependencyGraphIssue<TStep>(
        IReadOnlyList<TStep> steps,
        Func<TStep, Guid?> stepIdSelector,
        Func<TStep, string> stepLabelSelector,
        Func<TStep, IEnumerable<Guid>> dependencySelector) {
        var orderedSteps = steps
            .Where(step => stepIdSelector(step).HasValue && stepIdSelector(step)!.Value != Guid.Empty)
            .ToList();
        var stepsById = orderedSteps.ToDictionary(step => stepIdSelector(step)!.Value);

        foreach (var step in orderedSteps) {
            var stepId = stepIdSelector(step)!.Value;
            if (dependencySelector(step).Any(dependencyStepId => dependencyStepId == stepId)) {
                return new ProcessDependencyGraphIssue(
                    ProcessDependencyGraphIssueKind.SelfDependency,
                    [stepLabelSelector(step)]);
            }
        }

        var cycle = FindDependencyCycle(
            [.. orderedSteps.Select(step => stepIdSelector(step)!.Value)],
            stepId => dependencySelector(stepsById[stepId])
                .Where(stepsById.ContainsKey)
                .Distinct());
        if (cycle is null) {
            return null;
        }

        return new ProcessDependencyGraphIssue(
            ProcessDependencyGraphIssueKind.Cycle,
            [.. cycle.Select(stepId => stepLabelSelector(stepsById[stepId]))]);
    }

    private static List<Guid>? FindDependencyCycle(
        IReadOnlyList<Guid> orderedStepIds,
        Func<Guid, IEnumerable<Guid>> dependencySelector) {
        var stepOrder = orderedStepIds
            .Select((stepId, index) => new { stepId, index })
            .ToDictionary(item => item.stepId, item => item.index);
        var visitStateByStepId = orderedStepIds.ToDictionary(stepId => stepId, _ => 0);
        var traversalStack = new List<Guid>(orderedStepIds.Count);
        List<Guid>? cycle = null;

        bool Visit(Guid stepId) {
            visitStateByStepId[stepId] = 1;
            traversalStack.Add(stepId);

            foreach (var dependencyStepId in dependencySelector(stepId).OrderBy(candidate => stepOrder[candidate])) {
                var visitState = visitStateByStepId.GetValueOrDefault(dependencyStepId);
                if (visitState == 1) {
                    var cycleStartIndex = traversalStack.IndexOf(dependencyStepId);
                    cycle = traversalStack.Skip(cycleStartIndex).ToList();
                    cycle.Add(dependencyStepId);
                    return true;
                }

                if (visitState == 0 && Visit(dependencyStepId)) {
                    return true;
                }
            }

            traversalStack.RemoveAt(traversalStack.Count - 1);
            visitStateByStepId[stepId] = 2;
            return false;
        }

        foreach (var stepId in orderedStepIds) {
            if (visitStateByStepId[stepId] == 0 && Visit(stepId)) {
                break;
            }
        }

        return cycle;
    }

    private static string ResolveDefinitionStepLabel(ProcessStepEditorModel step) {
        return ResolvePersistedStepLabel(step.Title, step.Key, step.Id);
    }

    private static string ResolvePersistedStepLabel(string? title, string? key, Guid? stepId) {
        if (!string.IsNullOrWhiteSpace(title)) {
            return title.Trim();
        }

        if (!string.IsNullOrWhiteSpace(key)) {
            return key.Trim();
        }

        return stepId.HasValue && stepId.Value != Guid.Empty
            ? stepId.Value.ToString("D")
            : "Unnamed step";
    }

    private static string BuildWorkBrief(
        ProcessDefinition definition,
        ProcessStepDefinition step,
        string? executorName,
        ProcessProjectStructureContext? projectStructureContext = null) {
        var builder = new StringBuilder()
            .AppendLine($"{definition.Name}: {step.Title}")
            .AppendLine($"Customer value: {definition.ValueStatement}")
            .AppendLine($"Owner: {definition.OwnerName}")
            .AppendLine($"Executor: {(string.IsNullOrWhiteSpace(executorName) ? "Unassigned" : executorName)}")
            .AppendLine($"Inputs: {step.InputContractSummary}")
            .AppendLine($"Outputs: {step.OutputContractSummary}")
            .AppendLine($"Evidence: {step.EvidenceContractSummary}");

        if (!string.IsNullOrWhiteSpace(step.Notes)) {
            builder.AppendLine($"Instructions: {step.Notes}");
        }

        if (projectStructureContext is not null) {
            builder.AppendLine($"Project structure target: {projectStructureContext.ResolveTargetNodeTitle()} ({projectStructureContext.ResolveTargetNodeId()})");
            builder.AppendLine($"Project structure node: {projectStructureContext.NodeTitle} ({projectStructureContext.NodeId})");
            builder.AppendLine($"Project id: {projectStructureContext.ProjectId:D}");
        }

        return builder
            .ToString()
            .TrimEnd();
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

    private enum ProcessDependencyGraphIssueKind
    {
        SelfDependency = 0,
        Cycle = 1
    }

    private sealed record ProcessDependencyGraphIssue(
        ProcessDependencyGraphIssueKind Kind,
        IReadOnlyList<string> StepLabels);

}
