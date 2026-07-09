using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessCompletionGateContext(
    ProcessRuntimeStepAssignment Assignment,
    ProcessStepOutcomeResult Output,
    IReadOnlyList<ToolExecutionReceiptRecord>? ToolReceipts,
    Guid? CurrentExecutionRunId);

internal sealed record ProcessCompletionIssue(
    string Code,
    string Summary,
    string Evidence,
    IReadOnlyList<ArtifactSlotId> RequestedArtifactSlotIds,
    ProcessDiagnosticRetrySafety RetrySafety,
    ProcessDiagnosticIdempotencyClassification Idempotency);

internal sealed record ProcessCompletionGateEvaluation(
    IReadOnlyList<ProcessCompletionIssue> Issues,
    IReadOnlyList<ProcessCompletionIssue> OrderedIssues)
{
    public bool IsSatisfied => Issues.Count == 0;
}

internal sealed class ProcessCompletionGateEvaluator
{
    private readonly IReadOnlyList<Func<ProcessCompletionGateContext, ProcessCompletionIssue?>> gates;

    public ProcessCompletionGateEvaluator(IEnumerable<Func<ProcessCompletionGateContext, ProcessCompletionIssue?>> gates)
    {
        ArgumentNullException.ThrowIfNull(gates);

        this.gates = gates.ToArray();
        if (this.gates.Count == 0)
        {
            throw new ArgumentException("At least one completion gate is required.", nameof(gates));
        }
    }

    public ProcessCompletionGateEvaluation Evaluate(ProcessCompletionGateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var issues = new List<ProcessCompletionIssue>();
        foreach (var gate in gates)
        {
            AddIssue(issues, gate(context));
        }

        return new ProcessCompletionGateEvaluation(issues, OrderCompletionGateIssues(issues));
    }

    private static void AddIssue(List<ProcessCompletionIssue> issues, ProcessCompletionIssue? issue)
    {
        if (issue is not null &&
            !issues.Any(existing =>
                string.Equals(existing.Code, issue.Code, StringComparison.Ordinal) &&
                string.Equals(existing.Evidence, issue.Evidence, StringComparison.Ordinal)))
        {
            issues.Add(issue);
        }
    }

    private static IReadOnlyList<ProcessCompletionIssue> OrderCompletionGateIssues(
        IReadOnlyList<ProcessCompletionIssue> issues)
    {
        return issues
            .OrderBy(GetCompletionGateIssuePriority)
            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(issue => issue.Evidence, StringComparer.Ordinal)
            .ToArray();
    }

    private static int GetCompletionGateIssuePriority(ProcessCompletionIssue issue)
    {
        if (issue.RetrySafety == ProcessDiagnosticRetrySafety.UnsafeToRetry)
        {
            return 0;
        }

        return issue.Code switch
        {
            "process.adapter.product_required_tool_receipt_missing" => 10,
            "process.adapter.required_tool_receipt_missing" => 11,
            "process.adapter.product_mutation_receipt_missing" => 12,
            "process.adapter.runtime_lifecycle_correlation_missing" => 13,
            "process.adapter.product_required_file_content_missing" => 20,
            "process.adapter.product_required_file_content_check_invalid" => 21,
            "process.adapter.acceptance_criteria_missing" => 22,
            "process.adapter.product_required_output_missing" => 30,
            "process.adapter.product_required_output_path_invalid" => 31,
            "process.adapter.product_output_missing" => 32,
            "process.adapter.product_output_evidence_missing" => 33,
            "process.adapter.produced_artifact_evidence_missing" => 40,
            "process.adapter.produced_artifact_write_receipt_missing" => 41,
            "process.adapter.managed_artifact_materialization_failed" => 42,
            "process.adapter.managed_artifact_outcome_append_failed" => 43,
            "process.adapter.managed_artifact_acceptance_append_failed" => 44,
            "process.adapter.managed_artifact_readback_failed" => 45,
            "process.adapter.ungrounded_outcome_reference" => 50,
            "process.adapter.ungrounded_managed_artifact_reference" => 51,
            "process.adapter.completed_outcome_declares_unresolved_blocker" => 60,
            _ => 100
        };
    }
}
