using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Modules.Memory.Services;

public sealed class MemoryProviderProfileUiService(
    MemoryUiProfileOrigin origin,
    IMemoryProviderProfileStore providerProfileStore,
    MemoryProviderProfileEditorMapper editorMapper,
    TimeProvider timeProvider)
{
    public async Task<MemoryProviderProfile> SaveAsync(
        MemoryProviderProfileEditorModel editor,
        CancellationToken cancellationToken)
    {
        origin.RequireCurrent();
        MemoryProviderProfile profile;
        try {
            profile = editorMapper.ToProfile(editor.Capture());
        } catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) {
            throw new MemoryActionRefusedException(exception.Message);
        }
        await providerProfileStore.UpsertAsync(profile, timeProvider.GetUtcNow(), cancellationToken);
        return profile;
    }

    public async Task<IReadOnlyList<MemoryProviderProfile>> CreateDemoProvidersAsync(
        CancellationToken cancellationToken)
    {
        origin.RequireCurrent();
        HashSet<string> existingIds;
        try {
            existingIds = (await providerProfileStore.ListAsync(cancellationToken)).Select(profile => profile.InstanceId.Value).ToHashSet(StringComparer.Ordinal);
        } catch (Exception) {
            throw new MemoryActionRefusedException("The demo catalog could not be read. No demo profile writes were started.");
        }
        var demoProfiles = new[]
        {
            CreateDemoProvider(
                MemoryDemoProviderIds.Business,
                "Business demo memory",
                MemoryProviderHealthState.Healthy),
            CreateDemoProvider(
                MemoryDemoProviderIds.Programming,
                "Programming demo memory",
                MemoryProviderHealthState.Degraded)
        };

        var savedProfiles = new List<MemoryProviderProfile>();
        foreach (var profile in demoProfiles.Where(profile => !existingIds.Contains(profile.InstanceId.Value)))
        {
            try {
                origin.RequireCurrent();
                await providerProfileStore.UpsertAsync(profile, timeProvider.GetUtcNow(), cancellationToken);
                savedProfiles.Add(profile);
            } catch (Exception exception) {
                throw new MemoryDemoInterruptedException(savedProfiles, exception);
            }
        }

        return savedProfiles;
    }

    private MemoryProviderProfile CreateDemoProvider(
        string instanceId,
        string displayName,
        MemoryProviderHealthState healthState)
    {
        return editorMapper.ToProfile(new MemoryProviderProfileEditorModel
        {
            InstanceId = instanceId,
            DisplayName = displayName,
            DriverKind = MemoryProviderDriverKind.Mock,
            IsEnabled = true,
            HealthState = healthState,
            WorkspaceScope = MemoryProviderWorkspaceScope.AllWorkspaces,
            ProviderKind = "memory.mock",
            SupportsContextQuerySync = true
        });
    }
}
