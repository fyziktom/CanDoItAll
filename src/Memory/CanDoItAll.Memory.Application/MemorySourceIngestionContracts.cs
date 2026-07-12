using CanDoItAll.Memory.SourceGateway;
using CanDoItAll.Memory.Abstractions;
using MafMemorySourceKind = CanDoItAll.Memory.SourceGateway.MemorySourceKind;
using MafMemorySourceSnapshotId = CanDoItAll.Memory.SourceGateway.MemorySourceSnapshotId;

namespace CanDoItAll.Memory.Application;

public enum MemorySourceIngestionJobStatus
{
    Queued = 0,
    SnapshotCaptured = 1,
    DispatchReady = 2,
    Failed = 3,
    Cancelled = 4
}

public sealed record MemoryProviderSourceGatewayRequest(
    MemoryProviderInstanceId ProviderInstanceId,
    MemorySourceRequest ProviderRequest,
    MemorySourceGatewayRequest SourceGatewayRequest,
    Type ExpectedSnapshotContractType)
{
    public static MemoryProviderSourceGatewayRequest Create(
        MemoryProviderInstanceId providerInstanceId,
        MemorySourceRequest providerRequest,
        MafMemorySourceKind sourceKind,
        Guid scopeId,
        MemorySourceGatewayPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(providerRequest);
        return new MemoryProviderSourceGatewayRequest(
            providerInstanceId,
            providerRequest,
            new MemorySourceGatewayRequest(
                sourceKind,
                scopeId,
                ResolveRequestedScope(providerRequest.RequestedScopes),
                Cursor: null,
                Take: null,
                policy,
                RequesterId: providerRequest.SourceRequestId.Value),
            typeof(MemorySourceSnapshot));
    }

    private static MemorySourceScope ResolveRequestedScope(IReadOnlyList<MemorySourceScope> requestedScopes)
    {
        return requestedScopes.FirstOrDefault();
    }
}

public sealed record MemorySourceIngestionJobRequest(
    Guid JobId,
    MemoryProviderInstanceId ProviderInstanceId,
    MemorySourceGatewayRequest SourceGatewayRequest,
    Type ExpectedSnapshotContractType)
{
    public static MemorySourceIngestionJobRequest Create(
        MemoryProviderInstanceId providerInstanceId,
        MafMemorySourceKind sourceKind,
        Guid scopeId,
        MemorySourceScope requestedScope,
        MemorySourceGatewayPolicy policy,
        string requestedBy) =>
        new(
            Guid.NewGuid(),
            providerInstanceId,
            new MemorySourceGatewayRequest(
                sourceKind,
                scopeId,
                requestedScope,
                Cursor: null,
                Take: null,
                policy,
                requestedBy),
            typeof(MemorySourceSnapshot));
}

public sealed record MemorySourceIngestionJobRecord(
    Guid JobId,
    MemoryProviderInstanceId ProviderInstanceId,
    MemorySourceGatewayRequest SourceGatewayRequest,
    MemorySourceIngestionJobStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string StatusReason,
    MafMemorySourceSnapshotId? CapturedSnapshotId = null,
    MemoryOperationId? OperationId = null);
