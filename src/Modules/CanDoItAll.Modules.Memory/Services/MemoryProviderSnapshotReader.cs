using CanDoItAll.Memory.Application;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Memory.Services;

public sealed class MemoryProviderSnapshotReader(
    MemoryUiProfileOrigin origin,
    IMemoryProviderProfileStore providerProfileStore,
    IMemoryOperationLedgerStore operationLedgerStore,
    IMemoryFeedbackLedgerStore feedbackLedgerStore,
    IMemoryEventLedgerStore eventLedgerStore,
    MemoryProviderProfileEditorMapper editorMapper,
    MemoryProviderUiSurfaceProjector uiSurfaceProjector,
    ILogger<MemoryProviderSnapshotReader> logger) {
    public async Task<MemoryProviderManagementSnapshot> GetSnapshotAsync(string? selectedProviderInstanceId, CancellationToken cancellationToken) {
        origin.RequireCurrent();
        var profiles = await providerProfileStore.ListAsync(cancellationToken);
        var viewProfiles = profiles.Select(MemoryProviderManagementProfile.FromProfile)
            .OrderBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(profile => profile.InstanceId.Value, StringComparer.Ordinal).ToArray();
        var selected = selectedProviderInstanceId is null ? viewProfiles.FirstOrDefault()
            : viewProfiles.FirstOrDefault(profile => string.Equals(profile.InstanceId.Value, selectedProviderInstanceId, StringComparison.Ordinal));
        if (selected is null) {
            origin.RequireCurrent();
            return new(viewProfiles, null, [], [], [], []);
        }
        var failures = new List<MemoryReadRegion>();
        var operations = await ReadAsync(MemoryReadRegion.Operations, async () => (await operationLedgerStore.ListByProviderAsync(selected.InstanceId, cancellationToken: cancellationToken)).Select(MemoryProviderUiRecordMapper.ToUiRecord).ToArray());
        var feedback = await ReadAsync(MemoryReadRegion.Feedback, async () => (await feedbackLedgerStore.ListByProviderAsync(selected.InstanceId, cancellationToken)).OrderByDescending(r => r.UpdatedAtUtc).Take(100).Select(MemoryProviderUiRecordMapper.ToUiRecord).ToArray());
        var events = await ReadAsync(MemoryReadRegion.Events, async () => (await eventLedgerStore.ListPendingInboxAsync(selected.InstanceId, cancellationToken: cancellationToken)).OrderByDescending(r => r.UpdatedAtUtc).Select(MemoryProviderUiRecordMapper.ToUiRecord).ToArray());
        origin.RequireCurrent();
        return new(viewProfiles, selected, operations, feedback, events, uiSurfaceProjector.Project(selected)) {
            Editor = editorMapper.FromProfile(selected), SelectedRevision = MemoryProviderRevision.Capture(selected), FailedRegions = failures.ToArray()
        };

        async Task<T[]> ReadAsync<T>(MemoryReadRegion region, Func<Task<T[]>> read) {
            try {
                return await read();
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception exception) {
                logger.LogWarning("Memory {Region} read failed for provider {ProviderId}; exception type {ExceptionType}. Other regions remain independently readable.", region, selected.InstanceId.Value, exception.GetType().Name);
                failures.Add(region);
                return [];
            }
        }
    }
}
