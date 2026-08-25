using CanDoItAll.SharedProviders.Abstractions;
using System.Collections.Frozen;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed record SharedProviderSourceWriteRequest(
    string Name,
    Uri BaseUri,
    Guid ApiTokenSecretId,
    bool IsEnabled,
    bool AllowInsecurePrivateNetwork);

public sealed record SharedProviderSourceWriteResult(
    Guid Id,
    Guid ConcurrencyToken);

public sealed record SharedProviderSourceSnapshot(
    Guid Id,
    string Name,
    Uri BaseUri,
    Guid ApiTokenSecretId,
    bool IsEnabled,
    SharedProviderSourceNetworkPolicy NetworkPolicy,
    SharedProviderSourceStatus Status,
    SharedProviderSourceInstanceId? RemoteInstanceId,
    SharedProviderCatalogEntityTag? LastCatalogETag,
    DateTimeOffset? LastSyncAtUtc,
    int? LastStatusCode,
    string LastStatusMessage,
    Guid ConcurrencyToken);

public sealed record SharedProviderSourceDeleteResult(Guid Id);

public sealed record SharedProviderPublicationWriteResult(
    Guid Id,
    SharedProviderPublicationId PublicId,
    bool IsPublished,
    Guid ConcurrencyToken);

public enum SharedProviderSourceFailureKind
{
    Connectivity,
    Authorization,
    IncompatibleContract,
    IdentityMismatch
}

public sealed record SharedProviderSourceFailure(
    SharedProviderSourceFailureKind Kind,
    int? StatusCode,
    string SanitizedMessage);

public enum SharedProviderSelectionMode
{
    AddOrReactivate,
    Replace
}

public sealed record SharedProviderReconciliationRequest
{
    public SharedProviderReconciliationRequest(
        Guid sourceId,
        SharedProviderCatalogDocument catalog,
        SharedProviderCatalogEntityTag entityTag,
        IReadOnlySet<SharedProviderPublicationId> selectedPublicationIds,
        SharedProviderSelectionMode selectionMode = SharedProviderSelectionMode.AddOrReactivate,
        Guid? expectedSourceConcurrencyToken = null)
    {
        if (sourceId == Guid.Empty)
        {
            throw new ArgumentException("The source id cannot be empty.", nameof(sourceId));
        }

        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(selectedPublicationIds);
        SharedProviderProtocolJson.ValidateCatalog(catalog);
        var normalizedCatalog = SharedProviderProtocolJson.DeserializeCatalog(
            SharedProviderProtocolJson.SerializeCatalog(catalog));
        if (entityTag != SharedProviderCatalogEntityTag.FromRevision(normalizedCatalog.CatalogRevision))
        {
            throw new ArgumentException(
                "The catalog entity tag must match the authoritative catalog revision.",
                nameof(entityTag));
        }

        if (!Enum.IsDefined(selectionMode))
        {
            throw new ArgumentOutOfRangeException(nameof(selectionMode));
        }

        if (expectedSourceConcurrencyToken == Guid.Empty)
        {
            throw new ArgumentException(
                "The expected source concurrency token cannot be empty.",
                nameof(expectedSourceConcurrencyToken));
        }

        SourceId = sourceId;
        Catalog = normalizedCatalog;
        EntityTag = entityTag;
        SelectedPublicationIds = selectedPublicationIds.ToFrozenSet();
        SelectionMode = selectionMode;
        ExpectedSourceConcurrencyToken = expectedSourceConcurrencyToken;
    }

    public Guid SourceId { get; }

    public SharedProviderCatalogDocument Catalog { get; }

    public SharedProviderCatalogEntityTag EntityTag { get; }

    public IReadOnlySet<SharedProviderPublicationId> SelectedPublicationIds { get; }

    public SharedProviderSelectionMode SelectionMode { get; }

    public Guid? ExpectedSourceConcurrencyToken { get; }
}

public enum SharedProviderReconciliationOutcome
{
    Applied,
    SourceIdentityMismatch
}

public sealed record SharedProviderReconciliationResult(
    SharedProviderReconciliationOutcome Outcome,
    IReadOnlyList<Guid> AffectedProviderProfileIds)
{
    public IReadOnlyList<Guid> RetiredProviderProfileIds { get; init; } = [];
}

public enum SharedProviderSourceOperationOutcome
{
    Succeeded,
    NotModified,
    SourceDisabled,
    SourceIdentityMismatch,
    SelectionConflict,
    Failed
}

public sealed record SharedProviderSourceOperationResult
{
    private SharedProviderSourceOperationResult(
        SharedProviderSourceOperationOutcome outcome,
        SharedProviderCatalogDocument? catalog,
        SharedProviderCatalogEntityTag? entityTag,
        IReadOnlyList<Guid> affectedProviderProfileIds,
        IReadOnlyList<Guid> retiredProviderProfileIds,
        SharedProviderFailure? failure)
    {
        Outcome = outcome;
        Catalog = catalog;
        EntityTag = entityTag;
        AffectedProviderProfileIds = Array.AsReadOnly(affectedProviderProfileIds.ToArray());
        RetiredProviderProfileIds = Array.AsReadOnly(retiredProviderProfileIds.ToArray());
        Failure = failure;
    }

    public SharedProviderSourceOperationOutcome Outcome { get; }

    public SharedProviderCatalogDocument? Catalog { get; }

    public SharedProviderCatalogEntityTag? EntityTag { get; }

    public IReadOnlyList<Guid> AffectedProviderProfileIds { get; }

    public IReadOnlyList<Guid> RetiredProviderProfileIds { get; }

    public SharedProviderFailure? Failure { get; }

    public static SharedProviderSourceOperationResult Succeeded(
        SharedProviderCatalogDocument catalog,
        SharedProviderCatalogEntityTag entityTag,
        IReadOnlyList<Guid>? affectedProviderProfileIds = null,
        IReadOnlyList<Guid>? retiredProviderProfileIds = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        SharedProviderProtocolJson.ValidateCatalog(catalog);
        return new(
            SharedProviderSourceOperationOutcome.Succeeded,
            catalog,
            entityTag,
            affectedProviderProfileIds ?? [],
            retiredProviderProfileIds ?? [],
            failure: null);
    }

    public static SharedProviderSourceOperationResult NotModified(
        SharedProviderCatalogEntityTag entityTag)
        => new(
            SharedProviderSourceOperationOutcome.NotModified,
            catalog: null,
            entityTag,
            affectedProviderProfileIds: [],
            retiredProviderProfileIds: [],
            failure: null);

    public static SharedProviderSourceOperationResult SourceDisabled(
        SharedProviderFailure failure)
        => FailedCore(SharedProviderSourceOperationOutcome.SourceDisabled, failure);

    public static SharedProviderSourceOperationResult SourceIdentityMismatch(
        SharedProviderFailure failure)
        => FailedCore(SharedProviderSourceOperationOutcome.SourceIdentityMismatch, failure);

    public static SharedProviderSourceOperationResult SelectionConflict(
        SharedProviderFailure failure)
        => FailedCore(SharedProviderSourceOperationOutcome.SelectionConflict, failure);

    public static SharedProviderSourceOperationResult Failed(
        SharedProviderFailure failure)
        => FailedCore(SharedProviderSourceOperationOutcome.Failed, failure);

    private static SharedProviderSourceOperationResult FailedCore(
        SharedProviderSourceOperationOutcome outcome,
        SharedProviderFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new(
            outcome,
            catalog: null,
            entityTag: null,
            affectedProviderProfileIds: [],
            retiredProviderProfileIds: [],
            failure);
    }
}

public sealed class SharedProviderSourceDeletionBlockedException(
    Guid sourceId,
    int importCount)
    : InvalidOperationException(
        $"Shared-provider source '{sourceId:D}' cannot be deleted while it owns {importCount} import(s). Retire or migrate those imports first.")
{
    public Guid SourceId { get; } = sourceId;

    public int ImportCount { get; } = importCount;
}

public sealed class SharedProviderSelectionConflictException(
    IReadOnlyList<SharedProviderPublicationId> unknownPublicationIds)
    : InvalidOperationException(
        "The requested shared-provider selection is stale and contains publications that are neither imported nor present in the authoritative catalog.")
{
    public IReadOnlyList<SharedProviderPublicationId> UnknownPublicationIds { get; } =
        Array.AsReadOnly(unknownPublicationIds.ToArray());
}

public sealed record SharedProviderInvocationStartRequest(
    string RequestId,
    SharedProviderPublicationId PublicationId,
    Guid ProviderProfileId,
    string AuthenticatedSubject,
    AccessContextReference? AccessContextReference,
    string TraceId,
    string CorrelationId,
    SharedProviderRelayOperation Operation,
    SharedProviderRoutingModelId PublicModelId,
    string UpstreamModelId,
    DateTimeOffset RetainUntilUtc);

public sealed class SharedProviderConcurrencyException(
    string entityName,
    Guid entityId,
    Exception? innerException = null)
    : InvalidOperationException(
        $"{entityName} '{entityId:D}' was updated by another request.",
        innerException)
{
    public string EntityName { get; } = entityName;

    public Guid EntityId { get; } = entityId;
}
