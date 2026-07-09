using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessCompletionGateEvaluatorTests
{
    [Fact]
    public void Evaluate_deduplicates_issues_and_orders_by_gate_priority()
    {
        var evaluator = new ProcessCompletionGateEvaluator(
        [
            _ => CreateIssue("process.adapter.product_output_missing", "product-output", ProcessDiagnosticRetrySafety.SafeToRetry),
            _ => CreateIssue("process.adapter.product_output_missing", "product-output", ProcessDiagnosticRetrySafety.SafeToRetry),
            _ => CreateIssue("process.adapter.completed_outcome_declares_unresolved_blocker", "unsafe-blocker", ProcessDiagnosticRetrySafety.UnsafeToRetry),
            _ => null,
            _ => CreateIssue("process.adapter.unknown_gate", "unknown", ProcessDiagnosticRetrySafety.SafeToRetry)
        ]);

        var result = evaluator.Evaluate(new ProcessCompletionGateContext(
            Assignment: null!,
            Output: null!,
            ToolReceipts: null,
            CurrentExecutionRunId: null));

        Assert.False(result.IsSatisfied);
        Assert.Equal(3, result.Issues.Count);
        Assert.Equal(
            [
                "process.adapter.completed_outcome_declares_unresolved_blocker",
                "process.adapter.product_output_missing",
                "process.adapter.unknown_gate"
            ],
            result.OrderedIssues.Select(issue => issue.Code));
    }

    private static ProcessCompletionIssue CreateIssue(
        string code,
        string evidence,
        ProcessDiagnosticRetrySafety retrySafety)
        => new(
            code,
            Summary: code,
            evidence,
            RequestedArtifactSlotIds: [],
            retrySafety,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
}
