using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessCanvasSurfaceFactory
{
    private static string BuildDefinitionNodeId(ProcessStepEditorModel step)
    {
        return ProcessCanvasBranching.BuildDefinitionStepNodeId(step);
    }

    private static string BuildDefinitionNodeId(Guid stepId)
    {
        return $"{ProcessCanvasBranching.DefinitionStepNodePrefix}{stepId:D}";
    }

    private static string BuildDefinitionBranchNodeId(ProcessStepEditorModel step)
    {
        return ProcessCanvasBranching.BuildDefinitionBranchNodeId(step);
    }

    private static string BuildDefinitionRoleNodeId(ProcessRoleEditorModel role)
    {
        return ProcessCanvasBranching.BuildDefinitionRoleNodeId(role);
    }

    private static string BuildRunNodeId(Guid stepRunId)
    {
        return ProcessCanvasBranching.BuildRuntimeStepNodeId(stepRunId);
    }

    private static string BuildRunBranchNodeId(Guid stepRunId)
    {
        return ProcessCanvasBranching.BuildRuntimeBranchNodeId(stepRunId);
    }

    private static List<CanvasWorkbenchPort> BuildDefinitionBranchInputPorts(string decisionRoleTitle)
    {
        var ports = new List<CanvasWorkbenchPort>
        {
            new()
            {
                Id = ProcessCanvasCatalog.DefinitionPorts.BranchStepInput,
                Label = "From step",
                Side = "left",
                Tone = "neutral",
                Kind = "source"
            }
        };
        ports.Add(new CanvasWorkbenchPort
        {
            Id = ProcessCanvasCatalog.DefinitionPorts.BranchDecisionRoleInput,
            Label = string.IsNullOrWhiteSpace(decisionRoleTitle)
                ? "Decision authority"
                : decisionRoleTitle,
            Side = "left",
            Tone = "info",
            Kind = "decision-role",
            IsRequired = !string.IsNullOrWhiteSpace(decisionRoleTitle)
        });

        return ports;
    }

    private static CanvasWorkbenchPort BuildDefinitionBranchOutputPort(ProcessStepBranchOutcomeEditorModel outcome)
    {
        return new CanvasWorkbenchPort
        {
            Id = ProcessCanvasBranching.BuildOutcomePortId(outcome),
            Label = string.IsNullOrWhiteSpace(outcome.Title) ? "Untitled route" : outcome.Title,
            Side = "right",
            Tone = ProcessCanvasBranching.IsErrorOutcome(outcome)
                ? "danger"
                : ProcessCanvasBranching.IsDefaultOutcome(outcome)
                    ? "neutral"
                    : "accent",
            Kind = outcome.Key
        };
    }

    private static CanvasWorkbenchPort BuildRunBranchOutputPort(ProcessStepBranchOutcomeOptionViewModel outcome)
    {
        var tone = string.Equals(outcome.Title, ProcessCanvasBranching.ErrorRouteTitle, StringComparison.OrdinalIgnoreCase)
            ? "danger"
            : string.Equals(outcome.Title, ProcessCanvasBranching.DefaultRouteTitle, StringComparison.OrdinalIgnoreCase)
                ? "neutral"
                : "accent";

        return new CanvasWorkbenchPort
        {
            Id = ProcessCanvasBranching.BuildOutcomePortId(outcome),
            Label = string.IsNullOrWhiteSpace(outcome.Title) ? "Untitled route" : outcome.Title,
            Side = "right",
            Tone = tone,
            Kind = "route"
        };
    }

    private static List<CanvasWorkbenchPort> BuildDefinitionStepInputPorts(
        ProcessStepEditorModel step,
        IReadOnlyDictionary<Guid, ProcessRoleEditorModel> rolesById,
        ProcessCanvasCatalog.StepKindProfile profile)
    {
        var ports = new List<CanvasWorkbenchPort>();
        if (profile.AllowsStructuralInput)
        {
            ports.Add(new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.DefinitionPorts.StepStructuralInput,
                Label = "Inputs",
                Side = "left",
                Tone = ProcessCanvasBranching.GetOrderedDependencies(step).Count == 0 ? "neutral" : "info",
                Kind = "structural-input",
                IsRequired = step.StepKind != ProcessStepKind.Start
            });
        }

        if (profile.AllowsDecisionAuthorityInput && !ProcessCanvasBranching.ShouldRenderBranchRouter(step))
        {
            ports.Add(new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.DefinitionPorts.StepDecisionAuthorityInput,
                Label = ResolveDecisionAuthorityLabel(step.DecisionRoleRequirementId, rolesById),
                Side = "left",
                Tone = "info",
                Kind = "decision-role",
                IsRequired = step.DecisionRoleRequirementId.HasValue
            });
        }

        if (profile.AllowsParticipantInputs)
        {
            foreach (var responsibilityKind in ProcessCanvasCatalog.OrderedResponsibilities)
            {
                var hasBinding = step.RoleAssignments.Any(assignment => assignment.ResponsibilityKind == responsibilityKind);
                ports.Add(new CanvasWorkbenchPort
                {
                    Id = ProcessCanvasCatalog.DefinitionPorts.GetStepResponsibilityInputPortId(responsibilityKind),
                    Label = ProcessCanvasCatalog.DefinitionPorts.GetResponsibilityLabel(responsibilityKind),
                    Side = "left",
                    Tone = hasBinding ? "info" : "neutral",
                    Kind = responsibilityKind.ToString().ToLowerInvariant(),
                    IsRequired = step.RoleAssignments.Any(assignment => assignment.ResponsibilityKind == responsibilityKind && assignment.IsRequired)
                });
            }
        }

        if (profile.AllowsArtifactInputs)
        {
            ports.Add(new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.DefinitionPorts.StepArtifactInputs,
                Label = "Artifacts",
                Side = "left",
                Tone = step.ArtifactInputs.Count == 0 ? "neutral" : "accent",
                Kind = "artifact-input",
                IsRequired = step.ArtifactInputs.Count > 0
            });
        }

        return ports;
    }

    private static List<CanvasWorkbenchPort> BuildDefinitionStepOutputPorts(
        ProcessStepEditorModel step,
        ProcessCanvasCatalog.StepKindProfile profile)
    {
        var ports = new List<CanvasWorkbenchPort>();
        if (profile.AllowsStructuralOutput)
        {
            ports.Add(new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.DefinitionPorts.StepStructuralOutput,
                Label = ProcessCanvasBranching.ShouldRenderBranchRouter(step) ? "Route" : "Next",
                Side = "right",
                Tone = ProcessCanvasBranching.ShouldRenderBranchRouter(step) ? "accent" : "neutral",
                Kind = "structural-output"
            });
        }

        if (profile.AllowsArtifactOutputs)
        {
            ports.AddRange(step.ArtifactExpectations
                .Where(artifact => !string.IsNullOrWhiteSpace(artifact.Title))
                .Select(artifact => new CanvasWorkbenchPort
                {
                    Id = ProcessCanvasCatalog.DefinitionPorts.BuildStepArtifactOutputPortId(artifact),
                    Label = artifact.Title,
                    Side = "right",
                    Tone = artifact.IsRequired ? "info" : "neutral",
                    Kind = "artifact-output",
                    IsRequired = artifact.IsRequired
                }));
        }

        return ports;
    }

    private static List<CanvasWorkbenchPort> BuildDefinitionRoleOutputPorts(
        ProcessRoleEditorModel role,
        ProcessDefinitionEditorModel editor)
    {
        var ports = new List<CanvasWorkbenchPort>();
        foreach (var responsibilityKind in ProcessCanvasCatalog.OrderedResponsibilities)
        {
            var bindingCount = editor.Steps.Sum(step => step.RoleAssignments.Count(assignment =>
                assignment.RoleRequirementId == role.Id &&
                assignment.ResponsibilityKind == responsibilityKind));
            ports.Add(new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.DefinitionPorts.GetRoleResponsibilityOutputPortId(responsibilityKind),
                Label = ProcessCanvasCatalog.DefinitionPorts.GetResponsibilityLabel(responsibilityKind),
                Side = "right",
                Tone = bindingCount == 0 ? "neutral" : "info",
                Kind = responsibilityKind.ToString().ToLowerInvariant()
            });
        }

        var outboundMessagingCount = editor.MessagingPolicies.Count(item => item.SourceRoleRequirementId == role.Id);
        ports.Add(new CanvasWorkbenchPort
        {
            Id = ProcessCanvasCatalog.DefinitionPorts.RoleMessagingOutput,
            Label = outboundMessagingCount == 0 ? "Messaging" : $"Messaging ({outboundMessagingCount})",
            Side = "right",
            Tone = outboundMessagingCount == 0 ? "neutral" : "accent",
            Kind = "messaging"
        });

        ports.Add(new CanvasWorkbenchPort
        {
            Id = ProcessCanvasCatalog.DefinitionPorts.RoleDecisionAuthorityOutput,
            Label = "Decision authority",
            Side = "right",
            Tone = "info",
            Kind = "decision"
        });

        return ports;
    }

    private static List<CanvasWorkbenchPort> BuildDefinitionRoleInstanceOutputPorts(DefinitionRoleNodePlan plan)
    {
        var ports = new List<CanvasWorkbenchPort>();
        foreach (var responsibilityKind in plan.Responsibilities)
        {
            ports.Add(new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.DefinitionPorts.GetRoleResponsibilityOutputPortId(responsibilityKind),
                Label = ProcessCanvasCatalog.DefinitionPorts.GetResponsibilityLabel(responsibilityKind),
                Side = "right",
                Tone = "info",
                Kind = responsibilityKind.ToString().ToLowerInvariant(),
                IsRequired = true
            });
        }

        if (plan.IsDecisionAuthority)
        {
            ports.Add(new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.DefinitionPorts.RoleDecisionAuthorityOutput,
                Label = "Decision authority",
                Side = "right",
                Tone = "info",
                Kind = "decision",
                IsRequired = true
            });
        }

        return ports;
    }

    private static List<CanvasWorkbenchPort> BuildDefinitionRoleInputPorts(
        ProcessRoleEditorModel role,
        ProcessDefinitionEditorModel editor)
    {
        var inboundMessagingCount = editor.MessagingPolicies.Count(item => item.TargetRoleRequirementId == role.Id);
        return
        [
            new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.DefinitionPorts.RoleMessagingInput,
                Label = inboundMessagingCount == 0 ? "Incoming messages" : $"Incoming ({inboundMessagingCount})",
                Side = "left",
                Tone = inboundMessagingCount == 0 ? "neutral" : "accent",
                Kind = "messaging"
            }
        ];
    }

    private static List<CanvasWorkbenchPort> BuildDefinitionArtifactInputPorts()
    {
        return
        [
            new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.DefinitionPorts.ArtifactSourceInput,
                Label = "Produced by",
                Side = "left",
                Tone = "accent",
                Kind = "artifact-source",
                IsRequired = true
            }
        ];
    }

    private static List<CanvasWorkbenchPort> BuildDefinitionArtifactOutputPorts()
    {
        return
        [
            new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.DefinitionPorts.ArtifactUsageOutput,
                Label = "Use artifact",
                Side = "right",
                Tone = "accent",
                Kind = "artifact-output",
                IsRequired = true
            }
        ];
    }

    private static List<CanvasWorkbenchPort> BuildRunStepInputPorts(
        ProcessStepRunViewModel stepRun,
        ProcessCanvasCatalog.StepKindProfile profile)
    {
        var ports = new List<CanvasWorkbenchPort>();
        if (profile.AllowsStructuralInput)
        {
            ports.Add(new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.RuntimePorts.StepStructuralInput,
                Label = "Inputs",
                Side = "left",
                Tone = stepRun.Dependencies.Count == 0 ? "neutral" : "info",
                Kind = "structural-input",
                IsRequired = stepRun.StepKind != ProcessStepKind.Start
            });
        }

        if (profile.AllowsDecisionAuthorityInput &&
            !ProcessCanvasBranching.ShouldRenderBranchRouter(stepRun) &&
            (stepRun.DecisionRoleRequirementId.HasValue || !string.IsNullOrWhiteSpace(stepRun.DecisionRoleTitle)))
        {
            ports.Add(new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.RuntimePorts.StepDecisionAuthorityInput,
                Label = string.IsNullOrWhiteSpace(stepRun.DecisionRoleTitle)
                    ? "Decision authority"
                    : stepRun.DecisionRoleTitle,
                Side = "left",
                Tone = "info",
                Kind = "decision-role",
                IsRequired = stepRun.DecisionRoleRequirementId.HasValue
            });
        }

        if (profile.AllowsParticipantInputs)
        {
            foreach (var responsibilityPort in stepRun.ResponsibilityPorts)
            {
                ports.Add(new CanvasWorkbenchPort
                {
                    Id = ProcessCanvasCatalog.RuntimePorts.GetStepResponsibilityInputPortId(responsibilityPort.ResponsibilityKind),
                    Label = responsibilityPort.AssignmentCount > 1
                        ? $"{ProcessCanvasCatalog.DefinitionPorts.GetResponsibilityLabel(responsibilityPort.ResponsibilityKind)} ({responsibilityPort.AssignmentCount})"
                        : ProcessCanvasCatalog.DefinitionPorts.GetResponsibilityLabel(responsibilityPort.ResponsibilityKind),
                    Side = "left",
                    Tone = "info",
                    Kind = responsibilityPort.ResponsibilityKind.ToString().ToLowerInvariant(),
                    IsRequired = responsibilityPort.IsRequired
                });
            }
        }

        if (profile.AllowsArtifactInputs && stepRun.ArtifactInputCount > 0)
        {
            ports.Add(new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.RuntimePorts.StepArtifactInputs,
                Label = stepRun.ArtifactInputCount == 1
                    ? "Artifact"
                    : $"Artifacts ({stepRun.ArtifactInputCount})",
                Side = "left",
                Tone = "accent",
                Kind = "artifact-input",
                IsRequired = true
            });
        }

        return ports;
    }

    private static List<CanvasWorkbenchPort> BuildRunStepOutputPorts(
        ProcessStepRunViewModel stepRun,
        ProcessCanvasCatalog.StepKindProfile profile)
    {
        var ports = new List<CanvasWorkbenchPort>();
        if (profile.AllowsStructuralOutput)
        {
            ports.Add(new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.RuntimePorts.StepStructuralOutput,
                Label = ProcessCanvasBranching.ShouldRenderBranchRouter(stepRun) ? "Route" : "Next",
                Side = "right",
                Tone = ProcessCanvasBranching.ShouldRenderBranchRouter(stepRun) ? "accent" : "neutral",
                Kind = "structural-output"
            });
        }

        if (profile.AllowsArtifactOutputs)
        {
            ports.AddRange(stepRun.ArtifactOutputs
                .Where(artifact => !string.IsNullOrWhiteSpace(artifact.Title))
                .Select(artifact => new CanvasWorkbenchPort
                {
                    Id = $"{ProcessCanvasCatalog.RuntimePorts.StepArtifactOutputPrefix}{artifact.ArtifactExpectationId:D}",
                    Label = artifact.Title,
                    Side = "right",
                    Tone = artifact.IsRequired ? "info" : "neutral",
                    Kind = "artifact-output",
                    IsRequired = artifact.IsRequired
                }));
        }

        return ports;
    }

    private static string ResolveDecisionAuthorityLabel(
        Guid? roleId,
        IReadOnlyDictionary<Guid, ProcessRoleEditorModel> rolesById)
    {
        if (!roleId.HasValue || !rolesById.TryGetValue(roleId.Value, out var role))
        {
            return "Decision authority";
        }

        return string.IsNullOrWhiteSpace(role.DisplayName)
            ? "Decision authority"
            : role.DisplayName;
    }
}
