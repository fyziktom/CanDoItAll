using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessCanvasSurfaceFactory
{
    private static List<CanvasWorkbenchLink> BuildDefinitionLinks(
        IReadOnlyList<ProcessStepEditorModel> steps,
        IReadOnlyDictionary<Guid, ProcessRoleEditorModel> rolesById,
        IReadOnlyList<ProcessRoleMessagingPolicyEditorModel> messagingPolicies,
        IReadOnlyDictionary<DefinitionRoleNodeKey, string> roleNodeIds)
    {
        var stepsById = steps
            .Where(step => step.Id.HasValue)
            .ToDictionary(step => step.Id!.Value);
        var artifactOwnersById = steps
            .SelectMany(step => step.ArtifactExpectations
                .Where(artifact => artifact.Id is { } artifactId && artifactId != Guid.Empty)
                .Select(artifact => new KeyValuePair<Guid, (ProcessStepEditorModel Step, ProcessArtifactExpectationEditorModel Artifact)>(
                    artifact.Id!.Value,
                    (step, artifact))))
            .ToDictionary(item => item.Key, item => item.Value);
        var links = new List<CanvasWorkbenchLink>();

        foreach (var artifactOwner in artifactOwnersById.Values)
        {
            links.Add(new CanvasWorkbenchLink
            {
                SourceId = BuildDefinitionNodeId(artifactOwner.Step),
                SourcePortId = ProcessCanvasCatalog.DefinitionPorts.BuildStepArtifactOutputPortId(artifactOwner.Artifact),
                TargetId = ProcessCanvasBranching.BuildDefinitionArtifactNodeId(artifactOwner.Artifact),
                TargetPortId = ProcessCanvasCatalog.DefinitionPorts.ArtifactSourceInput,
                Kind = "artifact",
                IsUserAuthored = true
            });
        }

        foreach (var step in steps.Where(ProcessCanvasBranching.ShouldRenderBranchRouter))
        {
            links.Add(new CanvasWorkbenchLink
            {
                SourceId = BuildDefinitionNodeId(step),
                SourcePortId = ProcessCanvasCatalog.DefinitionPorts.StepStructuralOutput,
                TargetId = BuildDefinitionBranchNodeId(step),
                TargetPortId = ProcessCanvasCatalog.DefinitionPorts.BranchStepInput,
                Kind = "flow",
                IsUserAuthored = true
            });

            if (step.DecisionRoleRequirementId.HasValue &&
                rolesById.ContainsKey(step.DecisionRoleRequirementId.Value))
            {
                links.Add(new CanvasWorkbenchLink
                {
                    SourceId = ResolveDefinitionRoleNodeId(
                        rolesById[step.DecisionRoleRequirementId.Value],
                        step,
                        roleNodeIds),
                    SourcePortId = ProcessCanvasCatalog.DefinitionPorts.RoleDecisionAuthorityOutput,
                    TargetId = BuildDefinitionBranchNodeId(step),
                    TargetPortId = ProcessCanvasCatalog.DefinitionPorts.BranchDecisionRoleInput,
                    Kind = "decision-role",
                    IsUserAuthored = true
                });
            }
        }

        foreach (var step in steps.Where(step => !ProcessCanvasBranching.ShouldRenderBranchRouter(step) && step.DecisionRoleRequirementId.HasValue))
        {
            if (!rolesById.TryGetValue(step.DecisionRoleRequirementId!.Value, out var decisionRole))
            {
                continue;
            }

            links.Add(new CanvasWorkbenchLink
            {
                SourceId = ResolveDefinitionRoleNodeId(decisionRole, step, roleNodeIds),
                SourcePortId = ProcessCanvasCatalog.DefinitionPorts.RoleDecisionAuthorityOutput,
                TargetId = BuildDefinitionNodeId(step),
                TargetPortId = ProcessCanvasCatalog.DefinitionPorts.StepDecisionAuthorityInput,
                Kind = "decision-role",
                IsUserAuthored = true
            });
        }

        foreach (var step in steps)
        {
            foreach (var assignment in step.RoleAssignments
                         .Where(assignment => assignment.RoleRequirementId.HasValue &&
                             rolesById.ContainsKey(assignment.RoleRequirementId.Value)))
            {
                var role = rolesById[assignment.RoleRequirementId!.Value];
                links.Add(new CanvasWorkbenchLink
                {
                    SourceId = ResolveDefinitionRoleNodeId(role, step, roleNodeIds),
                    SourcePortId = ProcessCanvasCatalog.DefinitionPorts.GetRoleResponsibilityOutputPortId(assignment.ResponsibilityKind),
                    TargetId = BuildDefinitionNodeId(step),
                    TargetPortId = ProcessCanvasCatalog.DefinitionPorts.GetStepResponsibilityInputPortId(assignment.ResponsibilityKind),
                    Kind = "role-binding",
                    IsUserAuthored = true
                });
            }
        }

        foreach (var step in steps)
        {
            foreach (var dependency in ProcessCanvasBranching.GetOrderedDependencies(step)
                         .Where(dependency => dependency.DependsOnStepId.HasValue &&
                             stepsById.ContainsKey(dependency.DependsOnStepId.Value)))
            {
                var sourceStep = stepsById[dependency.DependsOnStepId!.Value];
                if (ProcessCanvasBranching.ShouldRenderBranchRouter(sourceStep))
                {
                    links.Add(new CanvasWorkbenchLink
                    {
                        SourceId = BuildDefinitionBranchNodeId(sourceStep),
                        SourcePortId = ProcessCanvasBranching.ResolveOutcomePortId(sourceStep, dependency.DependsOnBranchOutcomeId),
                        TargetId = BuildDefinitionNodeId(step),
                        TargetPortId = ProcessCanvasCatalog.DefinitionPorts.StepStructuralInput,
                        Kind = "flow",
                        IsUserAuthored = true
                    });
                    continue;
                }

                links.Add(new CanvasWorkbenchLink
                {
                    SourceId = BuildDefinitionNodeId(sourceStep),
                    SourcePortId = ProcessCanvasCatalog.DefinitionPorts.StepStructuralOutput,
                    TargetId = BuildDefinitionNodeId(step),
                    TargetPortId = ProcessCanvasCatalog.DefinitionPorts.StepStructuralInput,
                    Kind = "flow",
                    IsUserAuthored = true
                });
            }

            foreach (var artifactInput in step.ArtifactInputs.Where(item => item.ArtifactExpectationId.HasValue))
            {
                if (!artifactOwnersById.TryGetValue(artifactInput.ArtifactExpectationId!.Value, out var artifactOwner))
                {
                    continue;
                }

                links.Add(new CanvasWorkbenchLink
                {
                    SourceId = ProcessCanvasBranching.BuildDefinitionArtifactCloneNodeId(artifactOwner.Artifact, artifactInput, step),
                    SourcePortId = ProcessCanvasCatalog.DefinitionPorts.ArtifactUsageOutput,
                    TargetId = BuildDefinitionNodeId(step),
                    TargetPortId = ProcessCanvasCatalog.DefinitionPorts.StepArtifactInputs,
                    Kind = "artifact",
                    IsUserAuthored = true
                });
            }
        }

        foreach (var messagingPolicy in messagingPolicies
                     .Where(item => item.SourceRoleRequirementId.HasValue &&
                         item.TargetRoleRequirementId.HasValue &&
                         rolesById.ContainsKey(item.SourceRoleRequirementId.Value) &&
                         rolesById.ContainsKey(item.TargetRoleRequirementId.Value)))
        {
            var sourceRole = rolesById[messagingPolicy.SourceRoleRequirementId!.Value];
            var targetRole = rolesById[messagingPolicy.TargetRoleRequirementId!.Value];
            links.Add(new CanvasWorkbenchLink
            {
                SourceId = BuildDefinitionRoleNodeId(sourceRole),
                SourcePortId = ProcessCanvasCatalog.DefinitionPorts.RoleMessagingOutput,
                TargetId = BuildDefinitionRoleNodeId(targetRole),
                TargetPortId = ProcessCanvasCatalog.DefinitionPorts.RoleMessagingInput,
                Kind = "messaging",
                IsUserAuthored = true
            });
        }

        return links;
    }

    private static string ResolveDefinitionRoleNodeId(
        ProcessRoleEditorModel role,
        ProcessStepEditorModel? relatedStep,
        IReadOnlyDictionary<DefinitionRoleNodeKey, string> roleNodeIds)
    {
        if (role.Id.HasValue && relatedStep?.Id.HasValue == true)
        {
            var instanceKey = new DefinitionRoleNodeKey(role.Id.Value, relatedStep.Id.Value);
            if (roleNodeIds.TryGetValue(instanceKey, out var instanceNodeId))
            {
                return instanceNodeId;
            }
        }

        if (role.Id.HasValue)
        {
            var contractKey = new DefinitionRoleNodeKey(role.Id.Value, null);
            if (roleNodeIds.TryGetValue(contractKey, out var contractNodeId))
            {
                return contractNodeId;
            }
        }

        return BuildDefinitionRoleNodeId(role);
    }

    private static List<CanvasWorkbenchLink> BuildRunLinks(IReadOnlyList<ProcessStepRunViewModel> stepRuns)
    {
        var runsByDefinitionId = stepRuns.ToDictionary(stepRun => stepRun.StepDefinitionId);
        var links = new List<CanvasWorkbenchLink>();

        foreach (var stepRun in stepRuns.Where(ProcessCanvasBranching.ShouldRenderBranchRouter))
        {
            links.Add(new CanvasWorkbenchLink
            {
                SourceId = BuildRunNodeId(stepRun.Id),
                SourcePortId = ProcessCanvasCatalog.RuntimePorts.StepStructuralOutput,
                TargetId = BuildRunBranchNodeId(stepRun.Id),
                TargetPortId = ProcessCanvasCatalog.RuntimePorts.BranchStepInput,
                Kind = "flow",
                IsUserAuthored = false
            });
        }

        foreach (var stepRun in stepRuns)
        {
            foreach (var dependency in stepRun.Dependencies
                         .Where(dependency => runsByDefinitionId.ContainsKey(dependency.DependsOnStepDefinitionId)))
            {
                var sourceStepRun = runsByDefinitionId[dependency.DependsOnStepDefinitionId];
                if (ProcessCanvasBranching.ShouldRenderBranchRouter(sourceStepRun))
                {
                    links.Add(new CanvasWorkbenchLink
                    {
                        SourceId = BuildRunBranchNodeId(sourceStepRun.Id),
                        SourcePortId = ProcessCanvasBranching.ResolveOutcomePortId(sourceStepRun, dependency.DependsOnBranchOutcomeId),
                        TargetId = BuildRunNodeId(stepRun.Id),
                        TargetPortId = ProcessCanvasCatalog.RuntimePorts.StepStructuralInput,
                        Kind = "flow",
                        IsUserAuthored = false
                    });
                    continue;
                }

                links.Add(new CanvasWorkbenchLink
                {
                    SourceId = BuildRunNodeId(sourceStepRun.Id),
                    SourcePortId = ProcessCanvasCatalog.RuntimePorts.StepStructuralOutput,
                    TargetId = BuildRunNodeId(stepRun.Id),
                    TargetPortId = ProcessCanvasCatalog.RuntimePorts.StepStructuralInput,
                    Kind = "flow",
                    IsUserAuthored = false
                });
            }
        }

        return links;
    }
}
