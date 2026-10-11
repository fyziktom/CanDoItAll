using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

internal static class ProcessAuthoringPublicationValidator {
    public static void Validate(ProcessExecutableDefinitionClosure closure) {
        var documents = closure.Definitions.Keys.ToDictionary(key => key, key => ProcessExecutableDefinitionResolver.Decode(closure, key).Definition, StringComparer.Ordinal);
        ProcessTemplatePackLoader.ValidateExecutableDocuments(documents.Values.ToArray());
        foreach (var definition in documents.Values) {
            var roles = definition.RoleUsages.Select(role => role.Key).ToHashSet(StringComparer.Ordinal);
            var steps = definition.Steps.ToDictionary(step => step.Key, StringComparer.Ordinal);
            if (steps.Count == 0) {
                throw new InvalidOperationException("A published definition requires at least one executable step.");
            }
            foreach (var step in definition.Steps) {
                if (!string.IsNullOrEmpty(step.DecisionRoleKey) && !roles.Contains(step.DecisionRoleKey) ||
                        step.RoleAssignments.Any(binding => !roles.Contains(binding.RoleKey))) {
                    throw new InvalidOperationException($"Step '{step.Key}' references a role outside the definition.");
                }
                RequireUnique(step.ArtifactExpectations.Select(artifact => artifact.Key), "artifact", step.Key);
                RequireUnique(step.BranchOutcomes.Select(outcome => outcome.Key), "branch", step.Key);
                RequireDependency(step.DependsOnStepKey, step.DependsOnBranchOutcomeKey);
                foreach (var dependency in step.Dependencies) {
                    RequireDependency(dependency.DependsOnStepKey, dependency.DependsOnBranchOutcomeKey);
                }
                foreach (var input in step.ArtifactInputs) {
                    RequireArtifact(definition, input.SourceStepKey, input.ArtifactExpectationKey);
                }
                foreach (var outcome in step.BranchOutcomes) {
                    if (!string.IsNullOrEmpty(outcome.RouteTargetStepKey) && !steps.ContainsKey(outcome.RouteTargetStepKey)) {
                        throw new InvalidOperationException($"Step '{step.Key}' routes to an unknown step.");
                    }
                    if (!string.IsNullOrEmpty(outcome.RouteTargetArtifactExpectationKey)) {
                        RequireArtifact(definition, string.IsNullOrEmpty(outcome.RouteTargetStepKey) ? step.Key : outcome.RouteTargetStepKey,
                            outcome.RouteTargetArtifactExpectationKey);
                    }
                }
                foreach (var guidance in step.ResolvedExecutionGuidance) {
                    if (ProcessAuthoringCodec.Hash(guidance.Content) != guidance.ContentHash) {
                        throw new InvalidOperationException($"Step '{step.Key}' has inconsistent pinned execution guidance.");
                    }
                }
                if (step.SubprocessContract is { } child) {
                    if (!documents.TryGetValue(child.DefinitionKey, out var childDefinition)) {
                        throw new InvalidOperationException($"Step '{step.Key}' has an unresolved child definition.");
                    }
                    foreach (var output in child.AcceptedChildOutputs.Concat(child.NoGoChildOutputs).Append(child.AlreadySatisfiedOutput).Where(output => output is not null)) {
                        var producer = RequireArtifact(childDefinition, output!.StepKey, output.ArtifactExpectationKey);
                        if (!string.IsNullOrEmpty(output.BranchOutcomeKey) && !producer.BranchOutcomes.Any(branch => branch.Key == output.BranchOutcomeKey)) {
                            throw new InvalidOperationException($"Step '{step.Key}' references an unknown child branch outcome.");
                        }
                    }
                }
            }

            void RequireDependency(string source, string branch) {
                if (string.IsNullOrEmpty(source)) {
                    if (!string.IsNullOrEmpty(branch)) {
                        throw new InvalidOperationException("A branch dependency requires its source step.");
                    }
                    return;
                }
                if (!steps.TryGetValue(source, out var predecessor) || !string.IsNullOrEmpty(branch) && !predecessor.BranchOutcomes.Any(outcome => outcome.Key == branch)) {
                    throw new InvalidOperationException("An authored dependency references an unknown step or branch outcome.");
                }
            }
        }
    }

    private static void RequireUnique(IEnumerable<string> keys, string kind, string step) {
        var values = keys.ToArray();
        if (values.Any(string.IsNullOrWhiteSpace) || values.Distinct(StringComparer.Ordinal).Count() != values.Length) {
            throw new InvalidOperationException($"Step '{step}' contains an absent or duplicated {kind} identity.");
        }
    }

    private static ProcessTemplateDefinitionStepDocument RequireArtifact(ProcessTemplateDefinitionDocument definition, string step, string artifact) {
        var producer = definition.Steps.SingleOrDefault(item => item.Key == step);
        if (producer is null || !producer.ArtifactExpectations.Any(item => item.Key == artifact)) {
            throw new InvalidOperationException($"Definition '{definition.Key}' has an unresolved artifact reference '{step}/{artifact}'.");
        }
        return producer;
    }
}
