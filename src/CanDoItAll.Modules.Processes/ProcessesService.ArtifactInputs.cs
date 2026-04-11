using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private static List<ProcessStepArtifactInputEditorModel> BuildEditorArtifactInputs(
        ProcessStepDefinition step,
        IReadOnlyList<ProcessStepArtifactInputDefinition> allArtifactInputs)
    {
        return allArtifactInputs
            .Where(item => item.StepDefinitionId == step.Id)
            .OrderBy(item => item.DisplayOrder)
            .Select(item => new ProcessStepArtifactInputEditorModel
            {
                Id = item.Id,
                ArtifactExpectationId = item.ArtifactExpectationId
            })
            .ToList();
    }

    private static Error? ValidateArtifactInputs(ProcessDefinitionEditorModel model)
    {
        var stepsById = model.Steps
            .Where(step => step.Id.HasValue)
            .ToDictionary(step => step.Id!.Value);
        var artifactOwnerById = model.Steps
            .SelectMany(step => step.ArtifactExpectations
                .Where(artifact => artifact.Id.HasValue)
                .Select(artifact => new KeyValuePair<Guid, ProcessStepEditorModel>(artifact.Id!.Value, step)))
            .ToDictionary(item => item.Key, item => item.Value);

        foreach (var step in model.Steps)
        {
            foreach (var artifactInput in step.ArtifactInputs)
            {
                if (!artifactInput.ArtifactExpectationId.HasValue)
                {
                    return Error.Validation("Every artifact input must resolve to an upstream artifact expectation.", "processes.artifact-input-artifact-required");
                }

                if (!artifactOwnerById.TryGetValue(artifactInput.ArtifactExpectationId.Value, out var sourceStep))
                {
                    return Error.Validation("Artifact inputs must reference an artifact expectation in the same definition.", "processes.artifact-input-artifact-invalid");
                }

                if (!sourceStep.Id.HasValue || sourceStep == step)
                {
                    return Error.Validation("Artifact inputs must reference an upstream step instead of the consuming step itself.", "processes.artifact-input-self-reference");
                }

                if (step.Id.HasValue && !stepsById.ContainsKey(step.Id.Value))
                {
                    return Error.Validation("Artifact-consuming steps must belong to the same definition.", "processes.artifact-input-step-invalid");
                }

                if (ProcessCanvasBranching.GetOrderedDependencies(step).All(dependency => dependency.DependsOnStepId != sourceStep.Id))
                {
                    return Error.Validation("Artifact inputs must also keep an explicit structural dependency on the producing step.", "processes.artifact-input-dependency-required");
                }
            }
        }

        return null;
    }

    private static Error? ValidatePublishedArtifactInputs(
        IReadOnlyList<ProcessStepDefinition> steps,
        IReadOnlyList<ProcessArtifactExpectation> artifactExpectations,
        IReadOnlyList<ProcessStepArtifactInputDefinition> artifactInputs,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> stepDependenciesByStepId)
    {
        var stepsById = steps.ToDictionary(step => step.Id);
        var artifactOwnerById = artifactExpectations.ToDictionary(item => item.Id, item => item.StepDefinitionId);

        foreach (var artifactInput in artifactInputs.OrderBy(item => item.DisplayOrder))
        {
            if (!stepsById.TryGetValue(artifactInput.StepDefinitionId, out var consumingStep))
            {
                return Error.Validation("Published artifact inputs must belong to a published step.", "processes.publish-artifact-input-step-invalid");
            }

            if (!artifactOwnerById.TryGetValue(artifactInput.ArtifactExpectationId, out var producingStepId))
            {
                return Error.Validation("Published artifact inputs must reference a published artifact expectation.", "processes.publish-artifact-input-artifact-invalid");
            }

            if (producingStepId == consumingStep.Id)
            {
                return Error.Validation("Published artifact inputs must reference an upstream step instead of the consuming step itself.", "processes.publish-artifact-input-self-reference");
            }

            if (GetPersistedDependencies(consumingStep, stepDependenciesByStepId).All(dependency => dependency.DependsOnStepId != producingStepId))
            {
                return Error.Validation("Published artifact inputs must retain an explicit structural dependency on the producing step.", "processes.publish-artifact-input-dependency-required");
            }
        }

        return null;
    }
}
