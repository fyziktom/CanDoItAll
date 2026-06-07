namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessDispatchDatabaseRequirementDecision(
    ProcessStepRunStatus TargetStatus,
    bool IsUnsupportedNoOpTarget,
    bool IsTransitionAllowed,
    ProcessStepTransitionRequest? TransitionRequest);

internal sealed record ProcessDispatchMissingUpstreamArtifactMaterializationPlan(
    ProcessMissingUpstreamArtifactMaterializationFacts Facts,
    string BlockReason)
{
    public bool HasMissingInputs => Facts.HasMissingInputs;
}

internal sealed class ProcessDispatchPreExecutionGuardHandler(
    ProcessMissingUpstreamArtifactMaterializationCoordinator materializationCoordinator)
{
    public ProcessDispatchDatabaseRequirementDecision BuildDatabaseRequirementDecision(
        ProcessDispatchPreExecutionRouteFacts routeFacts,
        string failureMessage,
        string automationActor)
    {
        var targetStatus = ProcessDispatchDatabaseRequirementBlocker.ResolveTargetStatus(routeFacts.StepRun.Status);
        var isUnsupportedNoOpTarget = ProcessDispatchDatabaseRequirementBlocker.IsUnsupportedNoOpTarget(
            routeFacts.StepRun.Status,
            targetStatus);
        if (isUnsupportedNoOpTarget)
        {
            return new ProcessDispatchDatabaseRequirementDecision(
                targetStatus,
                IsUnsupportedNoOpTarget: true,
                IsTransitionAllowed: false,
                TransitionRequest: null);
        }

        var isTransitionAllowed = ProcessStepRunTransitions.IsAllowed(routeFacts.StepRun.Status, targetStatus);
        if (!isTransitionAllowed)
        {
            return new ProcessDispatchDatabaseRequirementDecision(
                targetStatus,
                IsUnsupportedNoOpTarget: false,
                IsTransitionAllowed: false,
                TransitionRequest: null);
        }

        return new ProcessDispatchDatabaseRequirementDecision(
            targetStatus,
            IsUnsupportedNoOpTarget: false,
            IsTransitionAllowed: true,
            ProcessDispatchDatabaseRequirementBlocker.BuildTransitionRequest(
                routeFacts.StepRun.Id,
                routeFacts.StepRun.ConcurrencyToken,
                targetStatus,
                failureMessage,
                automationActor));
    }

    public ProcessDispatchMissingUpstreamArtifactMaterializationPlan PlanMissingUpstreamArtifactMaterialization(
        ProcessDispatchPreExecutionRouteFacts routeFacts)
    {
        var facts = ProcessMissingUpstreamArtifactMaterializationFactsResolver.Create(routeFacts);

        return new ProcessDispatchMissingUpstreamArtifactMaterializationPlan(
            facts,
            facts.HasMissingInputs
                ? ProcessMissingUpstreamArtifactMaterializationBlocker.BuildBlockReason(routeFacts, facts)
                : string.Empty);
    }

    public ProcessStepTransitionRequest BuildMissingUpstreamArtifactBlockTransitionRequest(
        ProcessDispatchMissingUpstreamArtifactMaterializationPlan plan,
        Guid stepRunId,
        Guid concurrencyToken,
        string automationActor)
    {
        if (!plan.HasMissingInputs)
        {
            throw new ArgumentException("Missing upstream artifact block transition requires at least one missing input.", nameof(plan));
        }

        return ProcessMissingUpstreamArtifactMaterializationBlocker.BuildBlockTransitionRequest(
            stepRunId,
            concurrencyToken,
            plan.BlockReason,
            automationActor);
    }

    public async Task<bool> RecordAndRequestMissingUpstreamArtifactMaterializationAsync(
        ProcessDispatchPreExecutionRouteFacts routeFacts,
        ProcessDispatchMissingUpstreamArtifactMaterializationPlan plan,
        CancellationToken cancellationToken)
    {
        if (!plan.HasMissingInputs)
        {
            return false;
        }

        return await materializationCoordinator.RecordAndRequestAsync(
            routeFacts,
            plan.Facts,
            plan.BlockReason,
            cancellationToken);
    }
}
