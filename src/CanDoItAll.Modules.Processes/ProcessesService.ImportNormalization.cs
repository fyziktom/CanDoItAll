namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private static void PrepareImportedDefinitionForSave(ProcessDefinitionEditorModel model)
    {
        model.Id = null;
        model.WorkingVersionId = null;
        model.DefinitionConcurrencyToken = null;
        model.WorkingVersionConcurrencyToken = null;
        model.WorkingVersionNumber = 1;

        var roleIdMap = new Dictionary<Guid, Guid>();
        foreach (var role in model.Roles)
        {
            var importedRoleId = role.Id.GetValueOrDefault();
            var generatedRoleId = Guid.NewGuid();
            if (importedRoleId != Guid.Empty)
            {
                roleIdMap[importedRoleId] = generatedRoleId;
            }

            role.Id = generatedRoleId;
        }

        var stepIdMap = new Dictionary<Guid, Guid>();
        var branchOutcomeIdMap = new Dictionary<Guid, Guid>();
        var artifactExpectationIdMap = new Dictionary<Guid, Guid>();

        foreach (var step in model.Steps)
        {
            var importedStepId = step.Id.GetValueOrDefault();
            var generatedStepId = Guid.NewGuid();
            if (importedStepId != Guid.Empty)
            {
                stepIdMap[importedStepId] = generatedStepId;
            }

            step.Id = generatedStepId;

            foreach (var branchOutcome in step.BranchOutcomes)
            {
                var importedBranchOutcomeId = branchOutcome.Id.GetValueOrDefault();
                var generatedBranchOutcomeId = Guid.NewGuid();
                if (importedBranchOutcomeId != Guid.Empty)
                {
                    branchOutcomeIdMap[importedBranchOutcomeId] = generatedBranchOutcomeId;
                }

                branchOutcome.Id = generatedBranchOutcomeId;
            }

            foreach (var artifactExpectation in step.ArtifactExpectations)
            {
                var importedArtifactExpectationId = artifactExpectation.Id.GetValueOrDefault();
                var generatedArtifactExpectationId = Guid.NewGuid();
                if (importedArtifactExpectationId != Guid.Empty)
                {
                    artifactExpectationIdMap[importedArtifactExpectationId] = generatedArtifactExpectationId;
                }

                artifactExpectation.Id = generatedArtifactExpectationId;
            }

            foreach (var dependency in step.Dependencies)
            {
                dependency.Id = Guid.NewGuid();
            }

            foreach (var roleAssignment in step.RoleAssignments)
            {
                roleAssignment.Id = Guid.NewGuid();
            }

            foreach (var artifactInput in step.ArtifactInputs)
            {
                artifactInput.Id = Guid.NewGuid();
            }
        }

        foreach (var step in model.Steps)
        {
            step.DecisionRoleRequirementId = RemapImportedId(step.DecisionRoleRequirementId, roleIdMap);
            step.DependsOnStepId = RemapImportedId(step.DependsOnStepId, stepIdMap);
            step.DependsOnBranchOutcomeId = RemapImportedId(step.DependsOnBranchOutcomeId, branchOutcomeIdMap);

            foreach (var dependency in step.Dependencies)
            {
                dependency.DependsOnStepId = RemapImportedId(dependency.DependsOnStepId, stepIdMap);
                dependency.DependsOnBranchOutcomeId = RemapImportedId(dependency.DependsOnBranchOutcomeId, branchOutcomeIdMap);
            }

            foreach (var roleAssignment in step.RoleAssignments)
            {
                roleAssignment.RoleRequirementId = RemapImportedId(roleAssignment.RoleRequirementId, roleIdMap);
            }

            foreach (var artifactInput in step.ArtifactInputs)
            {
                artifactInput.ArtifactExpectationId = RemapImportedId(artifactInput.ArtifactExpectationId, artifactExpectationIdMap);
            }
        }
    }

    private static Guid? RemapImportedId(Guid? currentId, IReadOnlyDictionary<Guid, Guid> idMap)
    {
        if (!currentId.HasValue || currentId.Value == Guid.Empty)
        {
            return null;
        }

        return idMap.TryGetValue(currentId.Value, out var remappedId)
            ? remappedId
            : currentId;
    }
}
