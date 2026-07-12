using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Modules.Memory.Services;

public sealed class MemoryProviderProfileUiService(
    IMemoryProviderProfileStore providerProfileStore,
    MemoryProviderProfileEditorMapper editorMapper,
    TimeProvider timeProvider)
{
    public async Task<MemoryProviderProfile> SaveAsync(
        MemoryProviderProfileEditorModel editor,
        CancellationToken cancellationToken)
    {
        var profile = editorMapper.ToProfile(editor);
        await providerProfileStore.UpsertAsync(profile, timeProvider.GetUtcNow(), cancellationToken);
        return profile;
    }

    public async Task<IReadOnlyList<MemoryProviderProfile>> CreateDemoProvidersAsync(
        CancellationToken cancellationToken)
    {
        var existingIds = (await providerProfileStore.ListAsync(cancellationToken))
            .Select(profile => profile.InstanceId.Value)
            .ToHashSet(StringComparer.Ordinal);
        var demoProfiles = new[]
        {
            CreateDemoProvider(
                "provider.business-demo",
                "Business demo memory",
                MemoryProviderHealthState.Healthy),
            CreateDemoProvider(
                "provider.programming-demo",
                "Programming demo memory",
                MemoryProviderHealthState.Degraded)
        };

        var savedProfiles = new List<MemoryProviderProfile>();
        foreach (var profile in demoProfiles.Where(profile => !existingIds.Contains(profile.InstanceId.Value)))
        {
            await providerProfileStore.UpsertAsync(profile, timeProvider.GetUtcNow(), cancellationToken);
            savedProfiles.Add(profile);
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
