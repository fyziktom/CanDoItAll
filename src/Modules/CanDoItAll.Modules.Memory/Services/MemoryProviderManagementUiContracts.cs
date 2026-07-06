using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Modules.Memory.Services;

public sealed record MemoryProviderManagementSnapshot(
    IReadOnlyList<MemoryProviderManagementProfile> Providers,
    MemoryProviderManagementProfile? SelectedProvider,
    IReadOnlyList<MemoryProviderOperationUiRecord> Operations,
    IReadOnlyList<MemoryProviderFeedbackUiRecord> Feedback,
    IReadOnlyList<MemoryProviderEventUiRecord> Events,
    IReadOnlyList<MemoryProviderUiSurfaceProjection> ProviderUiSurfaces)
{
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
    public bool CanRunProviderBackedActions =>
        IsEnabled &&
        HealthState == MemoryProviderHealthState.Healthy &&
        Capabilities.Any(capability =>
            capability.Supported &&
            (capability.Id == MemoryCapabilityIds.ContextQuerySync ||
             capability.Id == MemoryCapabilityIds.ContextQueryAsync));

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
            profile.Manifest.Extensions,
            profile.Manifest.Limits);
    }
}

public enum MemoryProviderUiSurfaceAvailability
{
    Available = 0,
    ProviderUnavailable = 1,
    CapabilityUnavailable = 2,
    MissingComponentRegistration = 3,
    MissingUrl = 4,
    InvalidUrl = 5,
    UnsupportedKind = 6
}

public sealed record MemoryProviderUiSurfaceProjection(
    string SurfaceId,
    MemoryProviderUiSurfaceKind Kind,
    string Name,
    string? ComponentKey,
    string? Url,
    MemoryCapabilityId RequiredCapability,
    MemoryProviderUiSurfaceAvailability Availability,
    string Diagnostic,
    Type? ComponentType)
{
    public bool CanRender => Availability == MemoryProviderUiSurfaceAvailability.Available;
}

public sealed record MemoryProviderUiSurfaceComponentRegistration(
    string ComponentKey,
    Type ComponentType);

public static class MemoryProviderUiSurfaceKeys
{
    public const string MockProviderPanelComponent = "memory.mock.panel";
    public const string ProviderVendorUiUrlExtension = "provider.vendor.uiUrl";
}

public interface IMemoryProviderUiSurfaceComponentRegistry
{
    bool TryResolve(string componentKey, out Type componentType);
}

public sealed class MemoryProviderUiSurfaceComponentRegistry(
    IEnumerable<MemoryProviderUiSurfaceComponentRegistration> registrations) : IMemoryProviderUiSurfaceComponentRegistry
{
    private readonly IReadOnlyDictionary<string, Type> components = registrations
        .GroupBy(registration => registration.ComponentKey, StringComparer.Ordinal)
        .ToDictionary(
            group => group.Key,
            group => group.Last().ComponentType,
            StringComparer.Ordinal);

    public bool TryResolve(string componentKey, out Type componentType) =>
        components.TryGetValue(componentKey, out componentType!);
}

public sealed class MemoryProviderProfileEditorModel
{
    public string InstanceId { get; set; } = "provider.new-memory";

    public string DisplayName { get; set; } = "New memory provider";

    public MemoryProviderDriverKind DriverKind { get; set; } = MemoryProviderDriverKind.Mock;

    public bool IsEnabled { get; set; } = true;

    public MemoryProviderHealthState HealthState { get; set; } = MemoryProviderHealthState.Unknown;

    public MemoryProviderWorkspaceScope WorkspaceScope { get; set; } = MemoryProviderWorkspaceScope.AllWorkspaces;

    public MemoryProviderFallbackBehavior FallbackBehavior { get; set; } = MemoryProviderFallbackBehavior.DenyImplicitFallback;

    public string ProviderKind { get; set; } = "memory.mock";

    public bool SupportsContextQuerySync { get; set; } = true;

    public bool SupportsContextQueryAsync { get; set; }

    public bool SupportsSnapshotIngestion { get; set; }

    public bool SupportsProviderRequestedSources { get; set; }

    public bool SupportsImmediateFeedback { get; set; }

    public bool SupportsDelayedFeedback { get; set; }

    public bool SupportsProviderEvents { get; set; }

    public bool SupportsHostEventPolling { get; set; }

    public bool SupportsOperationStatus { get; set; }

    public bool SupportsRclUi { get; set; }

    public bool SupportsIframeUi { get; set; }

    public string ProviderUiUrl { get; set; } = string.Empty;

    public static MemoryProviderProfileEditorModel FromProfile(MemoryProviderManagementProfile? profile)
    {
        if (profile is null)
        {
            return new MemoryProviderProfileEditorModel();
        }

        var capabilities = profile.Capabilities
            .Where(capability => capability.Supported)
            .Select(capability => capability.Id)
            .ToHashSet();

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
            ProviderUiUrl = profile.Extensions.Values.TryGetValue(MemoryProviderUiSurfaceKeys.ProviderVendorUiUrlExtension, out var uiUrl) &&
                uiUrl.ValueKind == System.Text.Json.JsonValueKind.String
                    ? uiUrl.GetString() ?? string.Empty
                    : string.Empty
        };
    }
}

public sealed class MemoryQueryEditorModel
{
    public string Query { get; set; } = "payment integration";

    public bool UseAsyncQuery { get; set; }

    public string SourceModule { get; set; } = nameof(MemorySourceKind.Project);

    public string SourceRecordId { get; set; } = "project-1";

    public string Citation { get; set; } = "Project 1";
}

public sealed class MemoryFeedbackEditorModel
{
    public string ContextPackId { get; set; } = string.Empty;

    public MemoryFeedbackOutcome Outcome { get; set; } = MemoryFeedbackOutcome.Useful;

    public MemoryFeedbackStage Stage { get; set; } = MemoryFeedbackStage.ContextUsed;

    public string Comment { get; set; } = string.Empty;
}

public sealed class MemoryManualIngestionEditorModel
{
    public string Title { get; set; } = "Manual memory note";

    public string ContentText { get; set; } = "Payment integration context from the generic memory UI.";

    public string SourceCategory { get; set; } = "Manual";

    public string Tags { get; set; } = "memory-ui";
}

public sealed record MemoryProviderOperationUiRecord(
    MemoryOperationId OperationId,
    MemoryProviderInstanceId ProviderInstanceId,
    MemoryCapabilityId RequestedCapability,
    MemoryOperationKind OperationKind,
    MemoryLedgerStatus Status,
    string StatusReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    MemoryOperationAccepted? AcceptedOperation,
    MemoryFeedbackHandle? FeedbackHandle);

public sealed record MemoryProviderFeedbackUiRecord(
    MemoryFeedbackRecordId FeedbackRecordId,
    MemoryProviderInstanceId ProviderInstanceId,
    MemoryFeedbackStage Stage,
    MemoryFeedbackOutcome Outcome,
    MemoryFeedbackMatchState MatchState,
    MemoryLedgerStatus Status,
    string? UnmatchedReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record MemoryProviderEventUiRecord(
    MemoryEventInboxRecordId InboxRecordId,
    MemoryProviderInstanceId ProviderInstanceId,
    MemoryProviderEventId ProviderEventId,
    MemoryProviderEventKind EventKind,
    MemoryEventPriority Priority,
    MemoryLedgerStatus Status,
    string StatusReason,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record MemoryProviderQueryUiResult(
    MemoryOperationHandlerStatus Status,
    string Diagnostic,
    MemoryProviderOperationUiRecord? Operation,
    MemoryContextPack? ContextPack,
    MemoryOperationAccepted? AcceptedOperation,
    MemoryFeedbackHandle? FeedbackHandle,
    bool DriverDispatchAttempted)
{
    public bool HasContextPack => ContextPack is not null;
}

public sealed record MemoryProviderOperationUiResult(
    MemoryOperationHandlerStatus Status,
    string Diagnostic,
    MemoryProviderOperationUiRecord? Operation);

public sealed record MemoryProviderFeedbackUiResult(
    MemoryOperationHandlerStatus Status,
    string Diagnostic,
    MemoryProviderFeedbackUiRecord? Feedback);

public sealed record MemoryProviderManualIngestionUiResult(
    MemoryOperationHandlerStatus Status,
    string Diagnostic,
    Guid JobId,
    MemoryOperationId OperationId,
    string CapturedSnapshotId,
    MemoryProviderOperationUiRecord? Operation);

public sealed record MemoryProviderEventAcknowledgeUiResult(
    MemoryOperationHandlerStatus Status,
    string Diagnostic,
    MemoryProviderEventId EventId);

public interface IMemoryProviderManagementUiService
{
    Task<MemoryProviderManagementSnapshot> GetSnapshotAsync(
        string? selectedProviderInstanceId = null,
        CancellationToken cancellationToken = default);

    Task<MemoryProviderProfile> SaveProviderAsync(
        MemoryProviderProfileEditorModel editor,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryProviderProfile>> CreateDemoProvidersAsync(CancellationToken cancellationToken = default);

    Task<MemoryProviderQueryUiResult> RunQueryAsync(
        string? selectedProviderInstanceId,
        MemoryQueryEditorModel editor,
        CancellationToken cancellationToken = default);

    Task<MemoryProviderOperationUiResult> RefreshOperationAsync(
        string operationId,
        CancellationToken cancellationToken = default);

    Task<MemoryProviderOperationUiResult> CancelOperationAsync(
        string operationId,
        CancellationToken cancellationToken = default);

    Task<MemoryProviderFeedbackUiResult> SubmitFeedbackAsync(
        string? selectedProviderInstanceId,
        MemoryFeedbackEditorModel editor,
        CancellationToken cancellationToken = default);

    Task<MemoryProviderManualIngestionUiResult> EnqueueManualIngestionAsync(
        string? selectedProviderInstanceId,
        MemoryManualIngestionEditorModel editor,
        CancellationToken cancellationToken = default);

    Task<MemoryProviderEventAcknowledgeUiResult> AcknowledgeEventAsync(
        string? selectedProviderInstanceId,
        string providerEventId,
        bool accepted,
        CancellationToken cancellationToken = default);
}
