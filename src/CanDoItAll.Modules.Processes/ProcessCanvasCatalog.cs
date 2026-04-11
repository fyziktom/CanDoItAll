using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Processes;

public static class ProcessCanvasCatalog
{
    public static class NodeKinds
    {
        public const string DefinitionStep = "process-step";
        public const string DefinitionBranchRouter = "process-branch-router";
        public const string DefinitionRole = "process-role";
        public const string RuntimeStep = "process-run-step";
        public const string RuntimeBranchRouter = "process-run-branch-router";
    }

    public static class NodePrefixes
    {
        public const string DefinitionStep = "step:";
        public const string DefinitionBranchRouter = "branch:";
        public const string DefinitionRole = "role:";
        public const string RuntimeStep = "run-step:";
        public const string RuntimeBranchRouter = "run-branch:";
    }

    public static class DefinitionPorts
    {
        public const string StepStructuralInput = "step:inputs";
        public const string StepStructuralOutput = "step:next";
        public const string StepDecisionAuthorityInput = "step:decision-authority";
        public const string StepArtifactInputs = "step:artifact-inputs";
        public const string StepArtifactInputPrefix = "step:artifact-input:";
        public const string StepArtifactOutputPrefix = "step:artifact-output:";
        public const string BranchStepInput = "branch:step-input";
        public const string BranchDecisionRoleInput = "branch:decision-role";
        public const string BranchOutcomeOutputPrefix = "route:";
        public const string RoleDecisionAuthorityOutput = "role:decision-authority";

        public static string GetRoleResponsibilityOutputPortId(ProcessResponsibilityKind responsibilityKind)
        {
            return responsibilityKind switch
            {
                ProcessResponsibilityKind.Responsible => "role:responsible",
                ProcessResponsibilityKind.Reviewer => "role:reviewer",
                ProcessResponsibilityKind.Approver => "role:approver",
                ProcessResponsibilityKind.Backup => "role:backup",
                _ => throw new ArgumentOutOfRangeException(nameof(responsibilityKind), responsibilityKind, null)
            };
        }

        public static string GetResponsibilityLabel(ProcessResponsibilityKind responsibilityKind)
        {
            return responsibilityKind switch
            {
                ProcessResponsibilityKind.Responsible => "Responsible",
                ProcessResponsibilityKind.Reviewer => "Reviewer",
                ProcessResponsibilityKind.Approver => "Approver",
                ProcessResponsibilityKind.Backup => "Backup",
                _ => throw new ArgumentOutOfRangeException(nameof(responsibilityKind), responsibilityKind, null)
            };
        }

        public static string GetStepResponsibilityInputPortId(ProcessResponsibilityKind responsibilityKind)
        {
            return responsibilityKind switch
            {
                ProcessResponsibilityKind.Responsible => "step:responsible",
                ProcessResponsibilityKind.Reviewer => "step:reviewer",
                ProcessResponsibilityKind.Approver => "step:approver",
                ProcessResponsibilityKind.Backup => "step:backup",
                _ => throw new ArgumentOutOfRangeException(nameof(responsibilityKind), responsibilityKind, null)
            };
        }

        public static string BuildBranchOutcomeOutputPortId(ProcessStepBranchOutcomeEditorModel outcome)
        {
            ArgumentNullException.ThrowIfNull(outcome);

            return BuildDynamicPortId(BranchOutcomeOutputPrefix, outcome.Id, outcome.Key, outcome.Title, "outcome");
        }

        public static string BuildStepArtifactOutputPortId(ProcessArtifactExpectationEditorModel artifact)
        {
            ArgumentNullException.ThrowIfNull(artifact);

            return BuildDynamicPortId(StepArtifactOutputPrefix, artifact.Id, string.Empty, artifact.Title, "artifact");
        }

        public static string BuildStepArtifactInputPortId(ProcessArtifactExpectationEditorModel artifact)
        {
            ArgumentNullException.ThrowIfNull(artifact);

            return BuildDynamicPortId(StepArtifactInputPrefix, artifact.Id, string.Empty, artifact.Title, "artifact");
        }

        public static bool TryGetRoleResponsibilityKind(string? portId, out ProcessResponsibilityKind responsibilityKind)
        {
            responsibilityKind = default;
            if (string.IsNullOrWhiteSpace(portId))
            {
                return false;
            }

            foreach (var candidate in OrderedResponsibilities)
            {
                if (string.Equals(portId, GetRoleResponsibilityOutputPortId(candidate), StringComparison.Ordinal))
                {
                    responsibilityKind = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetStepResponsibilityKind(string? portId, out ProcessResponsibilityKind responsibilityKind)
        {
            responsibilityKind = default;
            if (string.IsNullOrWhiteSpace(portId))
            {
                return false;
            }

            foreach (var candidate in OrderedResponsibilities)
            {
                if (string.Equals(portId, GetStepResponsibilityInputPortId(candidate), StringComparison.Ordinal))
                {
                    responsibilityKind = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool IsStepStructuralInputPortId(string? portId)
        {
            return string.Equals(portId, StepStructuralInput, StringComparison.Ordinal) ||
                   CanvasWorkbenchAnchorPorts.IsInputPortId(portId);
        }

        public static bool IsStepStructuralOutputPortId(string? portId)
        {
            return string.Equals(portId, StepStructuralOutput, StringComparison.Ordinal) ||
                   CanvasWorkbenchAnchorPorts.IsOutputPortId(portId);
        }
    }

    public static class RuntimePorts
    {
        public const string StepStructuralInput = "run-step:inputs";
        public const string StepStructuralOutput = "run-step:next";
        public const string StepDecisionAuthorityInput = "run-step:decision-authority";
        public const string StepArtifactInputs = "run-step:artifact-inputs";
        public const string StepArtifactOutputPrefix = "run-step:artifact-output:";
        public const string BranchStepInput = "run-branch:step-input";
        public const string BranchOutcomeOutputPrefix = "run-route:";

        public static string GetStepResponsibilityInputPortId(ProcessResponsibilityKind responsibilityKind)
        {
            return responsibilityKind switch
            {
                ProcessResponsibilityKind.Responsible => "run-step:responsible",
                ProcessResponsibilityKind.Reviewer => "run-step:reviewer",
                ProcessResponsibilityKind.Approver => "run-step:approver",
                ProcessResponsibilityKind.Backup => "run-step:backup",
                _ => throw new ArgumentOutOfRangeException(nameof(responsibilityKind), responsibilityKind, null)
            };
        }

        public static string BuildBranchOutcomeOutputPortId(ProcessStepBranchOutcomeOptionViewModel outcome)
        {
            ArgumentNullException.ThrowIfNull(outcome);

            return $"{BranchOutcomeOutputPrefix}{outcome.Id:D}";
        }
    }

    public static class ConnectionCategories
    {
        public const string Structural = "process-structural";
        public const string DecisionAuthority = "process-decision-authority";
        public const string ResponsibilityResponsible = "process-responsible";
        public const string ResponsibilityReviewer = "process-reviewer";
        public const string ResponsibilityApprover = "process-approver";
        public const string ResponsibilityBackup = "process-backup";
        public const string Artifact = "process-artifact";
        public const string BranchRoute = "process-branch-route";
        public const string BranchDefault = "process-branch-default";
        public const string BranchError = "process-branch-error";
    }

    public enum PortFamily
    {
        RoleResponsibilityOutput,
        RoleDecisionAuthorityOutput,
        StepStructuralInput,
        StepStructuralOutput,
        StepResponsibilityInput,
        StepDecisionAuthorityInput,
        StepArtifactOutput,
        StepArtifactInput,
        BranchStepInput,
        BranchDecisionRoleInput,
        BranchOutcomeOutput,
        BranchDefaultOutput,
        BranchErrorOutput,
        RunStepStructuralInput,
        RunStepStructuralOutput,
        RunStepResponsibilityInput,
        RunStepDecisionAuthorityInput,
        RunStepArtifactInput,
        RunStepArtifactOutput,
        RunBranchStepInput,
        RunBranchOutcomeOutput
    }

    public enum PortDirection
    {
        Input,
        Output
    }

    public enum PortCardinality
    {
        SingleToSingle,
        SingleToMany,
        ManyToSingle,
        ManyToMany
    }

    public enum CanonicalStatus
    {
        CanonicalToday,
        NeedsExtension
    }

    public enum RouterRelevance
    {
        Optional,
        Primary
    }

    public readonly record struct PortFamilyMetadata(
        PortDirection Direction,
        PortCardinality Cardinality,
        CanonicalStatus CanonicalStatus,
        bool IsDynamic = false);

    public readonly record struct ConnectionVisual(
        string CategoryKey,
        string AccentColor);

    public readonly record struct StepKindProfile(
        bool AllowsStructuralInput,
        bool AllowsStructuralOutput,
        bool AllowsParticipantInputs,
        bool AllowsDecisionAuthorityInput,
        bool AllowsArtifactOutputs,
        bool AllowsArtifactInputs,
        RouterRelevance RouterRelevance);

    public static IReadOnlyList<string> DefinitionNodeKinds { get; } =
    [
        NodeKinds.DefinitionStep,
        NodeKinds.DefinitionBranchRouter,
        NodeKinds.DefinitionRole
    ];

    public static IReadOnlyList<string> RuntimeNodeKinds { get; } =
    [
        NodeKinds.RuntimeStep,
        NodeKinds.RuntimeBranchRouter
    ];

    public static IReadOnlyList<ProcessResponsibilityKind> OrderedResponsibilities { get; } =
    [
        ProcessResponsibilityKind.Responsible,
        ProcessResponsibilityKind.Reviewer,
        ProcessResponsibilityKind.Approver,
        ProcessResponsibilityKind.Backup
    ];

    public static StepKindProfile GetStepKindProfile(ProcessStepKind stepKind)
    {
        return stepKind switch
        {
            ProcessStepKind.Start => new StepKindProfile(
                AllowsStructuralInput: false,
                AllowsStructuralOutput: true,
                AllowsParticipantInputs: true,
                AllowsDecisionAuthorityInput: false,
                AllowsArtifactOutputs: true,
                AllowsArtifactInputs: true,
                RouterRelevance.Optional),
            ProcessStepKind.Decision => new StepKindProfile(
                AllowsStructuralInput: true,
                AllowsStructuralOutput: true,
                AllowsParticipantInputs: true,
                AllowsDecisionAuthorityInput: true,
                AllowsArtifactOutputs: true,
                AllowsArtifactInputs: true,
                RouterRelevance.Primary),
            ProcessStepKind.Approval => new StepKindProfile(
                AllowsStructuralInput: true,
                AllowsStructuralOutput: true,
                AllowsParticipantInputs: true,
                AllowsDecisionAuthorityInput: true,
                AllowsArtifactOutputs: true,
                AllowsArtifactInputs: true,
                RouterRelevance.Optional),
            ProcessStepKind.Review => new StepKindProfile(
                AllowsStructuralInput: true,
                AllowsStructuralOutput: true,
                AllowsParticipantInputs: true,
                AllowsDecisionAuthorityInput: true,
                AllowsArtifactOutputs: true,
                AllowsArtifactInputs: true,
                RouterRelevance.Optional),
            ProcessStepKind.End => new StepKindProfile(
                AllowsStructuralInput: true,
                AllowsStructuralOutput: false,
                AllowsParticipantInputs: true,
                AllowsDecisionAuthorityInput: false,
                AllowsArtifactOutputs: true,
                AllowsArtifactInputs: true,
                RouterRelevance.Optional),
            _ => new StepKindProfile(
                AllowsStructuralInput: true,
                AllowsStructuralOutput: true,
                AllowsParticipantInputs: true,
                AllowsDecisionAuthorityInput: false,
                AllowsArtifactOutputs: true,
                AllowsArtifactInputs: true,
                RouterRelevance.Optional)
        };
    }

    public static PortFamilyMetadata GetPortFamilyMetadata(PortFamily family)
    {
        return family switch
        {
            PortFamily.RoleResponsibilityOutput => new PortFamilyMetadata(PortDirection.Output, PortCardinality.ManyToMany, CanonicalStatus.CanonicalToday),
            PortFamily.RoleDecisionAuthorityOutput => new PortFamilyMetadata(PortDirection.Output, PortCardinality.SingleToMany, CanonicalStatus.CanonicalToday),
            PortFamily.StepStructuralInput => new PortFamilyMetadata(PortDirection.Input, PortCardinality.ManyToSingle, CanonicalStatus.CanonicalToday),
            PortFamily.StepStructuralOutput => new PortFamilyMetadata(PortDirection.Output, PortCardinality.SingleToMany, CanonicalStatus.CanonicalToday),
            PortFamily.StepResponsibilityInput => new PortFamilyMetadata(PortDirection.Input, PortCardinality.ManyToSingle, CanonicalStatus.CanonicalToday),
            PortFamily.StepDecisionAuthorityInput => new PortFamilyMetadata(PortDirection.Input, PortCardinality.SingleToSingle, CanonicalStatus.CanonicalToday),
            PortFamily.StepArtifactOutput => new PortFamilyMetadata(PortDirection.Output, PortCardinality.SingleToMany, CanonicalStatus.CanonicalToday, IsDynamic: true),
            PortFamily.StepArtifactInput => new PortFamilyMetadata(PortDirection.Input, PortCardinality.ManyToSingle, CanonicalStatus.CanonicalToday, IsDynamic: true),
            PortFamily.BranchStepInput => new PortFamilyMetadata(PortDirection.Input, PortCardinality.SingleToSingle, CanonicalStatus.CanonicalToday),
            PortFamily.BranchDecisionRoleInput => new PortFamilyMetadata(PortDirection.Input, PortCardinality.SingleToSingle, CanonicalStatus.CanonicalToday),
            PortFamily.BranchOutcomeOutput => new PortFamilyMetadata(PortDirection.Output, PortCardinality.SingleToMany, CanonicalStatus.CanonicalToday, IsDynamic: true),
            PortFamily.BranchDefaultOutput => new PortFamilyMetadata(PortDirection.Output, PortCardinality.SingleToMany, CanonicalStatus.CanonicalToday, IsDynamic: true),
            PortFamily.BranchErrorOutput => new PortFamilyMetadata(PortDirection.Output, PortCardinality.SingleToMany, CanonicalStatus.CanonicalToday, IsDynamic: true),
            PortFamily.RunStepStructuralInput => new PortFamilyMetadata(PortDirection.Input, PortCardinality.ManyToSingle, CanonicalStatus.CanonicalToday),
            PortFamily.RunStepStructuralOutput => new PortFamilyMetadata(PortDirection.Output, PortCardinality.SingleToMany, CanonicalStatus.CanonicalToday),
            PortFamily.RunStepResponsibilityInput => new PortFamilyMetadata(PortDirection.Input, PortCardinality.ManyToSingle, CanonicalStatus.CanonicalToday),
            PortFamily.RunStepDecisionAuthorityInput => new PortFamilyMetadata(PortDirection.Input, PortCardinality.SingleToSingle, CanonicalStatus.CanonicalToday),
            PortFamily.RunStepArtifactInput => new PortFamilyMetadata(PortDirection.Input, PortCardinality.ManyToSingle, CanonicalStatus.CanonicalToday),
            PortFamily.RunStepArtifactOutput => new PortFamilyMetadata(PortDirection.Output, PortCardinality.SingleToMany, CanonicalStatus.CanonicalToday, IsDynamic: true),
            PortFamily.RunBranchStepInput => new PortFamilyMetadata(PortDirection.Input, PortCardinality.SingleToSingle, CanonicalStatus.CanonicalToday),
            PortFamily.RunBranchOutcomeOutput => new PortFamilyMetadata(PortDirection.Output, PortCardinality.SingleToMany, CanonicalStatus.CanonicalToday, IsDynamic: true),
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, null)
        };
    }

    public static PortFamily GetBranchOutcomePortFamily(ProcessStepBranchOutcomeEditorModel outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        if (ProcessCanvasBranching.IsDefaultOutcome(outcome))
        {
            return PortFamily.BranchDefaultOutput;
        }

        if (ProcessCanvasBranching.IsErrorOutcome(outcome))
        {
            return PortFamily.BranchErrorOutput;
        }

        return PortFamily.BranchOutcomeOutput;
    }

    public static PortFamily GetBranchOutcomePortFamily(ProcessStepBranchOutcomeOptionViewModel outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        if (string.Equals(outcome.Title, ProcessCanvasBranching.DefaultRouteTitle, StringComparison.OrdinalIgnoreCase))
        {
            return PortFamily.BranchDefaultOutput;
        }

        if (string.Equals(outcome.Title, ProcessCanvasBranching.ErrorRouteTitle, StringComparison.OrdinalIgnoreCase))
        {
            return PortFamily.BranchErrorOutput;
        }

        return PortFamily.BranchOutcomeOutput;
    }

    public static ConnectionVisual GetConnectionVisual(PortFamily family)
    {
        return family switch
        {
            PortFamily.RoleDecisionAuthorityOutput or
            PortFamily.StepDecisionAuthorityInput or
            PortFamily.BranchDecisionRoleInput or
            PortFamily.RunStepDecisionAuthorityInput
                => new ConnectionVisual(ConnectionCategories.DecisionAuthority, "#8b5cf6"),
            PortFamily.StepStructuralInput or
            PortFamily.StepStructuralOutput or
            PortFamily.BranchStepInput or
            PortFamily.RunStepStructuralInput or
            PortFamily.RunStepStructuralOutput or
            PortFamily.RunBranchStepInput
                => new ConnectionVisual(ConnectionCategories.Structural, "#2563eb"),
            PortFamily.StepArtifactInput or
            PortFamily.StepArtifactOutput or
            PortFamily.RunStepArtifactInput or
            PortFamily.RunStepArtifactOutput
                => new ConnectionVisual(ConnectionCategories.Artifact, "#db2777"),
            PortFamily.BranchDefaultOutput
                => new ConnectionVisual(ConnectionCategories.BranchDefault, "#64748b"),
            PortFamily.BranchErrorOutput
                => new ConnectionVisual(ConnectionCategories.BranchError, "#dc2626"),
            PortFamily.BranchOutcomeOutput or
            PortFamily.RunBranchOutcomeOutput
                => new ConnectionVisual(ConnectionCategories.BranchRoute, "#7c3aed"),
            _ => new ConnectionVisual(ConnectionCategories.Structural, "#2563eb")
        };
    }

    public static ConnectionVisual GetResponsibilityVisual(ProcessResponsibilityKind responsibilityKind)
    {
        return responsibilityKind switch
        {
            ProcessResponsibilityKind.Responsible
                => new ConnectionVisual(ConnectionCategories.ResponsibilityResponsible, "#0ea5e9"),
            ProcessResponsibilityKind.Reviewer
                => new ConnectionVisual(ConnectionCategories.ResponsibilityReviewer, "#6366f1"),
            ProcessResponsibilityKind.Approver
                => new ConnectionVisual(ConnectionCategories.ResponsibilityApprover, "#16a34a"),
            ProcessResponsibilityKind.Backup
                => new ConnectionVisual(ConnectionCategories.ResponsibilityBackup, "#d97706"),
            _ => throw new ArgumentOutOfRangeException(nameof(responsibilityKind), responsibilityKind, null)
        };
    }

    private static string BuildDynamicPortId(string prefix, Guid? id, string key, string title, string fallbackPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        if (id.HasValue && id.Value != Guid.Empty)
        {
            return $"{prefix}{id.Value:D}";
        }

        var source = string.IsNullOrWhiteSpace(key) ? title : key;
        if (string.IsNullOrWhiteSpace(source))
        {
            return $"{prefix}{fallbackPrefix}-{Guid.NewGuid():N}";
        }

        return $"{prefix}{source.Trim().ToLowerInvariant().Replace(' ', '-')}";
    }
}
