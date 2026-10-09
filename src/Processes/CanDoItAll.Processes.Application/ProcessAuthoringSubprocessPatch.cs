using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

internal static class ProcessAuthoringSubprocessPatch {
    private const string HandoffKey = "subprocess-handoff";

    public static void Apply(ProcessTemplateDefinitionStepDocument target, ProcessTemplateDefinitionDocument child) {
        var contract = target.SubprocessContract ?? new ProcessSubprocessContract();
        contract.DefinitionKey = child.Key;
        var explicitMappings = target.ArtifactExpectations.Where(item => !string.IsNullOrWhiteSpace(item.SubprocessChildStepKey)).ToArray();
        if (explicitMappings.Length > 0) {
            contract.AcceptedChildOutputs = explicitMappings.Select(mapping => {
                var step = child.Steps.SingleOrDefault(item => item.Key == mapping.SubprocessChildStepKey)
                    ?? throw new InvalidOperationException("The mapped child step is unavailable in the selected executable definition.");
                var candidates = step.ArtifactExpectations.Where(item => item.Title == mapping.SubprocessChildArtifactTitle).ToArray();
                if (candidates.Length != 1) {
                    throw new InvalidOperationException("A child artifact mapping must identify exactly one artifact on its selected child step.");
                }
                var output = contract.AcceptedChildOutputs.SingleOrDefault(item => item.StepKey == step.Key && item.ArtifactExpectationKey == candidates[0].Key)
                    ?? new ProcessSubprocessChildOutputContract { StepKey = step.Key, ArtifactExpectationKey = candidates[0].Key };
                output.ArtifactTitle = candidates[0].Title;
                return output;
            }).ToList();
        } else if (contract.AcceptedChildOutputs.Count == 0 && contract.AlreadySatisfiedOutput is null) {
            var predecessors = child.Steps.SelectMany(step => step.Dependencies.Select(item => item.DependsOnStepKey).Append(step.DependsOnStepKey)).ToHashSet(StringComparer.Ordinal);
            contract.AcceptedChildOutputs = child.Steps.Where(step => !predecessors.Contains(step.Key))
                .SelectMany(step => step.ArtifactExpectations.Where(artifact => artifact.IsRequired).Select(artifact => new ProcessSubprocessChildOutputContract {
                    StepKey = step.Key, ArtifactExpectationKey = artifact.Key, ArtifactTitle = artifact.Title
                })).ToList();
            if (contract.AcceptedChildOutputs.Count == 0) {
                throw new InvalidOperationException("The selected child has no required terminal output. Define a child output before mapping it.");
            }
        }
        foreach (var output in contract.AcceptedChildOutputs.Concat(contract.NoGoChildOutputs).Append(contract.AlreadySatisfiedOutput).Where(item => item is not null)) {
            var step = child.Steps.SingleOrDefault(step => step.Key == output!.StepKey);
            if (step is null || !step.ArtifactExpectations.Any(artifact => artifact.Key == output!.ArtifactExpectationKey) ||
                    !string.IsNullOrEmpty(output!.BranchOutcomeKey) && !step.BranchOutcomes.Any(branch => branch.Key == output.BranchOutcomeKey)) {
                throw new InvalidOperationException("The retained child output contract is incompatible with the selected child definition.");
            }
        }
        foreach (var forwarded in contract.ForwardedChildContextArtifacts) {
            if (!child.Steps.Any(step => step.Key == forwarded.SourceStepKey && step.ArtifactExpectations.Any(artifact => artifact.Key == forwarded.ArtifactExpectationKey))) {
                throw new InvalidOperationException("The retained forwarded context is incompatible with the selected child definition.");
            }
        }
        if (string.IsNullOrEmpty(contract.ParentProducedArtifactExpectationKey)) {
            var output = target.ArtifactExpectations.FirstOrDefault();
            if (output is null) {
                output = new() { Key = HandoffKey, Title = "Subprocess handoff", ArtifactKind = "Artifact", IsRequired = true };
                target.ArtifactExpectations.Add(output);
            }
            contract.ParentProducedArtifactExpectationKey = output.Key;
        }
        target.SubprocessContract = contract;
        target.SubprocessProcessKey = child.Key;
        target.SubprocessDefinitionSnapshotName = child.DisplayName;
    }
}
