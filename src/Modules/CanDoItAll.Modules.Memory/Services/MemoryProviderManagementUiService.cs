using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using System.Text.Json;

namespace CanDoItAll.Modules.Memory.Services;

public sealed class MemoryProviderManagementUiService(
    IMemoryProviderProfileStore providerProfileStore,
    IMemoryOperationLedgerStore operationLedgerStore,
    IMemoryFeedbackLedgerStore feedbackLedgerStore,
    IMemoryEventLedgerStore eventLedgerStore,
    IMemoryOperationHandler operationHandler,
    ManualMemorySourceIngestionService manualIngestionService,
    IMemoryProviderUiSurfaceComponentRegistry uiSurfaceComponentRegistry,
    TimeProvider timeProvider) : IMemoryProviderManagementUiService
{
    public async Task<MemoryProviderManagementSnapshot> GetSnapshotAsync(
        string? selectedProviderInstanceId = null,
        CancellationToken cancellationToken = default)
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

        var operations = Array.Empty<MemoryProviderOperationUiRecord>();
        var feedback = Array.Empty<MemoryProviderFeedbackUiRecord>();
        var events = Array.Empty<MemoryProviderEventUiRecord>();
        var providerUiSurfaces = Array.Empty<MemoryProviderUiSurfaceProjection>();
        if (selectedProvider is not null)
        {
            operations = (await operationLedgerStore.ListByProviderAsync(selectedProvider.InstanceId, cancellationToken: cancellationToken))
                .Select(ToUiRecord)
                .ToArray();
            feedback = (await feedbackLedgerStore.ListByProviderAsync(selectedProvider.InstanceId, cancellationToken))
                .OrderByDescending(record => record.UpdatedAtUtc)
                .Select(ToUiRecord)
                .ToArray();
            events = (await eventLedgerStore.ListPendingInboxAsync(selectedProvider.InstanceId, cancellationToken: cancellationToken))
                .OrderByDescending(record => record.UpdatedAtUtc)
                .Select(ToUiRecord)
                .ToArray();
            providerUiSurfaces = ProjectUiSurfaces(selectedProvider);
        }

        return new MemoryProviderManagementSnapshot(viewProfiles, selectedProvider, operations, feedback, events, providerUiSurfaces);
    }

    public async Task<MemoryProviderProfile> SaveProviderAsync(
        MemoryProviderProfileEditorModel editor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(editor);

        var profile = BuildProfile(editor);
        await providerProfileStore.UpsertAsync(profile, timeProvider.GetUtcNow(), cancellationToken);
        return profile;
    }

    public async Task<IReadOnlyList<MemoryProviderProfile>> CreateDemoProvidersAsync(CancellationToken cancellationToken = default)
    {
        var existingProfiles = await providerProfileStore.ListAsync(cancellationToken);
        var existingIds = existingProfiles
            .Select(profile => profile.InstanceId.Value)
            .ToHashSet(StringComparer.Ordinal);
        var demoProfiles = new[]
        {
            CreateDemoProvider(
                "provider.business-demo",
                "Business demo memory",
                MemoryProviderHealthState.Healthy,
                [MemoryCapabilityIds.ContextQuerySync, MemoryCapabilityIds.FeedbackImmediate, MemoryCapabilityIds.EventsHostPoll]),
            CreateDemoProvider(
                "provider.programming-demo",
                "Programming demo memory",
                MemoryProviderHealthState.Degraded,
                [MemoryCapabilityIds.ContextQuerySync, MemoryCapabilityIds.ContextQueryAsync, MemoryCapabilityIds.OperationStatus])
        };

        var savedProfiles = new List<MemoryProviderProfile>();
        foreach (var profile in demoProfiles)
        {
            if (existingIds.Contains(profile.InstanceId.Value))
            {
                continue;
            }

            await providerProfileStore.UpsertAsync(profile, timeProvider.GetUtcNow(), cancellationToken);
            savedProfiles.Add(profile);
        }

        return savedProfiles;
    }

    public async Task<MemoryProviderQueryUiResult> RunQueryAsync(
        string? selectedProviderInstanceId,
        MemoryQueryEditorModel editor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(editor);

        var requiredCapability = editor.UseAsyncQuery
            ? MemoryCapabilityIds.ContextQueryAsync
            : MemoryCapabilityIds.ContextQuerySync;
        var request = MemoryOperationRequestBuilder.Query(
            CreateCaller("memory.ui.query"),
            CreateSelectionPolicy(selectedProviderInstanceId, requiredCapability),
            new MemoryContextQueryRequest(
                MemoryText.Normalize(editor.Query, nameof(editor.Query)),
                [requiredCapability],
                CreateSourceProvenance(editor)),
            CreateRetentionPolicy());
        var result = await operationHandler.ExecuteQueryAsync(request, cancellationToken);

        return new MemoryProviderQueryUiResult(
            result.Status,
            result.Diagnostic,
            result.OperationRecord is null ? null : ToUiRecord(result.OperationRecord),
            result.Output,
            result.AcceptedOperation,
            result.FeedbackHandle,
            result.DriverDispatchAttempted);
    }

    public async Task<MemoryProviderOperationUiResult> RefreshOperationAsync(
        string operationId,
        CancellationToken cancellationToken = default)
    {
        var request = MemoryOperationRequestBuilder.Status(
            CreateCaller("memory.ui.operation.status"),
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.OperationStatus),
            new MemoryOperationStatusRequest(ParseOperationId(operationId)),
            CreateRetentionPolicy());
        var result = await operationHandler.GetStatusAsync(request, cancellationToken);

        return new MemoryProviderOperationUiResult(
            result.Status,
            result.Diagnostic,
            result.Output is null ? null : ToUiRecord(result.Output));
    }

    public async Task<MemoryProviderOperationUiResult> CancelOperationAsync(
        string operationId,
        CancellationToken cancellationToken = default)
    {
        var request = MemoryOperationRequestBuilder.Cancellation(
            CreateCaller("memory.ui.operation.cancel"),
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.OperationStatus),
            new MemoryOperationCancellationRequest(ParseOperationId(operationId), "User cancelled operation from Memory UI."),
            CreateRetentionPolicy());
        var result = await operationHandler.CancelAsync(request, cancellationToken);

        return new MemoryProviderOperationUiResult(
            result.Status,
            result.Diagnostic,
            result.Output is null ? null : ToUiRecord(result.Output));
    }

    public async Task<MemoryProviderFeedbackUiResult> SubmitFeedbackAsync(
        string? selectedProviderInstanceId,
        MemoryFeedbackEditorModel editor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var requiredCapability = editor.Stage is MemoryFeedbackStage.ContextUsed or MemoryFeedbackStage.ImmediateToolResult
            ? MemoryCapabilityIds.FeedbackImmediate
            : MemoryCapabilityIds.FeedbackDelayed;
        var request = new MemoryOperationHandlerRequest<MemoryFeedbackOperationRequest>(
            CreateCaller("memory.ui.feedback.submit"),
            CreateSelectionPolicy(selectedProviderInstanceId, requiredCapability),
            MemoryOperationKind.Feedback,
            SourceSnapshotIds: [],
            CreateRetentionPolicy(),
            new MemoryFeedbackOperationRequest(
                new MemoryFeedbackRequest(
                    ParseContextPackId(editor.ContextPackId),
                    editor.Outcome,
                    string.IsNullOrWhiteSpace(editor.Comment) ? null : editor.Comment.Trim(),
                    EconomicImpact: null),
                editor.Stage,
                "Feedback was submitted without a persisted context delivery record."));
        var result = await operationHandler.SubmitFeedbackAsync(request, cancellationToken);

        return new MemoryProviderFeedbackUiResult(
            result.Status,
            result.Diagnostic,
            result.Output is null ? null : ToUiRecord(result.Output));
    }

    public async Task<MemoryProviderManualIngestionUiResult> EnqueueManualIngestionAsync(
        string? selectedProviderInstanceId,
        MemoryManualIngestionEditorModel editor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (string.IsNullOrWhiteSpace(selectedProviderInstanceId))
        {
            throw new InvalidOperationException("Select a provider before enqueueing manual ingestion.");
        }

        var result = await manualIngestionService.EnqueueAsync(
            new ManualMemorySourceIngestionRequest(
                MemoryProviderInstanceId.Parse(selectedProviderInstanceId),
                ManualMemorySourcePayload.Text(
                    MemoryText.Normalize(editor.Title, nameof(editor.Title)),
                    MemoryText.Normalize(editor.ContentText, nameof(editor.ContentText)),
                    MemoryText.Normalize(editor.SourceCategory, nameof(editor.SourceCategory)),
                    SplitTags(editor.Tags)),
                RequestedBy: "memory-ui",
                CreateRequester(),
                CreateRetentionPolicy()),
            cancellationToken);
        var operation = await operationLedgerStore.GetAsync(result.OperationId, cancellationToken);

        return new MemoryProviderManualIngestionUiResult(
            MemoryOperationHandlerStatus.Accepted,
            "Source snapshot captured and queued for provider ingestion.",
            result.JobId,
            result.OperationId,
            result.CapturedSnapshotId.Value,
            operation is null ? null : ToUiRecord(operation));
    }

    public async Task<MemoryProviderEventAcknowledgeUiResult> AcknowledgeEventAsync(
        string? selectedProviderInstanceId,
        string providerEventId,
        bool accepted,
        CancellationToken cancellationToken = default)
    {
        var eventId = ParseProviderEventId(providerEventId);
        var request = MemoryOperationRequestBuilder.EventAcknowledge(
            CreateCaller("memory.ui.event.acknowledge"),
            CreateSelectionPolicy(selectedProviderInstanceId, MemoryCapabilityIds.EventsProviderPush),
            new MemoryEventAcknowledgeRequest(eventId, accepted, accepted ? "Accepted from Memory UI." : "Rejected from Memory UI."),
            CreateRetentionPolicy());
        var result = await operationHandler.AcknowledgeEventAsync(request, cancellationToken);

        return new MemoryProviderEventAcknowledgeUiResult(
            result.Status,
            result.Diagnostic,
            eventId);
    }

    private static MemoryProviderProfile BuildProfile(MemoryProviderProfileEditorModel editor)
    {
        var capabilities = BuildCapabilities(editor);
        var uiSurfaces = BuildUiSurfaces(editor);
        var extensions = BuildExtensions(editor);

        return new MemoryProviderProfile(
            MemoryProviderInstanceId.Parse(editor.InstanceId),
            MemoryText.Normalize(editor.DisplayName, nameof(editor.DisplayName)),
            editor.DriverKind,
            editor.IsEnabled,
            editor.HealthState,
            editor.WorkspaceScope,
            SelectionTags: [],
            new MemoryProviderProfilePolicy(editor.FallbackBehavior),
            new MemoryProviderManifest(
                MemoryProviderKind.Parse(editor.ProviderKind),
                MemoryProtocolVersion.Current,
                capabilities,
                new MemoryProviderInteractionSupport(
                    editor.SupportsContextQuerySync,
                    editor.SupportsContextQueryAsync,
                    editor.SupportsProviderRequestedSources,
                    editor.SupportsImmediateFeedback || editor.SupportsDelayedFeedback,
                    editor.SupportsProviderEvents || editor.SupportsHostEventPolling),
                uiSurfaces,
                MemoryProviderLimits.Default,
                extensions));
    }

    private static MemorySourceProvenance CreateSourceProvenance(MemoryQueryEditorModel editor)
    {
        var sourceRecordIds = string.IsNullOrWhiteSpace(editor.SourceRecordId)
            ? Array.Empty<string>()
            : new[] { editor.SourceRecordId.Trim() };
        var citations = string.IsNullOrWhiteSpace(editor.Citation)
            ? Array.Empty<string>()
            : new[] { editor.Citation.Trim() };

        return new MemorySourceProvenance(
            MemorySourceSnapshotId.Parse("memory-ui.query"),
            string.IsNullOrWhiteSpace(editor.SourceModule) ? null : editor.SourceModule.Trim(),
            sourceRecordIds,
            citations);
    }

    private static MemoryProviderSelectionPolicy CreateSelectionPolicy(
        string? providerInstanceId,
        MemoryCapabilityId requiredCapability)
    {
        var policy = MemoryProviderSelectionPolicy.RequireCapability(requiredCapability);
        return string.IsNullOrWhiteSpace(providerInstanceId)
            ? policy
            : policy with
            {
                ExplicitProviderId = MemoryProviderInstanceId.Parse(providerInstanceId)
            };
    }

    private MemoryLedgerRetentionPolicy CreateRetentionPolicy()
    {
        var now = timeProvider.GetUtcNow();
        return MemoryLedgerRetentionPolicy.Expiring(now.AddDays(7), now.AddDays(30));
    }

    private static MemoryOperationCaller CreateCaller(string route)
    {
        return MemoryOperationCaller.UiAction(route, CreateRequester());
    }

    private static MemoryLedgerRequester CreateRequester() =>
        new(
            RequesterId: "memory-ui",
            AgentId: null,
            AgentRole: null,
            SessionId: "memory-ui-session",
            WorkflowId: null,
            WorkflowNodeId: null,
            ProcessId: null,
            ProcessStepId: null);

    private static IReadOnlyList<string> SplitTags(string tags)
    {
        return string.IsNullOrWhiteSpace(tags)
            ? []
            : tags
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(tag => tag.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    private static MemoryProviderOperationUiRecord ToUiRecord(MemoryOperationRecord record)
    {
        return new MemoryProviderOperationUiRecord(
            record.OperationId,
            record.ProviderInstanceId,
            record.RequestedCapability,
            record.OperationKind,
            record.Status,
            record.StatusReason,
            record.CreatedAtUtc,
            record.UpdatedAtUtc,
            record.CompletedAtUtc,
            record.Extensions.GetAcceptedOperation(),
            record.Extensions.GetContextDelivery()?.FeedbackHandle);
    }

    private static MemoryProviderFeedbackUiRecord ToUiRecord(MemoryFeedbackRecord record)
    {
        return new MemoryProviderFeedbackUiRecord(
            record.FeedbackRecordId,
            record.ProviderInstanceId,
            record.Stage,
            record.Outcome,
            record.MatchState,
            record.Status,
            record.UnmatchedReason,
            record.CreatedAtUtc,
            record.UpdatedAtUtc);
    }

    private static MemoryProviderEventUiRecord ToUiRecord(MemoryEventInboxRecord record)
    {
        return new MemoryProviderEventUiRecord(
            record.InboxRecordId,
            record.ProviderInstanceId,
            record.ProviderEventId,
            record.EventKind,
            record.Priority,
            record.Status,
            record.StatusReason,
            record.ReceivedAtUtc,
            record.UpdatedAtUtc);
    }

    private static MemoryOperationId ParseOperationId(string operationId)
    {
        return Guid.TryParse(operationId, out var parsed)
            ? new MemoryOperationId(parsed)
            : throw new ArgumentException("Operation id must be a valid GUID.", nameof(operationId));
    }

    private static MemoryContextPackId ParseContextPackId(string contextPackId)
    {
        return Guid.TryParse(contextPackId, out var parsed)
            ? new MemoryContextPackId(parsed)
            : throw new ArgumentException("Context pack id must be a valid GUID.", nameof(contextPackId));
    }

    private static MemoryProviderEventId ParseProviderEventId(string providerEventId)
    {
        return Guid.TryParse(providerEventId, out var parsed)
            ? new MemoryProviderEventId(parsed)
            : throw new ArgumentException("Provider event id must be a valid GUID.", nameof(providerEventId));
    }

    private static IReadOnlyList<MemoryCapabilityDescriptor> BuildCapabilities(MemoryProviderProfileEditorModel editor)
    {
        var capabilities = new List<MemoryCapabilityId>();
        AddIf(editor.SupportsContextQuerySync, MemoryCapabilityIds.ContextQuerySync);
        AddIf(editor.SupportsContextQueryAsync, MemoryCapabilityIds.ContextQueryAsync);
        AddIf(editor.SupportsSnapshotIngestion, MemoryCapabilityIds.IngestionSnapshot);
        AddIf(editor.SupportsProviderRequestedSources, MemoryCapabilityIds.IngestionProviderRequestedSource);
        AddIf(editor.SupportsImmediateFeedback, MemoryCapabilityIds.FeedbackImmediate);
        AddIf(editor.SupportsDelayedFeedback, MemoryCapabilityIds.FeedbackDelayed);
        AddIf(editor.SupportsProviderEvents, MemoryCapabilityIds.EventsProviderPush);
        AddIf(editor.SupportsHostEventPolling, MemoryCapabilityIds.EventsHostPoll);
        AddIf(editor.SupportsOperationStatus, MemoryCapabilityIds.OperationStatus);
        AddIf(editor.SupportsRclUi, MemoryCapabilityIds.UiRcl);
        AddIf(editor.SupportsIframeUi, MemoryCapabilityIds.UiIframe);

        return capabilities
            .Distinct()
            .Select(capability => new MemoryCapabilityDescriptor(capability, Version: "1", Supported: true))
            .ToArray();

        void AddIf(bool condition, MemoryCapabilityId capability)
        {
            if (condition)
            {
                capabilities.Add(capability);
            }
        }
    }

    private static IReadOnlyList<MemoryProviderUiSurface> BuildUiSurfaces(MemoryProviderProfileEditorModel editor)
    {
        var surfaces = new List<MemoryProviderUiSurface>();
        if (editor.SupportsRclUi)
        {
            surfaces.Add(new MemoryProviderUiSurface(
                MemoryProviderUiSurfaceKind.RazorComponentLibrary,
                "Provider panel",
                ComponentKey: $"{editor.ProviderKind}.panel",
                UrlSettingKey: null,
                MemoryCapabilityIds.UiRcl));
        }

        if (editor.SupportsIframeUi)
        {
            surfaces.Add(new MemoryProviderUiSurface(
                MemoryProviderUiSurfaceKind.Iframe,
                "Provider console",
                ComponentKey: null,
                UrlSettingKey: MemoryProviderUiSurfaceKeys.ProviderVendorUiUrlExtension,
                MemoryCapabilityIds.UiIframe));
        }

        return surfaces.ToArray();
    }

    private static MemoryProviderProfile CreateDemoProvider(
        string instanceId,
        string displayName,
        MemoryProviderHealthState healthState,
        IReadOnlyList<MemoryCapabilityId> capabilities)
    {
        var editor = new MemoryProviderProfileEditorModel
        {
            InstanceId = instanceId,
            DisplayName = displayName,
            DriverKind = MemoryProviderDriverKind.Mock,
            IsEnabled = true,
            HealthState = healthState,
            WorkspaceScope = MemoryProviderWorkspaceScope.AllWorkspaces,
            ProviderKind = "memory.mock",
            SupportsContextQuerySync = capabilities.Contains(MemoryCapabilityIds.ContextQuerySync),
            SupportsContextQueryAsync = capabilities.Contains(MemoryCapabilityIds.ContextQueryAsync),
            SupportsImmediateFeedback = capabilities.Contains(MemoryCapabilityIds.FeedbackImmediate),
            SupportsHostEventPolling = capabilities.Contains(MemoryCapabilityIds.EventsHostPoll),
            SupportsOperationStatus = capabilities.Contains(MemoryCapabilityIds.OperationStatus)
        };

        return BuildProfile(editor);
    }

    private MemoryProviderUiSurfaceProjection[] ProjectUiSurfaces(MemoryProviderManagementProfile provider)
    {
        if (provider.UiSurfaces.Count == 0)
        {
            return [];
        }

        var supportedCapabilities = provider.Capabilities
            .Where(capability => capability.Supported)
            .Select(capability => capability.Id)
            .ToHashSet();

        return provider.UiSurfaces
            .Select((surface, index) => ProjectUiSurface(provider, surface, supportedCapabilities, index))
            .ToArray();
    }

    private MemoryProviderUiSurfaceProjection ProjectUiSurface(
        MemoryProviderManagementProfile provider,
        MemoryProviderUiSurface surface,
        ISet<MemoryCapabilityId> supportedCapabilities,
        int index)
    {
        var name = string.IsNullOrWhiteSpace(surface.Name)
            ? surface.Kind.ToString()
            : surface.Name.Trim();
        var surfaceId = ToSurfaceId(index, name);
        if (!provider.IsEnabled || provider.HealthState != MemoryProviderHealthState.Healthy)
        {
            return Unavailable(
                surface,
                surfaceId,
                name,
                MemoryProviderUiSurfaceAvailability.ProviderUnavailable,
                "Selected provider must be enabled and healthy before provider UI can render.");
        }

        if (!supportedCapabilities.Contains(surface.CapabilityId))
        {
            return Unavailable(
                surface,
                surfaceId,
                name,
                MemoryProviderUiSurfaceAvailability.CapabilityUnavailable,
                $"Required capability '{surface.CapabilityId.Value}' is not declared by the selected provider.");
        }

        return surface.Kind switch
        {
            MemoryProviderUiSurfaceKind.RazorComponentLibrary => ProjectRclSurface(surface, surfaceId, name),
            MemoryProviderUiSurfaceKind.Iframe => ProjectUrlSurface(surface, provider.Extensions, surfaceId, name),
            MemoryProviderUiSurfaceKind.ExternalUrl => ProjectUrlSurface(surface, provider.Extensions, surfaceId, name),
            _ => Unavailable(
                surface,
                surfaceId,
                name,
                MemoryProviderUiSurfaceAvailability.UnsupportedKind,
                "Provider UI surface kind is not supported.")
        };

        MemoryProviderUiSurfaceProjection Unavailable(
            MemoryProviderUiSurface unavailableSurface,
            string unavailableSurfaceId,
            string unavailableName,
            MemoryProviderUiSurfaceAvailability availability,
            string diagnostic) =>
            new(
                unavailableSurfaceId,
                unavailableSurface.Kind,
                unavailableName,
                unavailableSurface.ComponentKey,
                Url: null,
                unavailableSurface.CapabilityId,
                availability,
                diagnostic,
                ComponentType: null);
    }

    private MemoryProviderUiSurfaceProjection ProjectRclSurface(
        MemoryProviderUiSurface surface,
        string surfaceId,
        string name)
    {
        if (string.IsNullOrWhiteSpace(surface.ComponentKey))
        {
            return new MemoryProviderUiSurfaceProjection(
                surfaceId,
                surface.Kind,
                name,
                ComponentKey: null,
                Url: null,
                surface.CapabilityId,
                MemoryProviderUiSurfaceAvailability.MissingComponentRegistration,
                "Provider UI surface did not declare a component key.",
                ComponentType: null);
        }

        var componentKey = surface.ComponentKey.Trim();
        if (!uiSurfaceComponentRegistry.TryResolve(componentKey, out var componentType))
        {
            return new MemoryProviderUiSurfaceProjection(
                surfaceId,
                surface.Kind,
                name,
                componentKey,
                Url: null,
                surface.CapabilityId,
                MemoryProviderUiSurfaceAvailability.MissingComponentRegistration,
                $"No RCL component is registered for '{componentKey}'.",
                ComponentType: null);
        }

        return new MemoryProviderUiSurfaceProjection(
            surfaceId,
            surface.Kind,
            name,
            componentKey,
            Url: null,
            surface.CapabilityId,
            MemoryProviderUiSurfaceAvailability.Available,
            "Provider RCL surface is available.",
            componentType);
    }

    private MemoryProviderUiSurfaceProjection ProjectUrlSurface(
        MemoryProviderUiSurface surface,
        MemoryExtensionData extensions,
        string surfaceId,
        string name)
    {
        if (string.IsNullOrWhiteSpace(surface.UrlSettingKey))
        {
            return new MemoryProviderUiSurfaceProjection(
                surfaceId,
                surface.Kind,
                name,
                surface.ComponentKey,
                Url: null,
                surface.CapabilityId,
                MemoryProviderUiSurfaceAvailability.MissingUrl,
                "Provider UI surface did not declare a URL setting key.",
                ComponentType: null);
        }

        if (!TryGetExtensionString(surface.UrlSettingKey.Trim(), out var configuredUrl))
        {
            return new MemoryProviderUiSurfaceProjection(
                surfaceId,
                surface.Kind,
                name,
                surface.ComponentKey,
                Url: null,
                surface.CapabilityId,
                MemoryProviderUiSurfaceAvailability.MissingUrl,
                "Provider UI URL is not configured.",
                ComponentType: null);
        }

        if (!TryNormalizeProviderUiUrl(configuredUrl, out var safeUrl))
        {
            return new MemoryProviderUiSurfaceProjection(
                surfaceId,
                surface.Kind,
                name,
                surface.ComponentKey,
                Url: null,
                surface.CapabilityId,
                MemoryProviderUiSurfaceAvailability.InvalidUrl,
                "Provider UI URL must use HTTPS or loopback HTTP.",
                ComponentType: null);
        }

        return new MemoryProviderUiSurfaceProjection(
            surfaceId,
            surface.Kind,
            name,
            surface.ComponentKey,
            safeUrl,
            surface.CapabilityId,
            MemoryProviderUiSurfaceAvailability.Available,
            "Provider URL surface is available.",
            ComponentType: null);

        bool TryGetExtensionString(string key, out string value)
        {
            value = string.Empty;
            if (!extensions.Values.TryGetValue(key, out var element) ||
                element.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            value = element.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }
    }

    private static bool TryNormalizeProviderUiUrl(string configuredUrl, out string safeUrl)
    {
        safeUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(configuredUrl) ||
            !Uri.TryCreate(configuredUrl.Trim(), UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        var isHttps = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        var isLoopbackHttp = string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            uri.IsLoopback;
        if (!isHttps && !isLoopbackHttp)
        {
            return false;
        }

        safeUrl = uri.AbsoluteUri;
        return true;
    }

    private static MemoryExtensionData BuildExtensions(MemoryProviderProfileEditorModel editor)
    {
        if (!editor.SupportsIframeUi || string.IsNullOrWhiteSpace(editor.ProviderUiUrl))
        {
            return MemoryExtensionData.Empty;
        }

        return MemoryExtensionData.From((
            MemoryProviderUiSurfaceKeys.ProviderVendorUiUrlExtension,
            JsonSerializer.SerializeToElement(editor.ProviderUiUrl.Trim())));
    }

    private static string ToSurfaceId(int index, string name)
    {
        var chars = name
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray();
        var normalizedName = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(normalizedName)
            ? $"surface-{index}"
            : $"surface-{index}-{normalizedName}";
    }

    private static class MemoryText
    {
        public static string Normalize(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value must not be empty.", parameterName);
            }

            return value.Trim();
        }
    }
}
