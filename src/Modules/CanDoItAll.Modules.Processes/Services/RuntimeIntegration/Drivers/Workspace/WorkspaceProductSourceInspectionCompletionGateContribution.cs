using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed class WorkspaceProductSourceInspectionCompletionGateContribution : IProcessCompletionGateContribution
{
    private const string PolicyInvalidDiagnosticCode = "process.runtime.product_source_inspection_policy_invalid";

    public string ContributionKey => "product-source-inspection";

    public int Order => 100;

    public ProcessCompletionIssue? Validate(ProcessCompletionGateContext context)
    {
        if (context.Output.Status != ProcessStepOutcomeStatus.Completed ||
            context.CurrentExecutionRunId is null)
        {
            return null;
        }

        var policy = WorkspaceProductSourceInspectionPolicy.Evaluate(
            context.Assignment.LaunchVariables,
            context.Assignment.StepKey,
            context.Output.BranchOutcomeKey);
        if (policy.Issue is not null)
        {
            return CreateConfigurationIssue(context.Assignment, policy.Issue);
        }

        if (!policy.IsInspectionRequired)
        {
            return null;
        }

        if (!WorkspaceProductSourceInspectionReceiptFacts.TryGetGroundedProductRootAlias(
                context.Assignment,
                out var productRootAlias))
        {
            return CreateEvidenceIssue(
                context.Assignment,
                $"Step '{context.Assignment.StepKey}' requires current-run product-source inspection but has no grounded external product root alias.");
        }

        var hasProductSourceRead = context.ToolReceipts?.Any(receipt =>
            receipt.ExecutionRunId == context.CurrentExecutionRunId.Value &&
            WorkspaceProductSourceInspectionReceiptFacts.IsSuccessfulProductSourceRead(
                receipt,
                productRootAlias,
                policy.ExcludedPathFragments)) == true;
        if (hasProductSourceRead)
        {
            return null;
        }

        var rejectedReadPaths = WorkspaceProductSourceInspectionReceiptFacts.ResolveRejectedProductSourceReadPaths(
            context.ToolReceipts,
            context.CurrentExecutionRunId.Value,
            productRootAlias,
            policy.ExcludedPathFragments);
        var rejectedReadSummary = rejectedReadPaths.Count == 0
            ? string.Empty
            : $" Current-run reads rejected as non-owning: {string.Join(", ", rejectedReadPaths.Select(path => $"'{path}'"))}.";
        var exclusionSummary = policy.ExcludedPathFragments.Count == 0
            ? string.Empty
            : $" Do not retry with a path containing any configured non-owning fragment: {string.Join(", ", policy.ExcludedPathFragments.Select(fragment => $"'{fragment}'"))}.";

        return CreateEvidenceIssue(
            context.Assignment,
            $"Step '{context.Assignment.StepKey}' requires diagnosis or repair grounded in current product source, but this execution did not read a representative owning product file under '{productRootAlias}'.{rejectedReadSummary}{exclusionSummary} List or search the grounded product root, then read a different owning product source before completing; shell layout, navigation, stylesheet, upstream diagnosis prose, and managed artifacts alone are not owning-source inspection.");
    }

    private static ProcessCompletionIssue CreateConfigurationIssue(
        ProcessRuntimeStepAssignment assignment,
        WorkspaceProductSourceInspectionPolicyIssue issue)
        => new(
            PolicyInvalidDiagnosticCode,
            $"Step '{assignment.StepKey}' has invalid product-source inspection policy configuration in '{issue.VariableName}': {issue.Reason} Correct the process template policy before retrying.",
            $"{assignment.RunId.Value:D}:{assignment.StepInstanceId.Value:D}:product-source-inspection-policy-invalid:{issue.VariableName}:{issue.Reason}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.UnsafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Unknown);

    private static ProcessCompletionIssue CreateEvidenceIssue(
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
