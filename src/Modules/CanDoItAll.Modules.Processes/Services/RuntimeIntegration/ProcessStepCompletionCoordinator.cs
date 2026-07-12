using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

using static CanDoItAll.Modules.Processes.ProcessCompletionIssueResultFactory;
using static CanDoItAll.Modules.Processes.ProcessBranchOutcomeResolver;
using static CanDoItAll.Modules.Processes.ProcessCompletionRetryPolicy;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessStepCompletionCoordinator(
    ProcessCompletionIssueResultFactory completionIssueResultFactory,
    ProcessManagedArtifactService managedArtifactService,
    ProcessOutcomeGroundingValidator groundingValidator,
    ProcessCompletionGateEvaluator completionGateEvaluator,
    ProcessExecutionResultConverter resultConverter)
{
    internal ProcessManagedArtifactService.ManagedOutcomeArtifactMaterialization Materialize(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        Guid executionRunId,
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts)
    {
        var normalizedOutput = ProcessOutcomeCitationSanitizer.RemoveNonCitableSourceMetadataFromOutcome(output);
        normalizedOutput = managedArtifactService.RecoverUnambiguousBranchOutcomeFromCurrentPrimaryArtifact(
            assignment,
            normalizedOutput,
            executionRunId,
            toolReceipts);
        if (ShouldRouteBlockedBranchOutcome(assignment, normalizedOutput))
        {
            normalizedOutput = CopyAsCompletedBranchOutcome(normalizedOutput);
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
        ProcessStepExecutionContract? stepContract = null)
    {
        if (groundingValidator.ValidateManagedArtifactBodyReferences(
                assignment,
                materialization.Output,
                completionToolReceipts) is { } ungroundedArtifactReferenceIssue)
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
            StepContract = stepContract ?? ProcessStepExecutionContract.Empty
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
                out var acceptedCompletionToolReceipts) is { } acceptanceIssue)
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                acceptanceIssue);
        }

        var producedArtifactContentHashes = managedArtifactService.BuildProducedArtifactContentHashes(
            assignment,
            materialization.Output,
            out var producedArtifactReadbackIssue);
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
            stepContract);
    }
}
