namespace CanDoItAll.Processes.Runtime;

public static class ProcessCompletionDiagnosticCodes
{
    public const string ProductMutationReceiptMissing = "process.adapter.product_mutation_receipt_missing";
    public const string ManagedArtifactWriteReceiptMissing = "process.adapter.produced_artifact_write_receipt_missing";
    public const string ProductRequiredToolReceiptMissing = "process.adapter.product_required_tool_receipt_missing";
    public const string ToolReceiptEvidenceContentRejected = "process.adapter.tool_receipt_evidence_content_rejected";
    public const string ProductSourceInspectionEvidenceMissing = "process.adapter.product_source_inspection_evidence_missing";
    public const string UiInteractionEvidenceMissing = "process.adapter.ui_interaction_evidence_missing";
    public const string UiPostInteractionStateEvidenceMissing = "process.adapter.ui_post_interaction_state_evidence_missing";
    public const string RuntimeRoutedBranchSelectedDirectly = "process.adapter.runtime_routed_branch_selected_directly";
    public const string RequiredBranchOutcomeMissing = "process.adapter.required_branch_outcome_missing";
}
