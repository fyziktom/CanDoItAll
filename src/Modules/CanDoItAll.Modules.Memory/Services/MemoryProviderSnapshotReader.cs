using CanDoItAll.Memory.Application;

namespace CanDoItAll.Modules.Memory.Services;

public sealed class MemoryProviderSnapshotReader(
    IMemoryProviderProfileStore providerProfileStore,
    IMemoryOperationLedgerStore operationLedgerStore,
    IMemoryFeedbackLedgerStore feedbackLedgerStore,
    IMemoryEventLedgerStore eventLedgerStore,
    MemoryProviderUiSurfaceProjector uiSurfaceProjector)
{
    public async Task<MemoryProviderManagementSnapshot> GetSnapshotAsync(
        string? selectedProviderInstanceId,
        CancellationToken cancellationToken)
    {
        var profiles = await providerProfileStore.ListAsync(cancellationToken);
        var viewProfiles = profiles
            .Select(MemoryProviderManagementProfile.FromProfile)
            .OrderBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(profile => profile.InstanceId.Value, StringComparer.Ordinal)
            .ToArray();
        var selectedProvider = viewProfiles.FirstOrDefault(profile =>
                string.Equals(profile.InstanceId.Value, selectedProviderInstanceId, StringComparison.Ordinal)) ??
            viewProfiles.FirstOrDefault();

        if (selectedProvider is null)
        {
            return new MemoryProviderManagementSnapshot(viewProfiles, null, [], [], [], []);
        }

        var operations = (await operationLedgerStore.ListByProviderAsync(
                selectedProvider.InstanceId,
                cancellationToken: cancellationToken))
            .Select(MemoryProviderUiRecordMapper.ToUiRecord)
            .ToArray();
        var feedback = (await feedbackLedgerStore.ListByProviderAsync(selectedProvider.InstanceId, cancellationToken))
            .OrderByDescending(record => record.UpdatedAtUtc)
            .Select(MemoryProviderUiRecordMapper.ToUiRecord)
            .ToArray();
        var events = (await eventLedgerStore.ListPendingInboxAsync(
                selectedProvider.InstanceId,
                cancellationToken: cancellationToken))
            .OrderByDescending(record => record.UpdatedAtUtc)
            .Select(MemoryProviderUiRecordMapper.ToUiRecord)
            .ToArray();

        return new MemoryProviderManagementSnapshot(
            viewProfiles,
            selectedProvider,
            operations,
            feedback,
            events,
            uiSurfaceProjector.Project(selectedProvider));
    }
}
