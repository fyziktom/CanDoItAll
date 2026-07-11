using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessRequiredProductSourceInspectionGate
{
    internal ProcessCompletionIssue? Validate(ProcessCompletionGateContext context)
    {
        if (context.Output.Status != ProcessStepOutcomeStatus.Completed ||
            context.CurrentExecutionRunId is null ||
            !ProcessProductSourceInspectionPolicy.IsRequired(
                context.Assignment.LaunchVariables,
                context.Assignment.StepKey,
                context.Output.BranchOutcomeKey))
        {
            return null;
        }

        if (!ProcessProductSourceInspectionReceiptFacts.TryGetGroundedProductRootAlias(
                context.Assignment,
                out var productRootAlias))
        {
            return CreateIssue(
                context.Assignment,
                $"Step '{context.Assignment.StepKey}' requires current-run product-source inspection but has no grounded external product root alias.");
        }

        var excludedPathFragments = ProcessProductSourceInspectionPolicy.ResolveExcludedPathFragments(
            context.Assignment.LaunchVariables,
            context.Assignment.StepKey);
        var hasProductSourceRead = context.ToolReceipts?.Any(receipt =>
            receipt.ExecutionRunId == context.CurrentExecutionRunId.Value &&
            ProcessProductSourceInspectionReceiptFacts.IsSuccessfulProductSourceRead(
                receipt,
                productRootAlias,
                excludedPathFragments)) == true;
        if (hasProductSourceRead)
        {
            return null;
        }

        var rejectedReadPaths = ProcessProductSourceInspectionReceiptFacts.ResolveRejectedProductSourceReadPaths(
            context.ToolReceipts,
            context.CurrentExecutionRunId.Value,
            productRootAlias,
            excludedPathFragments);
        var rejectedReadSummary = rejectedReadPaths.Count == 0
            ? string.Empty
            : $" Current-run reads rejected as non-owning: {string.Join(", ", rejectedReadPaths.Select(path => $"'{path}'"))}.";
        var exclusionSummary = excludedPathFragments.Count == 0
            ? string.Empty
            : $" Do not retry with a path containing any configured non-owning fragment: {string.Join(", ", excludedPathFragments.Select(fragment => $"'{fragment}'"))}.";

        return CreateIssue(
            context.Assignment,
            $"Step '{context.Assignment.StepKey}' requires diagnosis or repair grounded in current product source, but this execution did not read a representative owning product file under '{productRootAlias}'.{rejectedReadSummary}{exclusionSummary} List or search the grounded product root, then read a different application, component, domain, or mapped test source that owns the failed behavior before completing; shell layout, navigation, stylesheet, upstream diagnosis prose, and managed artifacts alone are not owning-source inspection.");
    }

    private static ProcessCompletionIssue CreateIssue(
        ProcessRuntimeStepAssignment assignment,
        string summary)
        => new(
            ProcessCompletionDiagnosticCodes.ProductSourceInspectionEvidenceMissing,
            summary,
            $"{assignment.RunId.Value:D}:{assignment.StepInstanceId.Value:D}:required-product-source-inspection",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
}
