using System.Text.Json;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Tests;

internal enum GenericMockMemoryQueryMode
{
    ImmediateContext = 0,
    AcceptedOperation = 1,
    ProviderError = 2,
    UnsupportedCapability = 3,
    Timeout = 4
}

internal sealed class GenericMockMemoryProviderFixture(
    GenericMockMemoryQueryMode queryMode = GenericMockMemoryQueryMode.ImmediateContext) :
    IMemoryProviderDriver,
    IMemoryProviderOperationStatusDriver,
    IMemoryProviderFeedbackDeliveryDriver,
    IMemoryProviderEventPollDriver,
    IMemoryProviderEventOutboxDriver,
    IMemoryProviderHealthDriver
{
    public const string MockRclComponentKey = "memory.mock.panel";
    public const string ProviderVendorUiUrlExtension = "provider.vendor.uiUrl";

    private readonly Queue<Func<MemoryOperationRecord, MemoryProviderOperationPollResult>> operationPollResults = new();
    private readonly Queue<IReadOnlyList<MemoryProviderEvent>> eventPollResults = new();

    public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Mock;

    public int QueryCalls { get; private set; }

    public int OperationStatusCalls { get; private set; }

    public int FeedbackDeliveryCalls { get; private set; }

    public int EventPollCalls { get; private set; }

    public int OutboxDeliveryCalls { get; private set; }

    public MemoryProviderQueueDispatchResult FeedbackDispatchResult { get; set; } =
        MemoryProviderQueueDispatchResult.Succeeded("generic mock feedback delivered");

    public MemoryProviderQueueDispatchResult OutboxDispatchResult { get; set; } =
        MemoryProviderQueueDispatchResult.Succeeded("generic mock outbox delivered");

    public static MemoryProviderProfile ImmediateContextProfile(
        string instanceId = "provider.mock.immediate",
        bool isEnabled = true,
        IReadOnlyList<string>? tags = null) =>
        CreateProfile(
            instanceId,
            "Immediate generic mock memory",
            [
                MemoryCapabilityIds.ContextQuerySync,
                MemoryCapabilityIds.FeedbackImmediate
            ],
            isEnabled,
            tags ?? ["mock", "immediate"]);

    public static MemoryProviderProfile DelayedContextProfile(
        string instanceId = "provider.mock.delayed",
        bool isEnabled = true,
        IReadOnlyList<string>? tags = null) =>
        CreateProfile(
            instanceId,
            "Delayed generic mock memory",
            [
                MemoryCapabilityIds.ContextQueryAsync,
                MemoryCapabilityIds.OperationStatus,
                MemoryCapabilityIds.FeedbackDelayed
            ],
            isEnabled,
            tags ?? ["mock", "delayed"]);

    public static MemoryProviderProfile EventfulProfile(
        string instanceId = "provider.mock.eventful",
        bool isEnabled = true,
        IReadOnlyList<string>? tags = null) =>
        CreateProfile(
            instanceId,
            "Eventful generic mock memory",
            [
                MemoryCapabilityIds.EventsHostPoll,
                MemoryCapabilityIds.EventsProviderPush
            ],
            isEnabled,
            tags ?? ["mock", "eventful"]);

    public static MemoryProviderProfile UiSurfaceProfile(
        string instanceId = "provider.mock.ui",
        bool isEnabled = true) =>
        CreateProfile(
            instanceId,
            "UI surface generic mock memory",
            [
                MemoryCapabilityIds.UiRcl,
                MemoryCapabilityIds.UiIframe
            ],
            isEnabled,
            ["mock", "ui"],
            [
                new MemoryProviderUiSurface(
                    MemoryProviderUiSurfaceKind.RazorComponentLibrary,
                    "Mock RCL surface",
                    MockRclComponentKey,
                    UrlSettingKey: null,
                    MemoryCapabilityIds.UiRcl),
                new MemoryProviderUiSurface(
                    MemoryProviderUiSurfaceKind.Iframe,
                    "Mock iframe console",
                    ComponentKey: null,
                    UrlSettingKey: ProviderVendorUiUrlExtension,
                    MemoryCapabilityIds.UiIframe)
            ],
            MemoryExtensionData.From((
                ProviderVendorUiUrlExtension,
                JsonSerializer.SerializeToElement("https://memory.example.test/mock"))));

    public static MemoryProviderProfile FailingProfile(
        string instanceId = "provider.mock.failing",
        bool isEnabled = true) =>
        CreateProfile(
            instanceId,
            "Failing generic mock memory",
            [MemoryCapabilityIds.ContextQuerySync],
            isEnabled,
            ["mock", "failing"]);

    public void CompleteNextOperationWithPayload(string text)
    {
        operationPollResults.Enqueue(operation =>
            MemoryProviderOperationPollResult.FromResult(
                new MemoryOperationResult(
                    operation.OperationId,
                    MemoryOperationStatus.Succeeded,
                    MemoryPayload.FromText(text),
                    Warnings: [],
                    FeedbackHandles: [],
                    SourceRefs: []),
                "generic mock operation completed"));
    }

    public void EnqueueEvents(params MemoryProviderEvent[] events)
    {
        eventPollResults.Enqueue(events.ToArray());
    }

    public Task<MemoryProviderDriverResult> ExecuteContextQueryAsync(
        MemoryProviderProfile provider,
        MemoryOperationRecord operation,
        MemoryContextQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        QueryCalls++;

        return Task.FromResult(queryMode switch
        {
            GenericMockMemoryQueryMode.ImmediateContext =>
                MemoryProviderDriverResult.ContextPackResult(
                    CreateContextPack(provider, request),
                    "generic mock context completed"),
            GenericMockMemoryQueryMode.AcceptedOperation =>
                MemoryProviderDriverResult.Accepted(
                    new MemoryOperationAccepted(
                        operation.OperationId,
                        $"/memory/mock/operations/{operation.OperationId.Value:D}",
                        operation.CreatedAtUtc.AddMinutes(5),
                        TimeSpan.FromMilliseconds(10),
                        CallbackAvailable: false),
                    "generic mock operation accepted"),
            GenericMockMemoryQueryMode.UnsupportedCapability =>
                MemoryProviderDriverResult.Failed(
                    MemoryProviderDriverResultKind.UnsupportedCapability,
                    "generic mock unsupported capability"),
            GenericMockMemoryQueryMode.Timeout =>
                MemoryProviderDriverResult.Failed(
                    MemoryProviderDriverResultKind.Timeout,
                    "generic mock timed out"),
            GenericMockMemoryQueryMode.ProviderError =>
                MemoryProviderDriverResult.Failed(
                    MemoryProviderDriverResultKind.ProviderError,
                    "generic mock provider error"),
            _ => MemoryProviderDriverResult.Failed(
                MemoryProviderDriverResultKind.ProviderError,
                "generic mock query mode is invalid")
        });
    }

    public Task<MemoryProviderOperationPollResult> PollOperationAsync(
        MemoryProviderProfile provider,
        MemoryOperationRecord operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        OperationStatusCalls++;

        if (operationPollResults.Count == 0)
        {
            return Task.FromResult(MemoryProviderOperationPollResult.StillRunning("generic mock operation still running"));
        }

        return Task.FromResult(operationPollResults.Dequeue()(operation));
    }

    public Task<MemoryProviderQueueDispatchResult> DeliverFeedbackAsync(
        MemoryProviderProfile provider,
        MemoryFeedbackRecord feedback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(feedback);
        cancellationToken.ThrowIfCancellationRequested();
        FeedbackDeliveryCalls++;
        return Task.FromResult(FeedbackDispatchResult);
    }

    public Task<MemoryProviderEventPollResult> PollEventsAsync(
        MemoryProviderProfile provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        cancellationToken.ThrowIfCancellationRequested();
        EventPollCalls++;

        if (eventPollResults.Count == 0)
        {
            return Task.FromResult(MemoryProviderEventPollResult.FromEvents([], "generic mock returned no events"));
        }

        return Task.FromResult(MemoryProviderEventPollResult.FromEvents(
            eventPollResults.Dequeue(),
            "generic mock returned provider events"));
    }

    public Task<MemoryProviderQueueDispatchResult> DeliverOutboxAsync(
        MemoryProviderProfile provider,
        MemoryEventOutboxRecord outbox,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(outbox);
        cancellationToken.ThrowIfCancellationRequested();
        OutboxDeliveryCalls++;
        return Task.FromResult(OutboxDispatchResult);
    }

    public Task<MemoryProviderHealth> GetHealthAsync(
        MemoryProviderProfile provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new MemoryProviderHealth(
            MemoryProviderHealthStatus.Reachable,
            LastErrorCategory: null,
            provider.Manifest));
    }

    public static MemoryProviderEvent CreateProviderEvent(
        MemoryProviderEventKind eventKind = MemoryProviderEventKind.SourceRequest,
        string message = "generic mock provider event") =>
        new(
            MemoryProviderEventId.New(),
            eventKind,
            MemoryCorrelationId.New(),
            MemoryCausationId.New(),
            message,
            MemoryPayload.FromText(message));

    private static MemoryContextPack CreateContextPack(
        MemoryProviderProfile provider,
        MemoryContextQueryRequest request)
    {
        var sourceRef = request.SourceProvenance.SourceSnapshotId?.Value
            ?? request.SourceProvenance.SourceModule
            ?? "memory://generic-mock";
        var sourceLabel = request.SourceProvenance.Citations.FirstOrDefault()
            ?? request.SourceProvenance.SourceModule
            ?? "generic mock memory";
        return new MemoryContextPack(
            MemoryContextPackId.New(),
            $"Generic mock context from {provider.InstanceId.Value}: {request.Query}",
            [
                new MemoryContextSection(
                    "Generic mock memory",
                    $"Provider '{provider.InstanceId.Value}' handled '{request.Query}'.",
                    [new MemoryCitation(sourceRef, sourceLabel)],
                    Confidence: 1.0m)
            ],
            Warnings: [],
            ProviderConfidence: 1.0m,
            FeedbackHandle: null);
    }

    private static MemoryProviderProfile CreateProfile(
        string instanceId,
        string displayName,
        IReadOnlyList<MemoryCapabilityId> capabilities,
        bool isEnabled,
        IReadOnlyList<string> tags,
        IReadOnlyList<MemoryProviderUiSurface>? surfaces = null,
        MemoryExtensionData? extensions = null)
    {
        return new MemoryProviderProfile(
            MemoryProviderInstanceId.Parse(instanceId),
            displayName,
            MemoryProviderDriverKind.Mock,
            isEnabled,
            MemoryProviderHealthState.Healthy,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            tags,
            MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(
                MemoryProviderKind.Parse("memory.mock"),
                MemoryProtocolVersion.Current,
                capabilities
                    .Select(capability => new MemoryCapabilityDescriptor(capability, Version: "1", Supported: true))
                    .ToArray(),
                CreateInteractionSupport(capabilities),
                surfaces ?? [],
                MemoryProviderLimits.Default,
                extensions ?? MemoryExtensionData.Empty));
    }

    private static MemoryProviderInteractionSupport CreateInteractionSupport(
        IReadOnlyList<MemoryCapabilityId> capabilities) =>
        new(
            SupportsSynchronousQueries: capabilities.Contains(MemoryCapabilityIds.ContextQuerySync),
            SupportsAsynchronousOperations: capabilities.Contains(MemoryCapabilityIds.ContextQueryAsync),
            SupportsSourceRequests:
                capabilities.Contains(MemoryCapabilityIds.IngestionSnapshot) ||
                capabilities.Contains(MemoryCapabilityIds.IngestionProviderRequestedSource),
            SupportsFeedback:
                capabilities.Contains(MemoryCapabilityIds.FeedbackImmediate) ||
                capabilities.Contains(MemoryCapabilityIds.FeedbackDelayed),
            SupportsProviderEvents:
                capabilities.Contains(MemoryCapabilityIds.EventsHostPoll) ||
                capabilities.Contains(MemoryCapabilityIds.EventsProviderPush));
}
