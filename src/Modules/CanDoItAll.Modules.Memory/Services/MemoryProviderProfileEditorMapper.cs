using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Http;
using CanDoItAll.Memory.Mcp;

namespace CanDoItAll.Modules.Memory.Services;

public sealed class MemoryProviderProfileEditorMapper
{
    public static MemoryProviderProfileEditorMapper Default { get; } = new();

    public MemoryProviderProfileEditorModel FromProfile(MemoryProviderManagementProfile? profile)
    {
        if (profile is null)
        {
            return new MemoryProviderProfileEditorModel();
        }

        var capabilities = profile.Capabilities
            .Where(capability => capability.Supported)
            .Select(capability => capability.Id)
            .ToHashSet();
        var sanitizedExtensions = MemoryProviderExtensionEditorMapper.SanitizePreserved(profile.Extensions);

        return new MemoryProviderProfileEditorModel
        {
            InstanceId = profile.InstanceId.Value,
            DisplayName = profile.DisplayName,
            DriverKind = profile.DriverKind,
            IsEnabled = profile.IsEnabled,
            HealthState = profile.HealthState,
            WorkspaceScope = profile.WorkspaceScope,
            FallbackBehavior = profile.DefaultPolicy.FallbackBehavior,
            ProviderKind = profile.ProviderKind.Value,
            SupportsContextQuerySync = capabilities.Contains(MemoryCapabilityIds.ContextQuerySync),
            SupportsContextQueryAsync = capabilities.Contains(MemoryCapabilityIds.ContextQueryAsync),
            SupportsSnapshotIngestion = capabilities.Contains(MemoryCapabilityIds.IngestionSnapshot),
            SupportsProviderRequestedSources = capabilities.Contains(MemoryCapabilityIds.IngestionProviderRequestedSource),
            SupportsImmediateFeedback = capabilities.Contains(MemoryCapabilityIds.FeedbackImmediate),
            SupportsDelayedFeedback = capabilities.Contains(MemoryCapabilityIds.FeedbackDelayed),
            SupportsProviderEvents = capabilities.Contains(MemoryCapabilityIds.EventsProviderPush),
            SupportsHostEventPolling = capabilities.Contains(MemoryCapabilityIds.EventsHostPoll),
            SupportsOperationStatus = capabilities.Contains(MemoryCapabilityIds.OperationStatus),
            SupportsRclUi = capabilities.Contains(MemoryCapabilityIds.UiRcl),
            SupportsIframeUi = capabilities.Contains(MemoryCapabilityIds.UiIframe),
            ProviderUiUrl = MemoryProviderExtensionEditorMapper.ReadProviderUiUrl(profile.Extensions.Values),
            SelectionTags = profile.SelectionTags.ToList(),
            Http = MemoryProviderHttpExtensionCodec.Read(profile.DriverKind, profile.Extensions.Values),
            Mcp = MemoryProviderMcpExtensionCodec.Read(profile.Extensions.Values),
            PreservedCapabilities = profile.Capabilities.ToArray(),
            PreservedUiSurfaces = profile.UiSurfaces.ToArray(),
            PreservedExtensions = sanitizedExtensions,
            PreservedLimits = profile.Limits,
            PreservedProtocolVersion = profile.ProtocolVersion,
            PreservedInteractionSupport = profile.InteractionSupport,
            LegacyRawCredentialKeys = MemoryProviderExtensionEditorMapper.FindLegacyRawCredentialKeys(profile.Extensions)
        };
    }

    public MemoryProviderProfile ToProfile(MemoryProviderProfileEditorModel editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (editor.WorkspaceScope != MemoryProviderWorkspaceScope.AllWorkspaces)
        {
            throw new InvalidOperationException(
                "Memory providers must use the all-workspaces scope because workspace-bound provider routing is not implemented.");
        }

        ValidateLegacyCredentialMigration(editor);
        MemoryProviderCapabilityPolicy.Validate(editor);
        var profile = new MemoryProviderProfile(
            MemoryProviderInstanceId.Parse(editor.InstanceId),
            MemoryProviderUiText.Normalize(editor.DisplayName, nameof(editor.DisplayName)),
            editor.DriverKind,
            editor.IsEnabled,
            editor.HealthState,
            editor.WorkspaceScope,
            NormalizeSelectionTags(editor.SelectionTags),
            new MemoryProviderProfilePolicy(editor.FallbackBehavior),
            new MemoryProviderManifest(
                MemoryProviderKind.Parse(editor.ProviderKind),
                editor.PreservedProtocolVersion,
                MemoryProviderManifestEditorMapper.BuildCapabilities(editor),
                MemoryProviderManifestEditorMapper.BuildInteractionSupport(editor),
                MemoryProviderManifestEditorMapper.BuildUiSurfaces(editor),
                editor.PreservedLimits,
                MemoryProviderExtensionEditorMapper.Build(editor)));

        ValidateTransport(profile);
        return profile;
    }

    private static IReadOnlyList<string> NormalizeSelectionTags(IEnumerable<string> tags)
    {
        return tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .ToArray();
    }

    private static void ValidateTransport(MemoryProviderProfile profile)
    {
        switch (profile.DriverKind)
        {
            case MemoryProviderDriverKind.Http:
                _ = HttpMemoryProviderConfiguration.FromProfile(profile, new HttpMemoryProviderOptions());
                break;
            case MemoryProviderDriverKind.Mcp:
                _ = McpMemoryProviderConfiguration.FromProfile(profile, new McpMemoryProviderOptions());
                break;
        }
    }

    private static void ValidateLegacyCredentialMigration(MemoryProviderProfileEditorModel editor)
    {
        var legacyKeys = editor.LegacyRawCredentialKeys
            .Concat(MemoryProviderExtensionEditorMapper.FindLegacyRawCredentialKeys(editor.PreservedExtensions))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (legacyKeys.Length == 0)
        {
            return;
        }

        var environmentVariable = editor.DriverKind switch
        {
            MemoryProviderDriverKind.Http or MemoryProviderDriverKind.NativeRemote => editor.Http.ApiKeyEnvironmentVariable,
            MemoryProviderDriverKind.Mcp => editor.Mcp.AuthHeaderEnvironmentVariable,
            _ => string.Empty
        };
        if (string.IsNullOrWhiteSpace(environmentVariable))
        {
            throw new InvalidOperationException(
                $"Provider contains legacy raw credential extension(s) '{string.Join("', '", legacyKeys)}'. " +
                "Configure a credential environment-variable reference before saving; raw credentials are never persisted.");
        }
    }
}
