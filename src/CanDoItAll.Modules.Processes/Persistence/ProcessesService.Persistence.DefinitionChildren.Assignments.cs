namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private async Task PersistDefinitionAssignmentsAndArtifactsAsync(
        DefinitionChildrenSaveContext context,
        IReadOnlyList<ResolvedProcessStep> resolvedSteps,
        CancellationToken cancellationToken)
    {
        foreach (var resolvedStep in resolvedSteps)
        {
            if (!context.ExistingAssignmentsByStepId.TryGetValue(resolvedStep.StepId, out var existingAssignmentsForStep))
            {
                existingAssignmentsForStep = [];
                context.ExistingAssignmentsByStepId[resolvedStep.StepId] = existingAssignmentsForStep;
            }

            var existingAssignmentsByShape = existingAssignmentsForStep
                .GroupBy(item => (item.RoleRequirementId, item.ResponsibilityKind))
                .ToDictionary(group => group.Key, group => group.ToList());

            foreach (var roleAssignmentModel in resolvedStep.Model.RoleAssignments)
            {
                if (!roleAssignmentModel.RoleRequirementId.HasValue || roleAssignmentModel.RoleRequirementId.Value == Guid.Empty)
                {
                    continue;
                }

                var resolvedRoleId = context.RoleIdMap.TryGetValue(roleAssignmentModel.RoleRequirementId.Value, out var remappedRoleId)
                    ? remappedRoleId
                    : roleAssignmentModel.RoleRequirementId.Value;
                if (!context.RolesById.ContainsKey(resolvedRoleId))
                {
                    throw new InvalidOperationException($"Role requirement '{roleAssignmentModel.RoleRequirementId.Value:D}' could not be resolved during save.");
                }

                ProcessStepRoleAssignmentRequirement? assignment = null;
                var requestedAssignmentId = ResolveStableChildId(
                    resolvedStep.ReusesExistingEntity ? roleAssignmentModel.Id : null,
                    context.AssignedAssignmentIds,
                    "step role assignment");
                if (resolvedStep.ReusesExistingEntity &&
                    roleAssignmentModel.Id.HasValue &&
                    roleAssignmentModel.Id.Value != Guid.Empty &&
                    context.AssignmentsById.TryGetValue(requestedAssignmentId, out var existingAssignment))
                {
                    assignment = existingAssignment;
                }
                else if ((!roleAssignmentModel.Id.HasValue || roleAssignmentModel.Id.Value == Guid.Empty) &&
                         existingAssignmentsByShape.TryGetValue((resolvedRoleId, roleAssignmentModel.ResponsibilityKind), out var matchingAssignments))
                {
                    assignment = matchingAssignments.FirstOrDefault(candidate => !context.RetainedAssignmentIds.Contains(candidate.Id));
                }

                if (assignment is null)
                {
                    assignment = new ProcessStepRoleAssignmentRequirement
                    {
                        Id = requestedAssignmentId
                    };

                    await context.DbContext.Set<ProcessStepRoleAssignmentRequirement>().AddAsync(assignment, cancellationToken);
                    context.ExistingAssignments.Add(assignment);
                    existingAssignmentsForStep.Add(assignment);
                    context.AssignmentsById[assignment.Id] = assignment;
                }

                assignment.StepDefinitionId = resolvedStep.StepId;
                assignment.RoleRequirementId = resolvedRoleId;
                assignment.ResponsibilityKind = roleAssignmentModel.ResponsibilityKind;
                assignment.IsRequired = roleAssignmentModel.IsRequired;
                assignment.FallbackOrder = Math.Max(0, roleAssignmentModel.FallbackOrder);
                assignment.RebindPolicySummary = roleAssignmentModel.RebindPolicySummary.Trim();

                context.RetainedAssignmentIds.Add(assignment.Id);
            }

            foreach (var artifactModel in resolvedStep.Model.ArtifactExpectations)
            {
                if (string.IsNullOrWhiteSpace(artifactModel.Title))
                {
                    continue;
                }

                ProcessArtifactExpectation? artifactExpectation = null;
                var requestedArtifactExpectationId = ResolveStableChildId(
                    resolvedStep.ReusesExistingEntity ? artifactModel.Id : null,
                    context.AssignedArtifactExpectationIds,
                    "artifact expectation");
                if (artifactModel.Id.HasValue && artifactModel.Id.Value != Guid.Empty)
                {
                    context.ArtifactExpectationIdMap[artifactModel.Id.Value] = requestedArtifactExpectationId;
                }

                if (resolvedStep.ReusesExistingEntity &&
                    artifactModel.Id.HasValue &&
                    artifactModel.Id.Value != Guid.Empty &&
                    context.ArtifactExpectationsById.TryGetValue(requestedArtifactExpectationId, out var existingArtifactExpectation))
                {
                    artifactExpectation = existingArtifactExpectation;
                }

                if (artifactExpectation is null)
                {
                    artifactExpectation = new ProcessArtifactExpectation
                    {
                        Id = requestedArtifactExpectationId
                    };

                    await context.DbContext.Set<ProcessArtifactExpectation>().AddAsync(artifactExpectation, cancellationToken);
                    context.ExistingArtifactExpectations.Add(artifactExpectation);
                    context.ArtifactExpectationsById[artifactExpectation.Id] = artifactExpectation;
                }

                artifactExpectation.StepDefinitionId = resolvedStep.StepId;
                artifactExpectation.ArtifactKind = artifactModel.ArtifactKind;
                artifactExpectation.Title = artifactModel.Title.Trim();
                artifactExpectation.IsRequired = artifactModel.IsRequired;
                artifactExpectation.TrustRequirement = artifactModel.TrustRequirement;
                artifactExpectation.SensitivityLevel = artifactModel.SensitivityLevel;
                artifactExpectation.RetentionDays = Math.Max(0, artifactModel.RetentionDays);
                artifactExpectation.AllowedFutureUsageSummary = artifactModel.AllowedFutureUsageSummary.Trim();
                artifactExpectation.ValidationRequirementSummary = artifactModel.ValidationRequirementSummary.Trim();
                artifactExpectation.WorkflowOutputId = artifactModel.WorkflowOutputId.Trim();
                artifactExpectation.WorkflowOutputName = artifactModel.WorkflowOutputName.Trim();
                artifactExpectation.WorkflowOutputKind = artifactModel.WorkflowOutputKind;
                artifactExpectation.SubprocessChildArtifactExpectationId =
                    artifactModel.SubprocessChildArtifactExpectationId is { } subprocessChildArtifactExpectationId &&
                    subprocessChildArtifactExpectationId != Guid.Empty
                        ? subprocessChildArtifactExpectationId
                        : null;
                artifactExpectation.SubprocessChildStepKey = artifactModel.SubprocessChildStepKey.Trim();
                artifactExpectation.SubprocessChildArtifactTitle = artifactModel.SubprocessChildArtifactTitle.Trim();

                context.ArtifactExpectationIdMap[artifactExpectation.Id] = artifactExpectation.Id;
                context.RetainedArtifactExpectationIds.Add(artifactExpectation.Id);
            }
        }

        foreach (var resolvedStep in resolvedSteps)
        {
            if (!context.ExistingArtifactInputsByStepId.TryGetValue(resolvedStep.StepId, out var existingArtifactInputsForStep))
            {
                existingArtifactInputsForStep = [];
                context.ExistingArtifactInputsByStepId[resolvedStep.StepId] = existingArtifactInputsForStep;
            }

            var existingArtifactInputsByArtifactId = existingArtifactInputsForStep
                .GroupBy(item => item.ArtifactExpectationId)
                .ToDictionary(group => group.Key, group => group.ToList());

            for (var artifactInputIndex = 0; artifactInputIndex < resolvedStep.Model.ArtifactInputs.Count; artifactInputIndex++)
            {
                var artifactInputModel = resolvedStep.Model.ArtifactInputs[artifactInputIndex];
                if (!artifactInputModel.ArtifactExpectationId.HasValue || artifactInputModel.ArtifactExpectationId.Value == Guid.Empty)
                {
                    continue;
                }

                var remappedArtifactExpectationId = context.ArtifactExpectationIdMap.TryGetValue(artifactInputModel.ArtifactExpectationId.Value, out var mappedArtifactExpectationId)
                    ? mappedArtifactExpectationId
                    : artifactInputModel.ArtifactExpectationId.Value;
                if (!context.ArtifactExpectationsById.ContainsKey(remappedArtifactExpectationId))
                {
                    throw new InvalidOperationException($"Artifact expectation '{artifactInputModel.ArtifactExpectationId.Value:D}' could not be resolved during save.");
                }

                ProcessStepArtifactInputDefinition? artifactInput = null;
                var requestedArtifactInputId = ResolveStableChildId(
                    resolvedStep.ReusesExistingEntity ? artifactInputModel.Id : null,
                    context.AssignedArtifactInputIds,
                    "artifact input");
                if (resolvedStep.ReusesExistingEntity &&
                    artifactInputModel.Id.HasValue &&
                    artifactInputModel.Id.Value != Guid.Empty &&
                    context.ArtifactInputsById.TryGetValue(requestedArtifactInputId, out var existingArtifactInput))
                {
                    artifactInput = existingArtifactInput;
                }
                else if ((!artifactInputModel.Id.HasValue || artifactInputModel.Id.Value == Guid.Empty) &&
                         existingArtifactInputsByArtifactId.TryGetValue(remappedArtifactExpectationId, out var matchingArtifactInputs))
                {
                    artifactInput = matchingArtifactInputs.FirstOrDefault(candidate => !context.RetainedArtifactInputIds.Contains(candidate.Id));
                }

                if (artifactInput is null)
                {
                    artifactInput = new ProcessStepArtifactInputDefinition
                    {
                        Id = requestedArtifactInputId
                    };

                    await context.DbContext.Set<ProcessStepArtifactInputDefinition>().AddAsync(artifactInput, cancellationToken);
                    context.ExistingArtifactInputs.Add(artifactInput);
                    existingArtifactInputsForStep.Add(artifactInput);
                    context.ArtifactInputsById[artifactInput.Id] = artifactInput;
                }

                artifactInput.StepDefinitionId = resolvedStep.StepId;
                artifactInput.ArtifactExpectationId = remappedArtifactExpectationId;
                artifactInput.DisplayOrder = artifactInputIndex;

                context.RetainedArtifactInputIds.Add(artifactInput.Id);
            }
        }
    }
}
