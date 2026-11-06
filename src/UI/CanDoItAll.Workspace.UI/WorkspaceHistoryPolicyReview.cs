using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Editing;

namespace CanDoItAll.Workspace.UI;

public sealed class WorkspaceHistoryPolicyReview(HistoryPolicySnapshot original, HistoryPolicy local, HistoryPolicySnapshot saved) {
    public HistoryPolicySnapshot Original { get; } = original;
    public HistoryPolicy Local { get; } = local;
    public HistoryPolicySnapshot Saved { get; } = saved;
    public MergeField<(HistoryCaptureMode Mode, int MaximumTextBytes)> Capture { get; } =
        new(CaptureOf(original.Policy), CaptureOf(local), CaptureOf(saved.Policy));
    public MergeField<(int MetadataDays, int DetailDays, long QuotaBytes)> Retention { get; } = CompareRetention(original.Policy, local, saved.Policy);
    public MergeField<int> Batch { get; } = new(original.Policy.BatchSize, local.BatchSize, saved.Policy.BatchSize);
    public bool IsResolved => Capture.IsResolved && Retention.IsResolved && Batch.IsResolved;

    public HistoryPolicy Resolve() => new() {
        CaptureMode = Capture.Result.Mode, MaximumTextBytes = Capture.Result.MaximumTextBytes, BatchSize = Batch.Result,
        MetadataRetentionDays = Retention.Result.MetadataDays, DetailRetentionDays = Retention.Result.DetailDays, DetailQuotaBytes = Retention.Result.QuotaBytes
    };

    private static (HistoryCaptureMode, int) CaptureOf(HistoryPolicy policy) => (policy.CaptureMode, policy.MaximumTextBytes);

    private static MergeField<(int, int, long)> CompareRetention(HistoryPolicy original, HistoryPolicy local, HistoryPolicy saved) {
        static (int, int, long) Values(HistoryPolicy policy) => (policy.MetadataRetentionDays, policy.DetailRetentionDays, policy.DetailQuotaBytes);
        var field = new MergeField<(int, int, long)>(Values(original), Values(local), Values(saved));
        if (field.LocalChanged && field.Local != field.Current) {
            field.RequireChoice();
        }
        return field;
    }
}
