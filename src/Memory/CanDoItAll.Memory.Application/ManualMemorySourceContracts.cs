using CanDoItAll.Memory.Abstractions;
using MafMemorySourceKind = CanDoItAll.Memory.SourceGateway.MemorySourceKind;
using MafMemorySourceSnapshotId = CanDoItAll.Memory.SourceGateway.MemorySourceSnapshotId;

namespace CanDoItAll.Memory.Application;

public enum ManualMemorySourcePayloadKind
{
    Text = 0,
    FileReference = 1,
    LinkReference = 2
}

public static class ManualMemorySourceGatewayParameterKeys
{
    public const string PayloadKind = "manual.payloadKind";
    public const string Title = "manual.title";
    public const string ContentText = "manual.contentText";
    public const string Locator = "manual.locator";
    public const string ContentType = "manual.contentType";
    public const string SourceCategory = "manual.sourceCategory";
    public const string Tags = "manual.tags";
}

public sealed record ManualMemorySourcePayload(
    Guid SourceId,
    ManualMemorySourcePayloadKind PayloadKind,
    string Title,
    string ContentText,
    string Locator,
    string ContentType,
    string SourceCategory,
    IReadOnlyList<string> Tags)
{
    public static ManualMemorySourcePayload Text(
        string title,
        string contentText,
        string sourceCategory,
        IReadOnlyList<string>? tags = null) =>
        new(
            Guid.NewGuid(),
            ManualMemorySourcePayloadKind.Text,
            title,
            contentText,
            Locator: string.Empty,
            ContentType: "text/plain",
            sourceCategory,
            tags ?? []);

    public static ManualMemorySourcePayload FileReference(
        string title,
        string locator,
        string contentType,
        string sourceCategory,
        IReadOnlyList<string>? tags = null) =>
        new(
            Guid.NewGuid(),
            ManualMemorySourcePayloadKind.FileReference,
            title,
            ContentText: string.Empty,
            locator,
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim(),
            sourceCategory,
            tags ?? []);

    public static ManualMemorySourcePayload LinkReference(
        string title,
        string url,
        string sourceCategory,
        IReadOnlyList<string>? tags = null) =>
        new(
            Guid.NewGuid(),
            ManualMemorySourcePayloadKind.LinkReference,
            title,
            ContentText: string.Empty,
            url,
            ContentType: "text/uri-list",
            sourceCategory,
            tags ?? []);

    public MemorySourceGatewayRequest ToGatewayRequest(
        MemoryProviderInstanceId providerInstanceId,
        string requestedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedBy);
        return new MemorySourceGatewayRequest(
            MafMemorySourceKind.ManualInput,
            SourceId,
            MemorySourceScope.Manual,
            Cursor: null,
            Take: null,
            MemorySourceGatewayPolicy.AllowScopes(
                [MafMemorySourceKind.ManualInput],
                [MemorySourceScope.Manual]),
            requestedBy)
        {
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ManualMemorySourceGatewayParameterKeys.PayloadKind] = PayloadKind.ToString(),
                [ManualMemorySourceGatewayParameterKeys.Title] = Title,
                [ManualMemorySourceGatewayParameterKeys.ContentText] = ContentText,
                [ManualMemorySourceGatewayParameterKeys.Locator] = Locator,
                [ManualMemorySourceGatewayParameterKeys.ContentType] = ContentType,
                [ManualMemorySourceGatewayParameterKeys.SourceCategory] = SourceCategory,
                [ManualMemorySourceGatewayParameterKeys.Tags] = string.Join(",", Tags.Select(tag => tag.Trim()).Where(tag => tag.Length > 0))
            }
        };
    }
}

public sealed record ManualMemorySourceIngestionRequest(
    MemoryProviderInstanceId ProviderInstanceId,
    ManualMemorySourcePayload Payload,
    string RequestedBy,
    MemoryLedgerRequester Requester,
    MemoryLedgerRetentionPolicy Retention);

public sealed record ManualMemorySourceIngestionResult(
    Guid JobId,
    MemoryOperationId OperationId,
    MafMemorySourceSnapshotId CapturedSnapshotId,
    IReadOnlyList<MemorySourcePayloadForm> PayloadForms);
