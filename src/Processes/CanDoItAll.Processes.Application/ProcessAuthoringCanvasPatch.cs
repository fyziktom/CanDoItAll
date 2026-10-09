using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

internal static class ProcessAuthoringCanvasPatch {
    public static ProcessAuthoringContent Apply(ProcessAuthoringContent content, ProcessDefinitionCanvasCommand command,
        ProcessDefinitionCanvasEditorProjection before, ProcessDefinitionCanvasEditorProjection after, ProcessTemplatePackLoader templates) {
        var knownNodes = before.Nodes.Select(node => node.NodeKey).ToHashSet();
        var added = after.Nodes.Where(node => !knownNodes.Contains(node.NodeKey)).ToArray();
        var definition = content.Definition;
        if (command.CommandKind == ProcessDefinitionCanvasCommandKind.AddStep) {
            var node = added.Single(item => item.Kind == ProcessDefinitionCanvasNodeKind.Step);
            var step = templates.LoadStepTemplate(command.ToolboxActionKey!.Value.Value);
            step.Key = node.StepKey!.Value.Value;
            step.Order = definition.Steps.Count == 0 ? 0 : definition.Steps.Max(item => item.Order) + 1;
            var parent = before.Nodes.FirstOrDefault(item => item.NodeKey == command.SelectedNodeKey);
            step.Dependencies = parent?.StepKey is { } parentKey ? [new() { DependsOnStepKey = parentKey.Value }] : [];
            step.DependsOnStepKey = parent?.StepKey?.Value ?? string.Empty;
            step.DependsOnBranchOutcomeKey = string.Empty;
            if (parent is { Kind: ProcessDefinitionCanvasNodeKind.BranchRouter, StepKey: { } branchKey }) {
                var parentStep = definition.Steps.Single(item => item.Key == branchKey.Value);
                var outcomeKey = UniqueKey(parentStep.BranchOutcomes.Select(item => item.Key), step.Key + "-route");
                parentStep.BranchOutcomes.Add(new() { Key = outcomeKey, Title = "Continue", RouteTargetKind = "SpecificStep", RouteTargetStepKey = step.Key });
                step.Dependencies[0].DependsOnBranchOutcomeKey = outcomeKey;
                step.DependsOnBranchOutcomeKey = outcomeKey;
            }
            definition.Steps.Add(step);
            var guidance = content.Guidance.ToDictionary(pair => pair.Key, pair => pair.Value);
            guidance.Add(step.Key, step.ResolvedExecutionGuidance);
            content = content with { Guidance = guidance };
        }
        if (command.CommandKind is ProcessDefinitionCanvasCommandKind.AddBranchRouter or ProcessDefinitionCanvasCommandKind.AddStep) {
            foreach (var router in added.Where(node => node.Kind == ProcessDefinitionCanvasNodeKind.BranchRouter)) {
                var step = definition.Steps.Single(item => item.Key == router.StepKey!.Value.Value);
                if (step.BranchOutcomes.Count == 0) {
                    step.BranchOutcomes.Add(new() { Key = step.Key + "-continue", Title = "Continue", RouteTargetKind = "NextStep" });
                }
            }
        }
        if (command.CommandKind == ProcessDefinitionCanvasCommandKind.AddRoleBinding) {
            var existingEdges = before.Edges.Select(edge => edge.EdgeKey).ToHashSet();
            foreach (var edge in after.Edges.Where(edge => edge.Kind == ProcessDefinitionCanvasEdgeKind.RoleBinding && !existingEdges.Contains(edge.EdgeKey))) {
                var role = after.Nodes.Single(node => node.NodeKey == edge.FromNodeKey).RoleKey!.Value.Value;
                var stepKey = after.Nodes.Single(node => node.NodeKey == edge.ToNodeKey).StepKey!.Value.Value;
                definition.Steps.Single(step => step.Key == stepKey).RoleAssignments.Add(new() {
                    RoleKey = role, ResponsibilityKind = "Responsible", IsRequired = true
                });
            }
        }
        if (command.CommandKind == ProcessDefinitionCanvasCommandKind.AddArtifactExpectation) {
            foreach (var node in added.Where(node => node.Kind == ProcessDefinitionCanvasNodeKind.Artifact)) {
                definition.Steps.Single(step => step.Key == node.StepKey!.Value.Value).ArtifactExpectations.Add(new() {
                    Key = node.ArtifactKey!, Title = node.Title, ArtifactKind = "Artifact", IsRequired = true
                });
            }
        }
        if (command.CommandKind == ProcessDefinitionCanvasCommandKind.AddSubprocessBoundary) {
            foreach (var node in added.Where(node => node.Kind == ProcessDefinitionCanvasNodeKind.SubprocessBoundary)) {
                definition.Steps.Single(step => step.Key == node.StepKey!.Value.Value).StepKind = nameof(ProcessDefinitionStepKind.Subprocess);
            }
        }
        var placements = new List<ProcessAuthoringReferencePlacement>();
        foreach (var node in after.Nodes) {
            var previous = content.References.FirstOrDefault(item => item.Key == node.NodeKey.Value);
            var isClone = previous?.IsClone == true || !knownNodes.Contains(node.NodeKey) && command.CommandKind is
                ProcessDefinitionCanvasCommandKind.CloneArtifactReference or ProcessDefinitionCanvasCommandKind.CloneRoleReference;
            var sourceStep = node.StepKey?.Value ?? previous?.StepKey;
            if (isClone && sourceStep is null) {
                sourceStep = before.Nodes.FirstOrDefault(item => item.NodeKey == command.SelectedNodeKey)?.StepKey?.Value;
            }
            placements.Add(new(node.NodeKey.Value, node.Kind, ProcessAuthoringCanvasLayout.SemanticKey(node), sourceStep, node.X, node.Y, isClone));
            var step = definition.Steps.FirstOrDefault(item => item.Key == node.StepKey?.Value);
            if (node.Kind == ProcessDefinitionCanvasNodeKind.Step && step is not null) {
                step.CanvasX = node.X;
                step.CanvasY = node.Y;
            } else if (node.Kind == ProcessDefinitionCanvasNodeKind.BranchRouter && step is not null) {
                step.BranchCanvasX = node.X;
                step.BranchCanvasY = node.Y;
            }
        }
        return content with { References = placements };
    }

    internal static string UniqueKey(IEnumerable<string> existing, string preferred) {
        var keys = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = preferred;
        for (var suffix = 2; keys.Contains(result); suffix++) {
            result = preferred + "-" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        return result;
    }
}
