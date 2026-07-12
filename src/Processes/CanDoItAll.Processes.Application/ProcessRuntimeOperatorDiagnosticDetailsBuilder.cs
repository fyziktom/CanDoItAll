using System.Text.RegularExpressions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

internal static partial class ProcessRuntimeOperatorDiagnosticDetailsBuilder
{
    public static ProcessRuntimeOperatorDiagnosticDetailsProjection? Create(
        string code,
        string safeSummary)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var gateId = ResolveGateId(code);
        if (string.IsNullOrWhiteSpace(gateId))
        {
            return null;
        }

        return new ProcessRuntimeOperatorDiagnosticDetailsProjection(
            gateId,
            ExtractBranchOutcomeKey(safeSummary),
            ExtractRouteTargetBranchOutcomeKey(code, safeSummary),
            ExtractFailedCriteriaIds(safeSummary),
            ExtractReceiptRuleIds(safeSummary),
            ResolveNextAction(code, safeSummary));
    }

    private static string ResolveGateId(string code)
        => code switch
        {
            "process.adapter.product_required_tool_receipt_missing" => "product-tool-receipt-gate",
            "process.adapter.required_tool_receipt_missing" => "process-tool-receipt-gate",
            "process.adapter.product_required_file_content_missing" => "product-content-readback-gate",
            ProcessCompletionDiagnosticCodes.ToolReceiptEvidenceContentRejected => "tool-receipt-evidence-content-gate",
            "process.adapter.branch_outcome_defect_evidence_missing" => "branch-defect-evidence-gate",
            "process.adapter.acceptance_criteria_missing" => "acceptance-criteria-gate",
            "process.adapter.runtime_lifecycle_correlation_missing" => "runtime-lifecycle-gate",
            "process.adapter.completion_issue_routed" => "completion-issue-route",
            _ => code.StartsWith("process.adapter.", StringComparison.OrdinalIgnoreCase)
                ? code["process.adapter.".Length..].Replace('_', '-')
                : string.Empty
        };

    private static string ExtractBranchOutcomeKey(string summary)
    {
        foreach (Match match in BranchRegex().Matches(summary))
        {
            var branch = match.Groups["branch"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(branch))
            {
                return branch;
            }
        }

        return string.Empty;
    }

    private static string ExtractRouteTargetBranchOutcomeKey(string code, string summary)
    {
        if (!string.Equals(code, "process.adapter.completion_issue_routed", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var match = RouteTargetRegex().Match(summary);
        return match.Success
            ? match.Groups["branch"].Value.Trim()
            : string.Empty;
    }

    private static IReadOnlyList<string> ExtractFailedCriteriaIds(string summary)
        => CriteriaIdRegex()
            .Matches(summary)
            .Select(match => match.Value.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<string> ExtractReceiptRuleIds(string summary)
        => ToolReceiptIdentifierRegex()
            .Matches(summary)
            .Select(match => match.Value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string ResolveNextAction(string code, string summary)
    {
        return code switch
        {
            "process.adapter.completion_issue_routed" => string.IsNullOrWhiteSpace(ExtractRouteTargetBranchOutcomeKey(code, summary))
                ? "Follow the configured completion issue route."
                : $"Follow routed branch '{ExtractRouteTargetBranchOutcomeKey(code, summary)}'.",
            "process.adapter.acceptance_criteria_missing" => "Retry accepted-branch QA with criterion-by-criterion evidence, or select a repair branch when criteria fail.",
            "process.adapter.runtime_lifecycle_correlation_missing" => "Repeat the configured current-execution lifecycle with correlated operation, observation, and cleanup receipts before completing this branch.",
            "process.adapter.product_required_tool_receipt_missing" => "Invoke the missing current-run product proof tools before completing this branch.",
            "process.adapter.required_tool_receipt_missing" => "Invoke the missing current-run process capability tools before completing this branch.",
            "process.adapter.product_required_file_content_missing" => "Repair the deterministic product content defect or route to the configured repair branch.",
            ProcessCompletionDiagnosticCodes.ArtifactPayloadSchemaInvalid => "Rewrite the declared schema-bound artifact from its template contract before completing this branch.",
            ProcessCompletionDiagnosticCodes.ToolReceiptEvidenceContentRejected => "Repair the rejected current-run tool evidence or route to the configured repair branch.",
            "process.adapter.branch_outcome_defect_evidence_missing" => "Provide deterministic defect evidence before using the configured repair route.",
            _ => "Inspect the diagnostic and retry only after the stated gate is satisfied."
        };
    }

    [GeneratedRegex(@"\bbranch\s+'(?<branch>[^']+)'", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BranchRegex();

    [GeneratedRegex(@"\bto\s+branch\s+'(?<branch>[^']+)'", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RouteTargetRegex();

    [GeneratedRegex(@"\bAC-\d{3,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CriteriaIdRegex();

    [GeneratedRegex(@"\b[A-Za-z][A-Za-z0-9]*(?:_[A-Za-z0-9]+)+\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ToolReceiptIdentifierRegex();
}
