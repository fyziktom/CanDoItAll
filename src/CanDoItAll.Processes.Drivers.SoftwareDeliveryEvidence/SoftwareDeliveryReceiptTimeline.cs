namespace CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence;

public static class SoftwareDeliveryReceiptTimeline
{
    public static readonly HashSet<string> ConcreteProductMutationToolNames =
    [
        "workspace_write_file",
        "workspace_append_file",
        "workspace_move_path",
        "workspace_delete_path",
        "workspace_create_directory"
    ];

    public static readonly HashSet<string> ConcreteProductSourceWriteToolNames =
    [
        "workspace_write_file",
        "workspace_append_file",
        "workspace_move_path"
    ];

    public static SoftwareDeliveryToolReceiptSnapshot? ResolveLatestImplementationProofReadReceipt(
        bool requiresSourceOrProjectImplementationProof,
        IEnumerable<SoftwareDeliveryToolReceiptSnapshot> successfulReceipts)
    {
        return successfulReceipts
            .Where(receipt => string.Equals(
                SoftwareDeliveryEvidencePolicy.NormalizeToolToken(receipt.ToolName),
                "workspace_read_file",
                StringComparison.Ordinal))
            .Where(SoftwareDeliveryPathRules.HasConcreteProductPath)
            .Where(receipt => SoftwareDeliveryPathRules.HasConcreteProductImplementationPath(
                requiresSourceOrProjectImplementationProof,
                receipt))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
    }

    public static bool HasBuildValidationReceipt(IReadOnlyList<SoftwareDeliveryToolReceiptSnapshot> successfulReceipts)
    {
        return successfulReceipts.Any(receipt =>
        {
            var toolName = SoftwareDeliveryEvidencePolicy.NormalizeToolToken(receipt.ToolName);
            return IsBuildValidationToolName(toolName);
        });
    }

    public static bool IsBuildValidationToolName(string normalizedToolName)
    {
        return normalizedToolName.EndsWith("_build", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_test", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_publish", StringComparison.Ordinal);
    }

    public static bool IsRunValidationToolName(string normalizedToolName)
    {
        return normalizedToolName.EndsWith("_run", StringComparison.Ordinal);
    }

    public static SoftwareDeliveryToolReceiptSnapshot? ResolveLatestRequiredImplementationValidationReceipt(
        IReadOnlySet<string> requiredToolNames,
        IReadOnlyList<SoftwareDeliveryToolReceiptSnapshot> successfulReceipts)
    {
        if (requiredToolNames.Count == 0)
        {
            return null;
        }

        return successfulReceipts
            .Where(receipt =>
            {
                var normalizedToolName = SoftwareDeliveryEvidencePolicy.NormalizeToolToken(receipt.ToolName);
                return requiredToolNames.Contains(normalizedToolName) &&
                       IsImplementationValidationToolName(normalizedToolName);
            })
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
    }

    public static SoftwareDeliveryToolReceiptSnapshot? ResolveLatestReceipt(
        IEnumerable<SoftwareDeliveryToolReceiptSnapshot> receipts,
        Func<string, bool> matchesToolName,
        bool requireConcreteProductPath,
        bool requireConcreteDeliverableOrSourcePath)
    {
        return receipts
            .Where(receipt => matchesToolName(SoftwareDeliveryEvidencePolicy.NormalizeToolToken(receipt.ToolName)))
            .Where(receipt => !requireConcreteProductPath || SoftwareDeliveryPathRules.HasConcreteProductPath(receipt))
            .Where(receipt => !requireConcreteDeliverableOrSourcePath ||
                              SoftwareDeliveryPathRules.HasConcreteProductDeliverableOrSourcePath(receipt))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
    }

    public static bool IsConcreteProductMutationToolName(string normalizedToolName)
    {
        return ConcreteProductMutationToolNames.Contains(normalizedToolName) ||
               IsImplementationBootstrapToolName(normalizedToolName);
    }

    public static bool IsImplementationBootstrapToolName(string normalizedToolName)
    {
        return normalizedToolName.StartsWith("workspace_", StringComparison.Ordinal) &&
               normalizedToolName.EndsWith("_new", StringComparison.Ordinal);
    }

    public static bool IsImplementationValidationToolName(string normalizedToolName)
    {
        return normalizedToolName.EndsWith("_build", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_test", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_run", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_publish", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_validate", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_lint", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_check", StringComparison.Ordinal) ||
               normalizedToolName.StartsWith("browser_", StringComparison.Ordinal);
    }

    public static bool IsReceiptAfter(
        SoftwareDeliveryToolReceiptSnapshot candidate,
        SoftwareDeliveryToolReceiptSnapshot baseline)
    {
        return candidate.CompletedAtUtc > baseline.CompletedAtUtc ||
               candidate.CompletedAtUtc == baseline.CompletedAtUtc &&
               candidate.StartedAtUtc > baseline.StartedAtUtc;
    }
}
