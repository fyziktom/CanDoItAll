using System.ComponentModel.DataAnnotations;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.Modules.Workspace.Pages.Components;

public sealed class ProviderHistoryPolicyDraft : IValidatableObject {
    public HistoryCaptureMode CaptureMode { get; set; }
    [Range(1, 3650)]
    public int MetadataRetentionDays { get; set; }
    [Range(1, 3650)]
    public int DetailRetentionDays { get; set; }
    [Range(1, 131072)]
    public int MaximumTextBytes { get; set; }
    [Range(typeof(long), "1", "9223372036854775807")]
    public long DetailQuotaBytes { get; set; }
    [Range(1, 1000)]
    public int BatchSize { get; set; }

    public static ProviderHistoryPolicyDraft From(HistoryPolicy policy) => new() {
        CaptureMode = policy.CaptureMode,
        MetadataRetentionDays = policy.MetadataRetentionDays,
        DetailRetentionDays = policy.DetailRetentionDays,
        MaximumTextBytes = policy.MaximumTextBytes,
        DetailQuotaBytes = policy.DetailQuotaBytes,
        BatchSize = policy.BatchSize
    };

    public HistoryPolicy ToPolicy() => new() {
        CaptureMode = CaptureMode, MetadataRetentionDays = MetadataRetentionDays,
        DetailRetentionDays = DetailRetentionDays, MaximumTextBytes = MaximumTextBytes,
        DetailQuotaBytes = DetailQuotaBytes, BatchSize = BatchSize
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) {
        if (!Enum.IsDefined(CaptureMode)) {
            yield return new("Choose Light or Detailed capture.", [nameof(CaptureMode)]);
        }
        if (DetailRetentionDays > MetadataRetentionDays) {
            yield return new("Detail retention cannot exceed metadata retention.", [nameof(DetailRetentionDays)]);
        }
    }
}
