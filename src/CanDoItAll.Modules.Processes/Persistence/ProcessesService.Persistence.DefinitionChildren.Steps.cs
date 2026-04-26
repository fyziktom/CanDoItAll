namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private async Task<IReadOnlyList<ResolvedProcessStep>> PersistDefinitionStepsAsync(
        DefinitionChildrenSaveContext context,
        ProcessDefinitionEditorModel model,
        CancellationToken cancellationToken)
    {
        var resolvedSteps = new List<ResolvedProcessStep>(model.Steps.Count);
        for (var index = 0; index < model.Steps.Count; index++)
        {
            var stepModel = model.Steps[index];
            var stepId = ResolveStableChildId(stepModel.Id, context.AssignedStepIds, "step");
            if (stepModel.Id.HasValue && stepModel.Id.Value != Guid.Empty)
            {
                context.StepIdMap[stepModel.Id.Value] = stepId;
            }

            var reusesExistingEntity = context.StepsById.TryGetValue(stepId, out var step);
            if (!reusesExistingEntity)
            {
                step = new ProcessStepDefinition
                {
                    Id = stepId,
                    ProcessDefinitionVersionId = context.WorkingVersionId
                };

                await context.DbContext.Set<ProcessStepDefinition>().AddAsync(step, cancellationToken);
                context.StepsById[stepId] = step;
            }

            step.ProcessDefinitionVersionId = context.WorkingVersionId;
            step.Key = string.IsNullOrWhiteSpace(stepModel.Key)
                ? BuildKey(stepModel.Title, $"step-{index + 1}")
                : BuildKey(stepModel.Key, $"step-{index + 1}");
            step.Title = stepModel.Title.Trim();
            step.Subtitle = stepModel.Subtitle.Trim();
            step.Notes = stepModel.Notes.Trim();
            step.StepKind = stepModel.StepKind;
            step.AllowsManualSkip = stepModel.AllowsManualSkip;
            step.AllowsSafeRefusal = stepModel.AllowsSafeRefusal;
            step.RequiresApproval = stepModel.RequiresApproval;
            step.RequiresDecisionRecord = stepModel.RequiresDecisionRecord;
            step.InputContractSummary = stepModel.InputContractSummary.Trim();
            step.OutputContractSummary = stepModel.OutputContractSummary.Trim();
            step.EvidenceContractSummary = stepModel.EvidenceContractSummary.Trim();
            step.DecisionRightsSummary = stepModel.DecisionRightsSummary.Trim();
            step.ExceptionPolicySummary = stepModel.ExceptionPolicySummary.Trim();
            step.TargetLeadHours = Math.Max(0, stepModel.TargetLeadHours);
            step.OrderIndex = index;
            step.DecisionRoleRequirementId = stepModel.DecisionRoleRequirementId.HasValue &&
                context.RoleIdMap.TryGetValue(stepModel.DecisionRoleRequirementId.Value, out var remappedDecisionRoleId)
                ? remappedDecisionRoleId
                : stepModel.DecisionRoleRequirementId;
            step.CanvasX = stepModel.CanvasX;
            step.CanvasY = stepModel.CanvasY;
            step.BranchCanvasX = stepModel.BranchCanvasX;
            step.BranchCanvasY = stepModel.BranchCanvasY;

            context.RetainedStepIds.Add(stepId);
            resolvedSteps.Add(new ResolvedProcessStep(
                stepId,
                reusesExistingEntity,
                step,
                stepModel,
                ProcessCanvasBranching.GetOrderedDependencies(stepModel)
                    .Select(dependency => new ProcessStepDependencyEditorModel
                    {
                        Id = dependency.Id,
                        DependsOnStepId = dependency.DependsOnStepId,
                        DependsOnBranchOutcomeId = dependency.DependsOnBranchOutcomeId
                    })
                    .ToList()));
        }

        foreach (var resolvedStep in resolvedSteps)
        {
            if (!context.ExistingBranchOutcomesByStepId.TryGetValue(resolvedStep.StepId, out var existingOutcomesForStep))
            {
                existingOutcomesForStep = [];
                context.ExistingBranchOutcomesByStepId[resolvedStep.StepId] = existingOutcomesForStep;
            }

            var existingOutcomesByKey = existingOutcomesForStep
                .GroupBy(item => item.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

            for (var outcomeIndex = 0; outcomeIndex < resolvedStep.Model.BranchOutcomes.Count; outcomeIndex++)
            {
                var outcomeModel = resolvedStep.Model.BranchOutcomes[outcomeIndex];
                var resolvedKey = string.IsNullOrWhiteSpace(outcomeModel.Key)
                    ? BuildKey(outcomeModel.Title, $"outcome-{outcomeIndex + 1}")
                    : BuildKey(outcomeModel.Key, $"outcome-{outcomeIndex + 1}");
                ProcessStepBranchOutcomeDefinition? branchOutcome = null;
                var requestedOutcomeId = ResolveStableChildId(
                    resolvedStep.ReusesExistingEntity ? outcomeModel.Id : null,
                    context.AssignedBranchOutcomeIds,
                    "branch outcome");
                if (outcomeModel.Id.HasValue && outcomeModel.Id.Value != Guid.Empty)
                {
                    context.BranchOutcomeIdMap[outcomeModel.Id.Value] = requestedOutcomeId;
                }

                if (resolvedStep.ReusesExistingEntity &&
                    context.BranchOutcomesById.TryGetValue(requestedOutcomeId, out var existingOutcome))
                {
                    branchOutcome = existingOutcome;
                }
                else if ((!outcomeModel.Id.HasValue || outcomeModel.Id.Value == Guid.Empty) &&
                         existingOutcomesByKey.TryGetValue(resolvedKey, out var matchingOutcomes))
                {
                    branchOutcome = matchingOutcomes.FirstOrDefault(candidate => !context.RetainedBranchOutcomeIds.Contains(candidate.Id));
                }

                if (branchOutcome is null)
                {
                    branchOutcome = new ProcessStepBranchOutcomeDefinition
                    {
                        Id = requestedOutcomeId
                    };

                    await context.DbContext.Set<ProcessStepBranchOutcomeDefinition>().AddAsync(branchOutcome, cancellationToken);
                    context.ExistingBranchOutcomes.Add(branchOutcome);
                    existingOutcomesForStep.Add(branchOutcome);
                    context.BranchOutcomesById[branchOutcome.Id] = branchOutcome;
                }

                branchOutcome.StepDefinitionId = resolvedStep.StepId;
                branchOutcome.Key = resolvedKey;
                branchOutcome.Title = outcomeModel.Title.Trim();
                branchOutcome.Description = outcomeModel.Description.Trim();
                branchOutcome.DisplayOrder = outcomeIndex;

                context.BranchOutcomeIdMap[branchOutcome.Id] = branchOutcome.Id;
                context.RetainedBranchOutcomeIds.Add(branchOutcome.Id);
            }
        }

        return resolvedSteps;
    }

    private async Task PersistDefinitionDependenciesAsync(
        DefinitionChildrenSaveContext context,
        IReadOnlyList<ResolvedProcessStep> resolvedSteps,
        CancellationToken cancellationToken)
    {
        foreach (var resolvedStep in resolvedSteps)
        {
            if (!context.ExistingDependenciesByStepId.TryGetValue(resolvedStep.StepId, out var existingDependenciesForStep))
            {
                existingDependenciesForStep = [];
                context.ExistingDependenciesByStepId[resolvedStep.StepId] = existingDependenciesForStep;
            }

            var existingDependenciesByShape = existingDependenciesForStep
                .GroupBy(item => (item.DependsOnStepId, item.DependsOnBranchOutcomeId))
                .ToDictionary(group => group.Key, group => group.ToList());

            for (var dependencyIndex = 0; dependencyIndex < resolvedStep.Dependencies.Count; dependencyIndex++)
            {
                var dependencyModel = resolvedStep.Dependencies[dependencyIndex];
                if (!dependencyModel.DependsOnStepId.HasValue || dependencyModel.DependsOnStepId.Value == Guid.Empty)
                {
                    continue;
                }

                var remappedDependsOnStepId = context.StepIdMap.TryGetValue(dependencyModel.DependsOnStepId.Value, out var mappedDependsOnStepId)
                    ? mappedDependsOnStepId
                    : dependencyModel.DependsOnStepId.Value;
                if (!context.StepsById.ContainsKey(remappedDependsOnStepId))
                {
                    throw new InvalidOperationException($"Dependency step '{dependencyModel.DependsOnStepId.Value:D}' could not be resolved during save.");
                }

                Guid? remappedDependsOnBranchOutcomeId = null;
                if (dependencyModel.DependsOnBranchOutcomeId.HasValue)
                {
                    remappedDependsOnBranchOutcomeId = context.BranchOutcomeIdMap.TryGetValue(dependencyModel.DependsOnBranchOutcomeId.Value, out var mappedOutcomeId)
                        ? mappedOutcomeId
                        : dependencyModel.DependsOnBranchOutcomeId.Value;
                    if (remappedDependsOnBranchOutcomeId.HasValue && !context.BranchOutcomesById.ContainsKey(remappedDependsOnBranchOutcomeId.Value))
                    {
                        throw new InvalidOperationException($"Dependency branch outcome '{dependencyModel.DependsOnBranchOutcomeId.Value:D}' could not be resolved during save.");
                    }
                }

                ProcessStepDependencyDefinition? dependency = null;
                var requestedDependencyId = ResolveStableChildId(
                    resolvedStep.ReusesExistingEntity ? dependencyModel.Id : null,
                    context.AssignedDependencyIds,
                    "step dependency");
                if (resolvedStep.ReusesExistingEntity &&
                    dependencyModel.Id.HasValue &&
                    dependencyModel.Id.Value != Guid.Empty &&
                    context.DependenciesById.TryGetValue(requestedDependencyId, out var existingDependency))
                {
                    dependency = existingDependency;
                }
                else if ((!dependencyModel.Id.HasValue || dependencyModel.Id.Value == Guid.Empty) &&
                         existingDependenciesByShape.TryGetValue((remappedDependsOnStepId, remappedDependsOnBranchOutcomeId), out var matchingDependencies))
                {
                    dependency = matchingDependencies.FirstOrDefault(candidate => !context.RetainedDependencyIds.Contains(candidate.Id));
                }

                if (dependency is null)
                {
                    dependency = new ProcessStepDependencyDefinition
                    {
                        Id = requestedDependencyId
                    };

                    await context.DbContext.Set<ProcessStepDependencyDefinition>().AddAsync(dependency, cancellationToken);
                    context.ExistingDependencies.Add(dependency);
                    existingDependenciesForStep.Add(dependency);
                    context.DependenciesById[dependency.Id] = dependency;
                }

                dependency.StepDefinitionId = resolvedStep.StepId;
                dependency.DependsOnStepId = remappedDependsOnStepId;
                dependency.DependsOnBranchOutcomeId = remappedDependsOnBranchOutcomeId;
                dependency.DisplayOrder = dependencyIndex;

                context.RetainedDependencyIds.Add(dependency.Id);
            }
        }
    }
}
