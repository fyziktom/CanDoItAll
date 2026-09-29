using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Modules.Memory.Services;

public sealed record MemoryProviderManagementSnapshot(
    IReadOnlyList<MemoryProviderManagementProfile> Providers,
    MemoryProviderManagementProfile? SelectedProvider,
    IReadOnlyList<MemoryProviderOperationUiRecord> Operations,
    IReadOnlyList<MemoryProviderFeedbackUiRecord> Feedback,
    IReadOnlyList<MemoryProviderEventUiRecord> Events,
    IReadOnlyList<MemoryProviderUiSurfaceProjection> ProviderUiSurfaces)
{
    public MemoryProviderProfileEditorModel? Editor { get; init; }
    public MemoryProviderRevision? SelectedRevision { get; init; }
    public IReadOnlyList<MemoryReadRegion> FailedRegions { get; init; } = [];

    public int ProviderCount => Providers.Count;

    public int EnabledProviderCount => Providers.Count(provider => provider.IsEnabled);

    public int HealthyProviderCount => Providers.Count(provider => provider.HealthState == MemoryProviderHealthState.Healthy);

    public int UiSurfaceCount => Providers.Sum(provider => provider.UiSurfaces.Count);
}

public sealed record MemoryProviderManagementProfile(
    MemoryProviderInstanceId InstanceId,
    string DisplayName,
    MemoryProviderDriverKind DriverKind,
    bool IsEnabled,
    MemoryProviderHealthState HealthState,
    MemoryProviderWorkspaceScope WorkspaceScope,
    IReadOnlyList<string> SelectionTags,
    MemoryProviderProfilePolicy DefaultPolicy,
    MemoryProviderKind ProviderKind,
    MemoryProtocolVersion ProtocolVersion,
    IReadOnlyList<MemoryCapabilityDescriptor> Capabilities,
    MemoryProviderInteractionSupport InteractionSupport,
    IReadOnlyList<MemoryProviderUiSurface> UiSurfaces,
    MemoryExtensionData Extensions,
    MemoryProviderLimits Limits)
{
    public IReadOnlyList<string> LegacyRawCredentialKeys { get; init; } = [];

    public bool CanRunProviderBackedActions =>
        IsEnabled &&
        HealthState == MemoryProviderHealthState.Healthy &&
        Capabilities.Any(capability =>
            capability.Supported &&
            capability.Id is var id &&
            (id == MemoryCapabilityIds.ContextQuerySync || id == MemoryCapabilityIds.ContextQueryAsync));

    public static MemoryProviderManagementProfile FromProfile(MemoryProviderProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new MemoryProviderManagementProfile(
            profile.InstanceId,
            profile.DisplayName,
            profile.DriverKind,
            profile.IsEnabled,
            profile.HealthState,
            profile.WorkspaceScope,
            profile.SelectionTags.ToArray(),
            profile.DefaultPolicy,
            profile.Manifest.ProviderKind,
            profile.Manifest.ProtocolVersion,
            profile.Manifest.Capabilities.ToArray(),
            profile.Manifest.InteractionSupport,
            profile.Manifest.UiSurfaces.ToArray(),
            MemoryProviderUiUrlPolicy.Sanitize(profile),
            profile.Manifest.Limits) {
            LegacyRawCredentialKeys = new[] { MemoryEditorDefaults.LegacyHttpCredential, MemoryEditorDefaults.LegacyNativeCredential }.Where(profile.Manifest.Extensions.Values.ContainsKey).ToArray()
        };
    }
}
