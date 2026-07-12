namespace CanDoItAll.Processes.Runtime;

internal static class ProcessCompletionGateDiagnosticCatalog
{
    private const string ManagedArtifactMissingPrimaryOutputRetryCode = "process.adapter.managed_artifact_missing_primary_output_retry";
    private const string ManagedArtifactSelfEvidenceRetryCode = "process.adapter.managed_artifact_self_evidence_retry";
    private const string NonTerminalPrimaryArtifactRetryCode = "process.adapter.non_terminal_primary_artifact_retry";

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
               string.Equals(code, ProcessCompletionDiagnosticCodes.RequiredBranchOutcomeMissing, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, ProcessCompletionDiagnosticCodes.RuntimeRoutedBranchSelectedDirectly, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "process.adapter.runtime_lifecycle_correlation_missing", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "process.adapter.runtime_gate_findings_append_failed", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, ProcessCompletionDiagnosticCodes.ToolReceiptEvidenceContentRejected, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, ProcessCompletionDiagnosticCodes.ArtifactPayloadSchemaInvalid, StringComparison.OrdinalIgnoreCase) ||
               IsManagedArtifactCompletionRetryCode(code);
    }

    private static bool IsManagedArtifactCompletionRetryCode(string code)
        => string.Equals(code, ManagedArtifactMissingPrimaryOutputRetryCode, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(code, ManagedArtifactSelfEvidenceRetryCode, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(code, NonTerminalPrimaryArtifactRetryCode, StringComparison.OrdinalIgnoreCase);
}
