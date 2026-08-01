using System.Text.Json.Serialization;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Web.Api;

internal enum MemoryProviderQueryMode
{
    Synchronous = 0,
    Asynchronous = 1
}

internal enum MemoryProviderDriverKindApiRequest
{
    Http = 0,
    Mcp = 1,
    NativeRemote = 2,
    Mock = 3
}

internal enum MemoryProviderFallbackBehaviorApiRequest
{
    DenyImplicitFallback = 0,
    AllowDefaultProviderWhenNoAssignment = 1
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record MemoryProviderProfileApiRequest(
    string DisplayName,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryProviderDriverKindApiRequest>))]
    MemoryProviderDriverKindApiRequest DriverKind,
    bool IsEnabled,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryProviderFallbackBehaviorApiRequest>))]
    MemoryProviderFallbackBehaviorApiRequest FallbackBehavior,
    string ProviderKind,
    IReadOnlyList<string>? SelectionTags,
    MemoryProviderCapabilitiesApiRequest Capabilities,
    MemoryProviderHttpTransportApiModel? Http,
    MemoryProviderMcpTransportApiModel? Mcp);

internal sealed record MemoryProviderProfileApiResponse(
    string ProviderId,
    string DisplayName,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryProviderDriverKind>))]
    MemoryProviderDriverKind DriverKind,
    bool IsEnabled,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryProviderHealthState>))]
    MemoryProviderHealthState HealthState,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryProviderWorkspaceScope>))]
    MemoryProviderWorkspaceScope WorkspaceScope,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryProviderFallbackBehavior>))]
    MemoryProviderFallbackBehavior FallbackBehavior,
    string ProviderKind,
    string ProtocolVersion,
    IReadOnlyList<string> SelectionTags,
    MemoryProviderCapabilitiesApiResponse Capabilities,
    MemoryProviderInteractionSupportApiResponse InteractionSupport,
    MemoryProviderLimitsApiResponse Limits,
    MemoryProviderHttpTransportApiModel? Http,
    MemoryProviderMcpTransportApiModel? Mcp);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record MemoryProviderCapabilitiesApiRequest(
    bool SupportsSynchronousQueries,
    bool SupportsAsynchronousQueries,
    bool SupportsOperationStatus);

internal sealed record MemoryProviderCapabilitiesApiResponse(
    bool SupportsSynchronousQueries,
    bool SupportsAsynchronousQueries,
    bool SupportsOperationStatus,
    bool SupportsRclUi,
    bool SupportsIframeUi);

internal sealed record MemoryProviderInteractionSupportApiResponse(
    bool SupportsSynchronousQueries,
    bool SupportsAsynchronousOperations);

internal sealed record MemoryProviderLimitsApiResponse(
    int MaxContextSections,
    int MaxSourceItems,
    int MaxInFlightOperations,
    double OperationTimeoutSeconds);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record MemoryProviderHttpTransportApiModel(
    string BaseUrl,
    string QueryPath,
    string HealthPath,
    string ApiKeyEnvironmentVariable,
    string AuthHeaderName,
    string AuthScheme,
    int TimeoutMilliseconds,
    int MaxRetryAttempts);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record MemoryProviderMcpTransportApiModel(
    string DescriptorKind,
    string ServerKey,
    string DisplayName,
    string Description,
    string RemoteEndpoint,
    string AuthHeaderName,
    string AuthHeaderEnvironmentVariable,
    string ContextQueryTool,
    string OperationStatusTool);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record MemoryProviderQueryApiRequest(
    string Query,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryProviderQueryMode>))]
    MemoryProviderQueryMode Mode);

internal sealed record MemoryProviderQueryApiResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryOperationHandlerStatus>))]
    MemoryOperationHandlerStatus Status,
    string Diagnostic,
    MemoryProviderSelectionApiResponse Selection,
    MemoryProviderOperationApiResponse? Operation,
    MemoryContextPackApiResponse? ContextPack,
    MemoryAcceptedOperationApiResponse? AcceptedOperation,
    string? FeedbackHandle,
    bool DriverDispatchAttempted);

internal sealed record MemoryProviderOperationStatusApiResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryOperationHandlerStatus>))]
    MemoryOperationHandlerStatus Status,
    string Diagnostic,
    MemoryProviderSelectionApiResponse Selection,
    MemoryProviderOperationApiResponse? Operation);

internal sealed record MemoryProviderSelectionApiResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryProviderSelectionStatus>))]
    MemoryProviderSelectionStatus Status,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryProviderSelectionReason>))]
    MemoryProviderSelectionReason Reason,
    string RequiredCapability,
    bool DispatchAllowed,
    string Diagnostic,
    string? SelectedProviderId,
    IReadOnlyList<string> CandidateProviderIds);

internal sealed record MemoryProviderOperationApiResponse(
    Guid OperationId,
    string ProviderId,
    string RequestedCapability,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryOperationKind>))]
    MemoryOperationKind OperationKind,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryLedgerStatus>))]
    MemoryLedgerStatus Status,
    int RetryCount,
    int TransitionCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string StatusReason);

internal sealed record MemoryContextPackApiResponse(
    Guid ContextPackId,
    string Summary,
    IReadOnlyList<MemoryContextSectionApiResponse> Sections,
    IReadOnlyList<MemoryWarningApiResponse> Warnings,
    decimal ProviderConfidence,
    string? FeedbackHandle);

internal sealed record MemoryContextSectionApiResponse(
    string Title,
    string Text,
    IReadOnlyList<MemoryCitationApiResponse> Citations,
    decimal Confidence);

internal sealed record MemoryCitationApiResponse(
    string SourceRef,
    string Label);

internal sealed record MemoryWarningApiResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryWarningKind>))]
    MemoryWarningKind Kind,
    string Message);

internal sealed record MemoryAcceptedOperationApiResponse(
    Guid OperationId,
    string StatusPath,
    DateTimeOffset ExpiresAtUtc,
    double PollAfterSeconds,
    bool CallbackAvailable);
