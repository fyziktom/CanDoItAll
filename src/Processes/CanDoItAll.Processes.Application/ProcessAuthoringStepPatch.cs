using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

internal static class ProcessAuthoringStepPatch {
    public static void Apply(ProcessTemplateDefinitionStepDocument target, ProcessDefinitionStepDraftProjection patch) {
        target.Title = patch.Basic.Title;
        target.Subtitle = patch.Basic.Subtitle;
        target.Notes = patch.Basic.Notes;
        target.StepKind = patch.Basic.StepKind.ToString();
        target.TargetLeadHours = patch.Basic.TargetLeadHours;
        target.AllowsManualSkip = patch.Basic.AllowsManualSkip;
        target.AllowsSafeRefusal = patch.Basic.AllowsSafeRefusal;
        target.RequiresApproval = patch.Basic.RequiresApproval;
        target.RequiresDecisionRecord = patch.Basic.RequiresDecisionRecord;
        target.DecisionRoleKey = patch.Basic.DecisionRoleKey?.Value ?? string.Empty;
        target.OperationTargetScope = patch.OperationContract.TargetScope.ToString();
        target.AllowedOperations = patch.OperationContract.AllowedOperations.Select(value => value.ToString()).ToList();
        target.InputContractSummary = patch.Contracts.InputContractSummary;
        target.OutputContractSummary = patch.Contracts.OutputContractSummary;
        target.EvidenceContractSummary = patch.Contracts.EvidenceContractSummary;
        target.DecisionRightsSummary = patch.Contracts.DecisionRightsSummary;
        target.ExceptionPolicySummary = patch.Contracts.ExceptionPolicySummary;
        target.RoleAssignments = patch.RoleBindings.Select(binding => new ProcessTemplateDefinitionStepRoleAssignmentDocument {
            RoleKey = binding.RoleKey.Value, ResponsibilityKind = binding.ResponsibilityKind.ToString(), IsRequired = binding.IsRequired,
            FallbackOrder = binding.FallbackOrder, RebindPolicySummary = binding.RebindPolicySummary
        }).ToList();
        var previousOutcomes = target.BranchOutcomes.ToDictionary(outcome => outcome.Key, StringComparer.Ordinal);
        target.BranchOutcomes = patch.BranchOutcomes.Select(outcome => {
            var result = previousOutcomes.GetValueOrDefault(outcome.OutcomeKey.Value) ?? new() { Key = outcome.OutcomeKey.Value };
            result.Title = outcome.Title;
            result.Description = outcome.Description;
            result.RouteTargetKind = outcome.RouteTarget.Kind.ToString();
            result.RouteTargetStepKey = outcome.RouteTarget.StepKey?.Value ?? string.Empty;
            result.RouteTargetArtifactExpectationKey = outcome.RouteTarget.ArtifactExpectationKey?.Value ?? string.Empty;
            result.IsBackwardRoute = outcome.IsBackwardRoute;
            result.LoopBudgetMaximumRepeats = outcome.LoopBudget.MaximumRepeats;
            result.LoopFingerprintPolicyKey = outcome.LoopBudget.FingerprintPolicyKey;
            result.LoopEscalationTargetKind = outcome.LoopBudget.EscalationTargetKind.ToString();
            return result;
        }).ToList();
        var previousArtifacts = target.ArtifactExpectations.ToDictionary(artifact => artifact.Key, StringComparer.Ordinal);
        target.ArtifactExpectations = patch.ArtifactExpectations.Select(artifact => {
            var result = previousArtifacts.GetValueOrDefault(artifact.ArtifactKey.Value) ?? new() { Key = artifact.ArtifactKey.Value };
            ApplyArtifact(result, artifact);
            return result;
        }).ToList();
        target.SubprocessProcessKey = patch.SubprocessMapping.ProcessKey;
        target.SubprocessDefinitionSnapshotName = patch.SubprocessMapping.DefinitionSnapshotName;
    }

    private static void ApplyArtifact(ProcessTemplateDefinitionArtifactExpectationDocument target, ProcessDefinitionArtifactExpectationProjection patch) {
        target.TemplateKey = patch.TemplateKey;
        target.Title = patch.Title;
        target.ArtifactKind = patch.ArtifactKind.ToString();
        target.IsRequired = patch.IsRequired;
        target.TrustRequirement = patch.TrustRequirement.ToString();
        target.SensitivityLevel = patch.SensitivityLevel.ToString();
        target.RetentionDays = patch.RetentionDays;
        target.WorkflowOutputId = patch.WorkflowOutputId;
        target.WorkflowOutputName = patch.WorkflowOutputName;
        target.WorkflowOutputKind = patch.WorkflowOutputKind.ToString();
        target.SubprocessChildArtifactExpectationId = patch.SubprocessChildArtifactExpectationId;
        target.SubprocessChildStepKey = patch.SubprocessChildStepKey;
        target.SubprocessChildArtifactTitle = patch.SubprocessChildArtifactTitle;
        target.AllowedFutureUsageSummary = patch.AllowedFutureUsageSummary;
        target.ValidationRequirementSummary = patch.ValidationRequirementSummary;
    }
}
