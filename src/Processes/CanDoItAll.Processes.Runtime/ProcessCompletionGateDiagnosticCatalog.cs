namespace CanDoItAll.Processes.Runtime;

internal static class ProcessCompletionGateDiagnosticCatalog
{
    public static bool IsCompletionGateDiagnosticCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        return code.StartsWith("process.adapter.product_", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("process.adapter.produced_artifact_", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("process.adapter.ungrounded_", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "process.adapter.required_tool_receipt_missing", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "process.adapter.completed_outcome_declares_unresolved_blocker", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "process.adapter.branch_outcome_defect_evidence_missing", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "process.adapter.branch_route_defect_evidence_missing", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "process.adapter.runtime_lifecycle_correlation_missing", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "process.adapter.runtime_gate_findings_append_failed", StringComparison.OrdinalIgnoreCase);
    }
}
