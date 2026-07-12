using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Modules.Memory.Services;

internal static class MemoryProviderManifestEditorMapper
{
    private static readonly IReadOnlyList<(MemoryCapabilityId Id, Func<MemoryProviderProfileEditorModel, bool> IsEnabled)> ManagedCapabilities =
    [
        (MemoryCapabilityIds.ContextQuerySync, editor => editor.SupportsContextQuerySync),
        (MemoryCapabilityIds.ContextQueryAsync, editor => editor.SupportsContextQueryAsync),
        (MemoryCapabilityIds.IngestionSnapshot, editor => editor.SupportsSnapshotIngestion),
        (MemoryCapabilityIds.IngestionProviderRequestedSource, editor => editor.SupportsProviderRequestedSources),
        (MemoryCapabilityIds.FeedbackImmediate, editor => editor.SupportsImmediateFeedback),
        (MemoryCapabilityIds.FeedbackDelayed, editor => editor.SupportsDelayedFeedback),
        (MemoryCapabilityIds.EventsProviderPush, editor => editor.SupportsProviderEvents),
        (MemoryCapabilityIds.EventsHostPoll, editor => editor.SupportsHostEventPolling),
        (MemoryCapabilityIds.OperationStatus, editor => editor.SupportsOperationStatus),
        (MemoryCapabilityIds.UiRcl, editor => editor.SupportsRclUi),
        (MemoryCapabilityIds.UiIframe, editor => editor.SupportsIframeUi)
    ];

    private static readonly IReadOnlySet<MemoryCapabilityId> ManagedCapabilityIds = ManagedCapabilities
        .Select(capability => capability.Id)
        .ToHashSet();

    public static IReadOnlyList<MemoryCapabilityDescriptor> BuildCapabilities(MemoryProviderProfileEditorModel editor)
    {
        if (!ManagedCapabilitiesChanged(editor) && editor.PreservedCapabilities.Count > 0)
        {
            return editor.PreservedCapabilities.ToArray();
        }

        var capabilities = editor.PreservedCapabilities
            .Where(capability => !ManagedCapabilityIds.Contains(capability.Id))
            .ToList();

        foreach (var (id, isEnabled) in ManagedCapabilities)
        {
            var preserved = editor.PreservedCapabilities
                .Where(capability => capability.Id == id)
                .ToArray();
            if (isEnabled(editor))
            {
                capabilities.Add(
                    preserved.LastOrDefault(capability => capability.Supported) ??
                    new MemoryCapabilityDescriptor(id, Version: "1", Supported: true));
                continue;
            }

            capabilities.AddRange(preserved.Where(capability => !capability.Supported));
        }

        return capabilities.ToArray();
    }

    public static IReadOnlyList<MemoryProviderUiSurface> BuildUiSurfaces(MemoryProviderProfileEditorModel editor)
    {
        if (!ManagedCapabilitiesChanged(editor) && editor.PreservedUiSurfaces.Count > 0)
        {
            return editor.PreservedUiSurfaces.ToArray();
        }

        var surfaces = editor.PreservedUiSurfaces
            .Where(surface => surface.CapabilityId != MemoryCapabilityIds.UiRcl &&
                              surface.CapabilityId != MemoryCapabilityIds.UiIframe)
            .ToList();
        AddManagedSurface(
            MemoryCapabilityIds.UiRcl,
            editor.SupportsRclUi,
            () => new MemoryProviderUiSurface(
                MemoryProviderUiSurfaceKind.RazorComponentLibrary,
                "Provider panel",
                ComponentKey: $"{editor.ProviderKind}.panel",
                UrlSettingKey: null,
                MemoryCapabilityIds.UiRcl));
        AddManagedSurface(
            MemoryCapabilityIds.UiIframe,
            editor.SupportsIframeUi,
            () => new MemoryProviderUiSurface(
                MemoryProviderUiSurfaceKind.Iframe,
                "Provider console",
                ComponentKey: null,
                UrlSettingKey: MemoryProviderUiSurfaceKeys.ProviderVendorUiUrlExtension,
                MemoryCapabilityIds.UiIframe));
        return surfaces.ToArray();

        void AddManagedSurface(
            MemoryCapabilityId capabilityId,
            bool enabled,
            Func<MemoryProviderUiSurface> createDefault)
        {
            var preserved = editor.PreservedUiSurfaces
                .Where(surface => surface.CapabilityId == capabilityId)
                .ToArray();
            var originallyEnabled = editor.PreservedCapabilities.Any(capability =>
                capability.Id == capabilityId && capability.Supported);
            if (enabled)
            {
                surfaces.AddRange(preserved.Length > 0 ? preserved : [createDefault()]);
            }
            else if (!originallyEnabled)
            {
                surfaces.AddRange(preserved);
            }
        }
    }

    public static MemoryProviderInteractionSupport BuildInteractionSupport(MemoryProviderProfileEditorModel editor)
    {
        return new MemoryProviderInteractionSupport(
            editor.SupportsContextQuerySync,
            editor.SupportsContextQueryAsync,
            editor.SupportsProviderRequestedSources,
            editor.SupportsImmediateFeedback || editor.SupportsDelayedFeedback,
            editor.SupportsProviderEvents || editor.SupportsHostEventPolling);
    }

    private static bool ManagedCapabilitiesChanged(MemoryProviderProfileEditorModel editor) =>
        ManagedCapabilities.Any(capability =>
            capability.IsEnabled(editor) != editor.PreservedCapabilities.Any(preserved =>
                preserved.Id == capability.Id && preserved.Supported));
}
