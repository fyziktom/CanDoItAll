using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Security.Abstractions;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class SharedProviderSourceSyncService
{
    private const int MaximumSelectedPublications = 256;

    private readonly IDbContextFactory<AppDbContext> dbContextFactory;
    private readonly SharedProviderSourceService sourceService;
    private readonly SharedProviderReconciliationCoordinator reconciliationCoordinator;
    private readonly ISharedProviderCatalogClient catalogClient;
    private readonly ISecretRuntimeResolver secretRuntimeResolver;

    public SharedProviderSourceSyncService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        SharedProviderSourceService sourceService,
        SharedProviderReconciliationCoordinator reconciliationCoordinator,
        ISharedProviderCatalogClient catalogClient,
        ISecretRuntimeResolver secretRuntimeResolver)
    {
        this.dbContextFactory = dbContextFactory;
        this.sourceService = sourceService;
        this.reconciliationCoordinator = reconciliationCoordinator;
        this.catalogClient = catalogClient;
        this.secretRuntimeResolver = secretRuntimeResolver;
    }

    public async Task<SharedProviderSourceOperationResult> TestAsync(
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        var source = await sourceService.GetAsync(sourceId, cancellationToken);
        SharedProviderCatalogAccessToken accessToken;
        try
        {
            accessToken = await ResolveAccessTokenAsync(source, cancellationToken);
        }
        catch (SharedProviderSourceCredentialException exception)
        {
            var change = await RecordFetchFailureAsync(source, exception.Failure, cancellationToken);
            return SharedProviderSourceOperationResult.Failed(exception.Failure) with { Change = change };
        }

        var fetchResult = await catalogClient.FetchAsync(
            CreateFetchRequest(
                source,
                accessToken,
                ifNoneMatch: null),
            cancellationToken);
        return await HandleTestResultAsync(source, fetchResult, cancellationToken);
    }

    public async Task<SharedProviderSourceOperationResult> SynchronizeAsync(
        Guid sourceId,
        IReadOnlySet<SharedProviderPublicationId> selectedPublicationIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedSelection = NormalizeSelection(selectedPublicationIds);
        var source = await sourceService.GetAsync(sourceId, cancellationToken);
        if (!source.IsEnabled)
        {
            return SharedProviderSourceOperationResult.SourceDisabled(
                SharedProviderSourceOperationFailures.SourceDisabled());
        }

        SharedProviderCatalogAccessToken accessToken;
        try
        {
            accessToken = await ResolveAccessTokenAsync(source, cancellationToken);
        }
        catch (SharedProviderSourceCredentialException exception)
        {
            var change = await RecordFetchFailureAsync(source, exception.Failure, cancellationToken);
            return SharedProviderSourceOperationResult.Failed(exception.Failure) with { Change = change };
        }

        var currentSelection = await LoadSelectionStateAsync(
            source.Id,
            cancellationToken);
        var conditionalEntityTag = CanUseConditionalFetch(source, currentSelection) &&
            currentSelection.PublicationIds.SetEquals(normalizedSelection)
            ? source.LastCatalogETag
            : null;
        var fetchResult = await catalogClient.FetchAsync(
            CreateFetchRequest(
                source,
                accessToken,
                conditionalEntityTag),
            cancellationToken);
        if (fetchResult is SharedProviderCatalogFetchResult.NotModified notModified)
        {
            if (conditionalEntityTag is null || notModified.EntityTag != conditionalEntityTag)
            {
                var failure = SharedProviderSourceOperationFailures.InvalidNotModified();
                var change = await RecordFetchFailureAsync(source, failure, cancellationToken);
                return SharedProviderSourceOperationResult.Failed(failure) with { Change = change };
            }

            var latestSelection = await LoadSelectionStateAsync(
                source.Id,
                cancellationToken);
            var latestSource = await sourceService.GetAsync(source.Id, cancellationToken);
            if (latestSource.ConcurrencyToken != source.ConcurrencyToken)
            {
                throw new SharedProviderConcurrencyException(
                    nameof(SharedProviderSource),
                    source.Id);
            }

            if (CanUseConditionalFetch(latestSource, latestSelection) &&
                latestSelection.PublicationIds.SetEquals(normalizedSelection))
            {
                return SharedProviderSourceOperationResult.NotModified(notModified.EntityTag);
            }

            fetchResult = await catalogClient.FetchAsync(
                CreateFetchRequest(
                    source,
                    accessToken,
                    ifNoneMatch: null),
                cancellationToken);
        }

        return await HandleSynchronizationResultAsync(
            source,
            normalizedSelection,
            fetchResult,
            cancellationToken);
    }

    private async Task<SharedProviderSourceOperationResult> HandleTestResultAsync(
        SharedProviderSourceSnapshot source,
        SharedProviderCatalogFetchResult fetchResult,
        CancellationToken cancellationToken)
    {
        switch (fetchResult)
        {
            case SharedProviderCatalogFetchResult.Succeeded succeeded:
            {
                var acceptance = await sourceService.RecordSuccessfulCatalogTestAsync(
                    source.Id,
                    source.ConcurrencyToken,
                    succeeded.Catalog.SourceInstanceId,
                    succeeded.EntityTag,
                    cancellationToken);
                if (acceptance.Acceptance == SharedProviderCatalogIdentityAcceptance.IdentityMismatch)
                {
                    var failure = SharedProviderSourceOperationFailures.IdentityMismatch();
                    return SharedProviderSourceOperationResult.SourceIdentityMismatch(failure) with { Change = acceptance.Change };
                }

                return SharedProviderSourceOperationResult.Succeeded(
                    succeeded.Catalog,
                    succeeded.EntityTag) with { Change = acceptance.Change };
            }
            case SharedProviderCatalogFetchResult.NotModified:
            {
                var failure = SharedProviderSourceOperationFailures.UnexpectedNotModified();
                var change = await RecordFetchFailureAsync(source, failure, cancellationToken);
                return SharedProviderSourceOperationResult.Failed(failure) with { Change = change };
            }
            case SharedProviderCatalogFetchResult.Failed failed:
                return await PersistFetchFailureAsync(source, failed.Failure, cancellationToken);
            default:
                throw new ArgumentOutOfRangeException(nameof(fetchResult));
        }
    }

    private async Task<SharedProviderSourceOperationResult> HandleSynchronizationResultAsync(
        SharedProviderSourceSnapshot source,
        IReadOnlySet<SharedProviderPublicationId> selectedPublicationIds,
        SharedProviderCatalogFetchResult fetchResult,
        CancellationToken cancellationToken)
    {
        switch (fetchResult)
        {
            case SharedProviderCatalogFetchResult.Succeeded succeeded:
            {
                try
                {
                    var reconciliation = await reconciliationCoordinator.ReconcileAsync(
                        new SharedProviderReconciliationRequest(
                            source.Id,
                            succeeded.Catalog,
                            succeeded.EntityTag,
                            selectedPublicationIds,
                            SharedProviderSelectionMode.Replace,
                            source.ConcurrencyToken),
                        cancellationToken);
                    if (reconciliation.Outcome ==
                        SharedProviderReconciliationOutcome.SourceIdentityMismatch)
                    {
                        var failure = SharedProviderSourceOperationFailures.IdentityMismatch();
                        return SharedProviderSourceOperationResult.SourceIdentityMismatch(failure) with { Change = reconciliation.Change };
                    }

                    return SharedProviderSourceOperationResult.Succeeded(
                        succeeded.Catalog,
                        succeeded.EntityTag,
                        reconciliation.AffectedProviderProfileIds,
                        reconciliation.RetiredProviderProfileIds) with { Change = reconciliation.Change };
                }
                catch (SharedProviderSelectionConflictException)
                {
                    return SharedProviderSourceOperationResult.SelectionConflict(
                        SharedProviderSourceOperationFailures.SelectionConflict());
                }
            }
            case SharedProviderCatalogFetchResult.NotModified:
            {
                var failure = SharedProviderSourceOperationFailures.UnexpectedNotModified();
                var change = await RecordFetchFailureAsync(source, failure, cancellationToken);
                return SharedProviderSourceOperationResult.Failed(failure) with { Change = change };
            }
            case SharedProviderCatalogFetchResult.Failed failed:
                return await PersistFetchFailureAsync(source, failed.Failure, cancellationToken);
            default:
                throw new ArgumentOutOfRangeException(nameof(fetchResult));
        }
    }

    private async Task<SharedProviderSourceOperationResult> PersistFetchFailureAsync(
        SharedProviderSourceSnapshot source,
        SharedProviderFailure failure,
        CancellationToken cancellationToken)
    {
        var change = await RecordFetchFailureAsync(source, failure, cancellationToken);
        return (failure.Code == SharedProviderCatalogFailureCodes.SourceIdentityMismatch
            ? SharedProviderSourceOperationResult.SourceIdentityMismatch(failure)
            : SharedProviderSourceOperationResult.Failed(failure)) with { Change = change };
    }

    private Task<SharedProviderChange> RecordFetchFailureAsync(
        SharedProviderSourceSnapshot source,
        SharedProviderFailure failure,
        CancellationToken cancellationToken)
        => sourceService.RecordFetchFailureAsync(
            source.Id,
            source.ConcurrencyToken,
            new SharedProviderSourceFailure(
                ClassifyFailure(failure),
                MapStatusCode(failure.Category),
                failure.SanitizedMessage),
            cancellationToken);

    private async Task<SharedProviderCatalogAccessToken> ResolveAccessTokenAsync(
        SharedProviderSourceSnapshot source,
        CancellationToken cancellationToken)
    {
        string? tokenValue;
        try
        {
            tokenValue = await secretRuntimeResolver.ResolveValueAsync(
                new SecretRuntimeRequest(
                    source.ApiTokenSecretId,
                    SecretRuntimePurposes.SharedProviderSourceToken,
                    AllowedSecretIds: [source.ApiTokenSecretId],
                    ConsumerType: SecretRuntimeConsumerTypes.SharedProviderSource,
                    ConsumerId: SecretRuntimeConsumerIds.SharedProviderSource(source.Id)),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new SharedProviderSourceCredentialException(
                SharedProviderSourceOperationFailures.CredentialUnavailable(),
                exception);
        }

        try
        {
            return new SharedProviderCatalogAccessToken(tokenValue!);
        }
        catch (ArgumentException exception)
        {
            throw new SharedProviderSourceCredentialException(
                SharedProviderSourceOperationFailures.CredentialUnavailable(),
                exception);
        }
    }

    private static SharedProviderCatalogFetchRequest CreateFetchRequest(
        SharedProviderSourceSnapshot source,
        SharedProviderCatalogAccessToken accessToken,
        SharedProviderCatalogEntityTag? ifNoneMatch)
        => new(
            source.BaseUri,
            source.NetworkPolicy,
            accessToken,
            ifNoneMatch,
            source.RemoteInstanceId);

    private async Task<SharedProviderSelectionStateSnapshot> LoadSelectionStateAsync(
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var selectedImports = await dbContext.Set<SharedProviderImport>()
            .AsNoTracking()
            .Where(import =>
                import.SourceId == sourceId &&
                import.SelectionState == SharedProviderSelectionState.Selected)
            .Select(import => new
            {
                import.RemotePublicationId,
                import.AvailabilityState
            })
            .ToArrayAsync(cancellationToken);
        return new SharedProviderSelectionStateSnapshot(
            selectedImports.Select(import => import.RemotePublicationId).ToHashSet(),
            selectedImports.All(import => IsAuthoritativeAvailability(import.AvailabilityState)));
    }

    private static bool CanUseConditionalFetch(
        SharedProviderSourceSnapshot source,
        SharedProviderSelectionStateSnapshot selection)
        => source.Status == SharedProviderSourceStatus.Available &&
            selection.HasOnlyAuthoritativeAvailability;

    private static bool IsAuthoritativeAvailability(SharedProviderAvailabilityState availability)
        => availability is
            SharedProviderAvailabilityState.Available or
            SharedProviderAvailabilityState.Unpublished or
            SharedProviderAvailabilityState.Missing;

    private static IReadOnlySet<SharedProviderPublicationId> NormalizeSelection(
        IReadOnlySet<SharedProviderPublicationId> selectedPublicationIds)
    {
        ArgumentNullException.ThrowIfNull(selectedPublicationIds);
        if (selectedPublicationIds.Count > MaximumSelectedPublications ||
            selectedPublicationIds.Any(publicationId => publicationId.Value == Guid.Empty))
        {
            throw new ArgumentException(
                $"A source selection must contain at most {MaximumSelectedPublications} valid publication identifiers.",
                nameof(selectedPublicationIds));
        }

        return selectedPublicationIds.ToHashSet();
    }

    private static SharedProviderSourceFailureKind ClassifyFailure(
        SharedProviderFailure failure)
    {
        if (failure.Code == SharedProviderCatalogFailureCodes.SourceIdentityMismatch)
        {
            return SharedProviderSourceFailureKind.IdentityMismatch;
        }

        return failure.Category switch
        {
            SharedProviderFailureCategory.Unauthorized or
                SharedProviderFailureCategory.InsufficientScope =>
                    SharedProviderSourceFailureKind.Authorization,
            SharedProviderFailureCategory.Validation or
                SharedProviderFailureCategory.NotFound or
                SharedProviderFailureCategory.UpstreamFailure or
                SharedProviderFailureCategory.VersionUnsupported or
                SharedProviderFailureCategory.Conflict =>
                    SharedProviderSourceFailureKind.IncompatibleContract,
            _ => SharedProviderSourceFailureKind.Connectivity
        };
    }

    private static int? MapStatusCode(SharedProviderFailureCategory category)
        => category switch
        {
            SharedProviderFailureCategory.Validation => 422,
            SharedProviderFailureCategory.Unauthorized => 401,
            SharedProviderFailureCategory.InsufficientScope => 403,
            SharedProviderFailureCategory.NotFound => 404,
            SharedProviderFailureCategory.Conflict => 409,
            SharedProviderFailureCategory.Unavailable => 503,
            SharedProviderFailureCategory.RateLimited => 429,
            SharedProviderFailureCategory.UpstreamFailure => 502,
            SharedProviderFailureCategory.Timeout => 504,
            SharedProviderFailureCategory.VersionUnsupported => 422,
            _ => null
        };

    private sealed class SharedProviderSourceCredentialException(
        SharedProviderFailure failure,
        Exception innerException)
        : InvalidOperationException(
            "The shared-provider source credential could not be resolved.",
            innerException)
    {
        public SharedProviderFailure Failure { get; } = failure;
    }

    private sealed record SharedProviderSelectionStateSnapshot(
        IReadOnlySet<SharedProviderPublicationId> PublicationIds,
        bool HasOnlyAuthoritativeAvailability);
}

internal static class SharedProviderSourceOperationFailures
{
    private static readonly SharedProviderFailureCode SourceDisabledCode =
        new("shared_provider_source_disabled");
    private static readonly SharedProviderFailureCode CredentialUnavailableCode =
        new("shared_provider_source_credential_unavailable");
    private static readonly SharedProviderFailureCode SelectionConflictCode =
        new("shared_provider_source_selection_stale");
    private static readonly SharedProviderFailureCode UnexpectedNotModifiedCode =
        new("shared_provider_catalog_unexpected_not_modified");
    private static readonly SharedProviderFailureCode InvalidNotModifiedCode =
        new("shared_provider_catalog_invalid_not_modified");

    public static SharedProviderFailure SourceDisabled()
        => new(
            SharedProviderFailureCategory.Validation,
            SourceDisabledCode,
            "The shared-provider source is disabled.");

    public static SharedProviderFailure CredentialUnavailable()
        => new(
            SharedProviderFailureCategory.Unauthorized,
            CredentialUnavailableCode,
            "The shared-provider source credential is unavailable.");

    public static SharedProviderFailure SelectionConflict()
        => new(
            SharedProviderFailureCategory.Conflict,
            SelectionConflictCode,
            "The selected publications changed or are no longer authoritative. Refresh discovery and retry.");

    public static SharedProviderFailure UnexpectedNotModified()
        => new(
            SharedProviderFailureCategory.UpstreamFailure,
            UnexpectedNotModifiedCode,
            "The source returned an unexpected not-modified response.");

    public static SharedProviderFailure InvalidNotModified()
        => new(
            SharedProviderFailureCategory.UpstreamFailure,
            InvalidNotModifiedCode,
            "The source returned an invalid not-modified response.");

    public static SharedProviderFailure IdentityMismatch()
        => new(
            SharedProviderFailureCategory.Conflict,
            SharedProviderCatalogFailureCodes.SourceIdentityMismatch,
            "The source identity differs from the trusted identity.");
}
