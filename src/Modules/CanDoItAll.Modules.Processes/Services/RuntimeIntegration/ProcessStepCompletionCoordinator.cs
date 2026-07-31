using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Logging;

using static CanDoItAll.Modules.Processes.ProcessCompletionIssueResultFactory;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessStepCompletionCoordinator(
    ProcessCompletionIssueResultFactory completionIssueResultFactory,
    ProcessManagedArtifactService managedArtifactService,
    ProcessOutcomeGroundingValidator groundingValidator,
    ProcessCompletionGateEvaluator completionGateEvaluator,
    ProcessExecutionResultConverter resultConverter,
    ILogger<ProcessStepCompletionCoordinator> logger)
{
    internal ProcessManagedArtifactService.ManagedOutcomeArtifactMaterialization Materialize(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        Guid executionRunId,
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts,
        ProcessStepExecutionContract? stepContract = null)
    {
        var normalizedOutput = ProcessOutcomeCitationSanitizer.RemoveNonCitableSourceMetadataFromOutcome(output);
        normalizedOutput = groundingValidator.RemoveUngroundedNonAuthoritativeCriterionEvidenceRefs(
            assignment,
            normalizedOutput,
            toolReceipts,
            stepContract ?? ProcessStepExecutionContract.Empty,
            out var removedNonAuthoritativeCriterionEvidenceRefCount);
        if (removedNonAuthoritativeCriterionEvidenceRefCount > 0)
        {
            logger.LogWarning(
                "Omitted {RemovedEvidenceRefCount} ungrounded evidence refs from typed NotVerified criterion entries before managed artifact materialization. ProcessRunId={RunId} StepInstanceId={StepInstanceId} StepKey={StepKey} ExecutionRunId={ExecutionRunId}",
                removedNonAuthoritativeCriterionEvidenceRefCount,
                assignment.RunId.Value,
                assignment.StepInstanceId.Value,
                assignment.StepKey,
                executionRunId);
        }

        normalizedOutput = groundingValidator.RemoveUngroundedSupplementalEvidenceRefsFromGroundedDefectOutcome(
            assignment,
            normalizedOutput,
            toolReceipts,
            stepContract ?? ProcessStepExecutionContract.Empty,
            out var removedSupplementalEvidenceRefCount);
        if (removedSupplementalEvidenceRefCount > 0)
        {
            logger.LogWarning(
                "Omitted {RemovedEvidenceRefCount} ungrounded supplemental top-level evidence refs from a grounded non-acceptance outcome before managed artifact materialization. ProcessRunId={RunId} StepInstanceId={StepInstanceId} StepKey={StepKey} ExecutionRunId={ExecutionRunId}",
                removedSupplementalEvidenceRefCount,
                assignment.RunId.Value,
                assignment.StepInstanceId.Value,
                assignment.StepKey,
                executionRunId);
        }

        return managedArtifactService.MaterializeManagedOutcomeArtifactIfNeeded(
            assignment,
            normalizedOutput,
            executionRunId,
            toolReceipts);
    }

    internal Task<IReadOnlyList<ToolExecutionReceiptRecord>> LoadCompletionToolReceiptsAsync(
        IAgentExecutionHistoryReader workspaceService,
        ProcessRuntimeStepAssignment assignment,
        Guid currentExecutionRunId,
        IReadOnlyList<ToolExecutionReceiptRecord> currentToolReceipts,
        CancellationToken cancellationToken)
        => ProcessManagedArtifactService.LoadStepCompletionToolReceiptsAsync(
            workspaceService,
            assignment,
            currentExecutionRunId,
            currentToolReceipts,
            cancellationToken);

    internal ProcessExecutionAdapterResult Complete(
        ProcessRuntimeStepAssignment assignment,
        ProcessManagedArtifactService.ManagedOutcomeArtifactMaterialization materialization,
        string rawOutputHash,
        Guid executionRunId,
        IReadOnlyList<ToolExecutionReceiptRecord> completionToolReceipts,
        bool appendRuntimeGateFindings = false,
        ProcessStepExecutionContract? stepContract = null,
        ParentSubprocessBridgedOutcome? verifiedSubprocessOutcome = null)
    {
        if (materialization.Output.Status != ProcessStepOutcomeStatus.Completed)
        {
            return resultConverter.ToAdapterResult(
                assignment,
                materialization.Output,
                rawOutputHash,
                completionToolReceipts,
                executionRunId,
                producedArtifactContentHashes: null,
                stepContract: stepContract,
                verifiedSubprocessOutcome: verifiedSubprocessOutcome);
        }

        if (groundingValidator.ValidateManagedArtifactBodyReferences(
                assignment,
                materialization.Output,
                completionToolReceipts,
                verifiedSubprocessOutcome,
                stepContract ?? ProcessStepExecutionContract.Empty) is { } ungroundedArtifactReferenceIssue)
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                ungroundedArtifactReferenceIssue);
        }

        var completionGateEvaluation = completionGateEvaluator.Evaluate(new ProcessCompletionGateContext(
            assignment,
            materialization.Output,
            completionToolReceipts,
            executionRunId)
        {
            StepContract = stepContract ?? ProcessStepExecutionContract.Empty,
            VerifiedSubprocessOutcome = verifiedSubprocessOutcome
        });
        if (!completionGateEvaluation.IsSatisfied)
        {
            if (appendRuntimeGateFindings &&
                completionIssueResultFactory.AppendRuntimeGateFindingsForRoutedCompletionIssue(
                    assignment,
                    materialization.Output,
                    executionRunId,
                    completionGateEvaluation,
                    completionToolReceipts) is { } runtimeGateFindingsIssue)
            {
                return NeedsManagerForCompletionIssue(
                    assignment,
                    rawOutputHash,
                    runtimeGateFindingsIssue);
            }

            if (completionIssueResultFactory.TryCreateRoutedCompletionIssueResult(
                    assignment,
                    materialization.Output,
                    rawOutputHash,
                    completionGateEvaluation,
                    completionToolReceipts,
                    executionRunId,
                    producedArtifactContentHashes: null,
                    out var routedCompletionIssueResult))
            {
                return routedCompletionIssueResult;
            }

            return NeedsManagerForCompletionIssues(
                assignment,
                rawOutputHash,
                completionGateEvaluation);
        }

        if (managedArtifactService.AcceptManagedOutcomeArtifactIfNeeded(
                assignment,
                materialization,
                executionRunId,
                completionToolReceipts,
                out var acceptedCompletionToolReceipts,
                out var acceptedArtifactContentHashes) is { } acceptanceIssue)
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                acceptanceIssue);
        }

        var producedArtifactReadbackIssue = default(ProcessCompletionIssue);
        var producedArtifactContentHashes = acceptedArtifactContentHashes;
        if (producedArtifactContentHashes is null)
        {
            producedArtifactContentHashes = managedArtifactService.BuildProducedArtifactContentHashes(
                assignment,
                materialization.Output,
                out producedArtifactReadbackIssue);
        }

        if (producedArtifactReadbackIssue is not null)
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                producedArtifactReadbackIssue);
        }

        return resultConverter.ToAdapterResult(
            assignment,
            materialization.Output,
            rawOutputHash,
            acceptedCompletionToolReceipts,
            executionRunId,
            producedArtifactContentHashes,
            stepContract,
            verifiedSubprocessOutcome);
    }
}
