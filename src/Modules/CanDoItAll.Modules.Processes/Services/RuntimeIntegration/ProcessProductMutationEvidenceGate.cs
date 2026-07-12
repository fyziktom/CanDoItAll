using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionMetadataBuilder;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessProductMutationEvidenceGate
{
    internal static bool IsProductMutationEvidenceMissing(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
        => output.Status == ProcessStepOutcomeStatus.Completed &&
           AllowsProductMutation(NormalizeOperations(assignment.AllowedOperations), assignment.OperationTargetScope) &&
           !output.EvidenceRefs.Any(reference => !string.IsNullOrWhiteSpace(reference));

    internal static ProcessCompletionIssue? Validate(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        if (!IsProductMutationEvidenceMissing(assignment, output))
        {
            return null;
        }

        return new ProcessCompletionIssue(
            "process.adapter.product_output_evidence_missing",
            $"Step '{assignment.StepKey}' claimed completion for a product-mutating scope but returned no evidence references.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:evidence-missing",
            [],
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }
}
