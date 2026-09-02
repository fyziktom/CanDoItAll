namespace CanDoItAll.AgentFramework.ProviderHistory;

public static class HistoryContractValidation {
    public static void Validate(HistoryPolicy policy) {
        ArgumentNullException.ThrowIfNull(policy);
        if (!Enum.IsDefined(policy.CaptureMode) || policy.MetadataRetentionDays is < 1 or > 3650
            || policy.DetailRetentionDays < 1 || policy.DetailRetentionDays > policy.MetadataRetentionDays
            || policy.MaximumTextBytes is < 1 or > 128 * 1024
            || policy.DetailQuotaBytes < 1 || policy.BatchSize is < 1 or > 1000) {
            throw new ArgumentException("History policy exceeds supported retention, text, quota or batch limits.", nameof(policy));
        }
    }

    public static void Validate(ProviderRequestHistoryQuery query) {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Scope is null
            || query.Scope is HistoryProviderScope.SingleProvider { Provider.Value: var id } && id == Guid.Empty
            || query.ToUtc <= query.FromUtc || query.ToUtc - query.FromUtc > TimeSpan.FromDays(31)
            || query.PageSize is < 1 or > 200
            || query.Model is { Value: null or "" }
            || query.CredentialId?.Value == Guid.Empty
            || query.RequestId?.Value == Guid.Empty || query.AttemptId?.Value == Guid.Empty
            || !Valid(query.Workload) || !Valid(query.Operation) || !Valid(query.Outcome) || !Valid(query.PriceState)
            || query.Issuer?.Length > 512 || query.Subject?.Length > 512
            || query.CorrelationId?.Length > 256 || query.Cursor?.Length > 8192) {
            throw new ProviderHistoryException(HistoryFailure.InvalidQuery, "Select a valid provider, interval of at most 31 days and page size from 1 to 200.");
        }
    }

    public static void Validate(HistoryAttemptStart start) {
        ArgumentNullException.ThrowIfNull(start);
        if (start.EntryId.Value == Guid.Empty || start.RequestId.Value == Guid.Empty
            || start.AttemptId.Value == Guid.Empty || start.Provider.Id is null
            || start.Provider.Id.Value.Value == Guid.Empty
            || start.Provider.ResolvedModel is null && start.Operation is not (HistoryOperation.ListModels or HistoryOperation.TestHealth)
            || start.Partition.OriginInstanceId == Guid.Empty || start.Partition.StorageLineageId == Guid.Empty
            || string.IsNullOrWhiteSpace(start.Partition.SecurityPartition)
            || start.Partition.SecurityPartition.Length > 128
            || !Enum.IsDefined(start.Operation) || !Enum.IsDefined(start.Workload)) {
            throw new ArgumentException("A resolved attempt requires stable request, provider, model and partition identities.", nameof(start));
        }
        Validate(start.Policy.Policy);
    }

    private static bool Valid<T>(T? value) where T : struct, Enum => value is null || Enum.IsDefined(value.Value);
}
