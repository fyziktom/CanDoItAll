namespace CanDoItAll.Processes.Templates;

internal static class ProcessTemplateStepSummaryBuilder
{
    public static ProcessTemplateDefinitionStepAuthoringDefaults Build(
        ProcessTemplateDefinitionDocument definition)
        => new(
            definition.Steps
                .Select(CreateStepSummary)
                .OrderBy(step => step.Order)
                .ThenBy(step => step.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray());

    private static ProcessTemplateDefinitionStepAuthoringSummary CreateStepSummary(
        ProcessTemplateDefinitionStepDocument step)
    {
        var stepKey = NormalizeOptional(step.Key, "step");
        return new ProcessTemplateDefinitionStepAuthoringSummary(
            step.Order,
            stepKey,
            NormalizeOptional(step.Title, stepKey),
            NormalizeOptional(step.Subtitle, string.Empty),
            NormalizeOptional(step.Notes, string.Empty),
            NormalizeOptional(step.StepKind, "Work"),
            step.TargetLeadHours,
            step.AllowsManualSkip,
            step.AllowsSafeRefusal,
            step.RequiresApproval,
            step.RequiresDecisionRecord,
            NormalizeOptional(step.DecisionRoleKey, string.Empty),
            NormalizeOptional(step.InputContractSummary, string.Empty),
            NormalizeOptional(step.OutputContractSummary, string.Empty),
            NormalizeOptional(step.EvidenceContractSummary, string.Empty),
            NormalizeOptional(step.DecisionRightsSummary, string.Empty),
            NormalizeOptional(step.ExceptionPolicySummary, string.Empty),
            step.AllowedOperations
                .Select(operation => NormalizeOptional(operation, string.Empty))
                .Where(operation => !string.IsNullOrWhiteSpace(operation))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            NormalizeOptional(step.OperationTargetScope, string.Empty),
            CanDoItAll.Processes.Contracts.ProcessCapabilityScope.Normalize(step.CapabilityScope),
            NormalizeOptional(step.SubprocessProcessKey, string.Empty),
            NormalizeOptional(step.SubprocessDefinitionSnapshotName, string.Empty),
            step.SubprocessContract,
            step.BranchOutcomes
                .Select((outcome, index) => CreateBranchOutcomeSummary(stepKey, outcome, index))
                .ToArray(),
            step.RoleAssignments
                .Select(assignment => CreateRoleBindingSummary(step, assignment))
                .ToArray(),
            step.ArtifactExpectations
                .Select((artifact, index) => CreateArtifactExpectationSummary(stepKey, artifact, index))
                .ToArray());
    }

    private static ProcessTemplateDefinitionStepBranchOutcomeSummary CreateBranchOutcomeSummary(
        string stepKey,
        ProcessTemplateDefinitionStepBranchOutcomeDocument outcome,
        int index)
        => new(
            NormalizeOptional(outcome.Key, $"{stepKey}-outcome-{index + 1}"),
            NormalizeOptional(outcome.Title, NormalizeOptional(outcome.Key, $"Outcome {index + 1}")),
            NormalizeOptional(outcome.Description, string.Empty),
            NormalizeOptional(outcome.RouteTargetKind, "NextStep"),
            NormalizeOptional(outcome.RouteTargetStepKey, string.Empty),
            NormalizeOptional(outcome.RouteTargetArtifactExpectationKey, string.Empty),
            outcome.IsBackwardRoute,
            outcome.LoopBudgetMaximumRepeats,
            NormalizeOptional(outcome.LoopFingerprintPolicyKey, string.Empty),
            NormalizeOptional(outcome.LoopEscalationTargetKind, "Escalate"));

    private static ProcessTemplateDefinitionStepRoleBindingSummary CreateRoleBindingSummary(
        ProcessTemplateDefinitionStepDocument step,
        ProcessTemplateDefinitionStepRoleAssignmentDocument assignment)
        => new(
            NormalizeOptional(step.Key, "step"),
            NormalizeOptional(step.Title, NormalizeOptional(step.Key, "Step")),
            NormalizeOptional(assignment.RoleKey, string.Empty),
            NormalizeOptional(assignment.RoleKey, string.Empty),
            NormalizeOptional(assignment.ResponsibilityKind, "Responsible"),
            assignment.IsRequired,
            assignment.FallbackOrder,
            NormalizeOptional(assignment.RebindPolicySummary, string.Empty));

    private static ProcessTemplateDefinitionStepArtifactExpectationSummary CreateArtifactExpectationSummary(
        string stepKey,
        ProcessTemplateDefinitionArtifactExpectationDocument artifact,
        int index)
        => new(
            NormalizeOptional(artifact.Key, $"{stepKey}-artifact-{index + 1}"),
            NormalizeOptional(artifact.TemplateKey, string.Empty),
            NormalizeOptional(artifact.Title, NormalizeOptional(artifact.TemplateKey, $"Artifact {index + 1}")),
            NormalizeOptional(artifact.ArtifactKind, "Unspecified"),
            artifact.IsRequired,
            NormalizeOptional(artifact.TrustRequirement, "Unspecified"),
            NormalizeOptional(artifact.SensitivityLevel, "Unspecified"),
            artifact.RetentionDays,
            NormalizeOptional(artifact.WorkflowOutputId, string.Empty),
            NormalizeOptional(artifact.WorkflowOutputName, string.Empty),
            NormalizeOptional(artifact.WorkflowOutputKind, string.Empty),
            artifact.SubprocessChildArtifactExpectationId,
            NormalizeOptional(artifact.SubprocessChildStepKey, string.Empty),
            NormalizeOptional(artifact.SubprocessChildArtifactTitle, string.Empty),
            NormalizeOptional(artifact.AllowedFutureUsageSummary, string.Empty),
            NormalizeOptional(artifact.ValidationRequirementSummary, string.Empty));

    private static string NormalizeOptional(string? value, string defaultValue)
        => string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
}
