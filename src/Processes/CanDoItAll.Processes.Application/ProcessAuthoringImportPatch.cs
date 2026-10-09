using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

internal static class ProcessAuthoringImportPatch {
    public static ProcessAuthoringContent Apply(ProcessAuthoringContent target, ProcessAuthoringContent source,
        ProcessTemplateImportCommand command, ProcessTemplateImportedComponentProjection imported) {
        var provenance = new ProcessAuthoringProvenance(command.ItemKey.Value, source.Base.Version, ProcessAuthoringCodec.Hash(ProcessAuthoringCodec.Write(source)));
        var remap = new Dictionary<string, string>(StringComparer.Ordinal);
        var resources = target.RoleResources.ToDictionary(pair => pair.Key, pair => pair.Value);
        var guidance = target.Guidance.ToDictionary(pair => pair.Key, pair => pair.Value);
        switch (command.CommandKind) {
            case ProcessTemplateImportCommandKind.ImportRole:
                AddRole(source.Definition.RoleUsages.Single(role => role.Key == imported.SourceComponentKey));
                break;
            case ProcessTemplateImportCommandKind.ImportArtifact:
                var artifact = source.Definition.Steps.SelectMany(step => step.ArtifactExpectations.Select(item => (Step: step, Artifact: item)))
                    .Single(item => command.ItemKey.Value == $"artifact:{source.Definition.Key}:{item.Step.Key}:{item.Artifact.Key}").Artifact;
                var destination = target.Definition.Steps.SingleOrDefault(step => step.Key == command.TargetStepKey?.Value)
                    ?? throw new InvalidOperationException("The captured artifact target no longer exists.");
                var oldArtifact = artifact.Key;
                artifact.Key = ProcessAuthoringCanvasPatch.UniqueKey(destination.ArtifactExpectations.Select(item => item.Key), artifact.Key);
                destination.ArtifactExpectations.Add(artifact);
                remap.Add("artifact:" + oldArtifact, artifact.Key);
                break;
            case ProcessTemplateImportCommandKind.ImportProcess:
                MergeProcess();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
        return target with { RoleResources = resources, Guidance = guidance, Imports = [.. target.Imports,
            new(command.OperationId, provenance,
                command.TargetStepKey?.Value, remap, imported)] };

        void AddRole(ProcessTemplateDefinitionRoleUsageDocument role) {
            var original = role.Key;
            role.Key = ProcessAuthoringCanvasPatch.UniqueKey(target.Definition.RoleUsages.Select(item => item.Key), original);
            target.Definition.RoleUsages.Add(role);
            remap.Add("role:" + original, role.Key);
            if (source.RoleResources.TryGetValue(original, out var resource)) {
                resources.Add(role.Key, resource);
            }
        }

        void MergeProcess() {
            foreach (var role in source.Definition.RoleUsages) {
                AddRole(role);
            }
            var usedSteps = target.Definition.Steps.Select(step => step.Key).ToList();
            foreach (var step in source.Definition.Steps) {
                var key = ProcessAuthoringCanvasPatch.UniqueKey(usedSteps, step.Key);
                remap.Add("step:" + step.Key, key);
                usedSteps.Add(key);
            }
            var usedArtifacts = target.Definition.Steps.SelectMany(step => step.ArtifactExpectations.Select(artifact => artifact.Key)).ToList();
            foreach (var step in source.Definition.Steps) {
                foreach (var artifact in step.ArtifactExpectations) {
                    var key = ProcessAuthoringCanvasPatch.UniqueKey(usedArtifacts, artifact.Key);
                    remap.Add(ArtifactId(step.Key, artifact.Key), key);
                    usedArtifacts.Add(key);
                }
            }
            foreach (var driver in source.Definition.LaunchDriverActivations) {
                if (target.Definition.LaunchDriverActivations.Any(existing => existing.DriverKey == driver.DriverKey)) {
                    throw new InvalidOperationException("The imported process uses a launch driver already configured on this definition. Resolve that driver conflict before importing.");
                }
                foreach (var binding in driver.InputArtifactBindings) {
                    binding.ArtifactExpectationKey = MapArtifact(binding.SourceStepKey, binding.ArtifactExpectationKey);
                    binding.SourceStepKey = MapStep(binding.SourceStepKey);
                }
                target.Definition.LaunchDriverActivations.Add(driver);
            }
            var order = target.Definition.Steps.Count == 0 ? 0 : target.Definition.Steps.Max(step => step.Order) + 1;
            foreach (var step in source.Definition.Steps.OrderBy(step => step.Order)) {
                var originalKey = step.Key;
                step.DecisionRoleKey = MapRole(step.DecisionRoleKey);
                foreach (var binding in step.RoleAssignments) {
                    binding.RoleKey = MapRole(binding.RoleKey);
                }
                step.DependsOnStepKey = MapStep(step.DependsOnStepKey);
                foreach (var dependency in step.Dependencies) {
                    dependency.DependsOnStepKey = MapStep(dependency.DependsOnStepKey);
                }
                foreach (var input in step.ArtifactInputs) {
                    input.ArtifactExpectationKey = MapArtifact(input.SourceStepKey, input.ArtifactExpectationKey);
                    input.SourceStepKey = MapStep(input.SourceStepKey);
                }
                foreach (var route in step.BranchOutcomes) {
                    if (!string.IsNullOrWhiteSpace(route.RouteTargetArtifactExpectationKey)) {
                        route.RouteTargetArtifactExpectationKey = MapArtifact(string.IsNullOrWhiteSpace(route.RouteTargetStepKey) ? originalKey : route.RouteTargetStepKey,
                            route.RouteTargetArtifactExpectationKey);
                    }
                    route.RouteTargetStepKey = MapStep(route.RouteTargetStepKey);
                }
                foreach (var artifact in step.ArtifactExpectations) {
                    artifact.Key = MapArtifact(originalKey, artifact.Key);
                }
                if (step.ExecutionContract is { } execution) {
                    foreach (var slot in execution.ProducedArtifactSlots) {
                        slot.ArtifactExpectationKey = MapArtifact(originalKey, slot.ArtifactExpectationKey);
                    }
                }
                if (step.SubprocessContract is { } child) {
                    child.ParentProducedArtifactExpectationKey = MapArtifact(originalKey, child.ParentProducedArtifactExpectationKey);
                }
                step.Key = MapStep(originalKey);
                step.Order = order++;
                target.Definition.Steps.Add(step);
                guidance.Add(step.Key, source.Guidance.GetValueOrDefault(originalKey) ?? []);
            }
        }

        string MapStep(string key) => string.IsNullOrWhiteSpace(key) ? key : remap.GetValueOrDefault("step:" + key)
            ?? throw new InvalidOperationException("The imported process contains an unresolved step reference.");
        string MapRole(string key) => string.IsNullOrWhiteSpace(key) ? key : remap.GetValueOrDefault("role:" + key)
            ?? throw new InvalidOperationException("The imported process contains an unresolved role reference.");
        string MapArtifact(string step, string key) => string.IsNullOrWhiteSpace(key) ? key : remap.GetValueOrDefault(ArtifactId(step, key))
            ?? throw new InvalidOperationException("The imported process contains an unresolved artifact reference.");
    }

    private static string ArtifactId(string step, string artifact) => "artifact:" + step + ":" + artifact;
}
