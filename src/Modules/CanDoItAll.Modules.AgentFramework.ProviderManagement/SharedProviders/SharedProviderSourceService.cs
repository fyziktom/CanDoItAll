using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class SharedProviderSourceService
{
    private const int MaximumPersistedStatusMessageLength = 400;

    private readonly IDbContextFactory<AppDbContext> dbContextFactory;
    private readonly IClock clock;
    private readonly IReadOnlyList<IProviderProfileCommitObserver> providerProfileCommitObservers;
    private readonly ISharedProviderSourceUriPolicy sourceUriPolicy;

    public SharedProviderSourceService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IClock clock,
        IEnumerable<IProviderProfileCommitObserver> providerProfileCommitObservers,
        ISharedProviderSourceUriPolicy sourceUriPolicy)
    {
        this.dbContextFactory = dbContextFactory;
        this.clock = clock;
        this.providerProfileCommitObservers = providerProfileCommitObservers.ToArray();
        this.sourceUriPolicy = sourceUriPolicy;
    }

    public async Task<IReadOnlyList<SharedProviderSourceSnapshot>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var sources = await dbContext.Set<SharedProviderSource>()
            .AsNoTracking()
            .OrderBy(source => source.Name)
            .ThenBy(source => source.Id)
            .ToArrayAsync(cancellationToken);
        return Array.AsReadOnly(sources.Select(CreateSnapshot).ToArray());
    }

    public async Task<SharedProviderSourceSnapshot> GetAsync(
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        ValidateSourceId(sourceId);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var source = await dbContext.Set<SharedProviderSource>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == sourceId, cancellationToken)
            ?? throw SourceNotFound(sourceId);
        return CreateSnapshot(source);
    }

    public async Task<SharedProviderSourceWriteResult> CreateAsync(
        SharedProviderSourceWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var canonicalBaseUri = NormalizeBaseUri(request);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureSecretExistsAsync(dbContext, request.ApiTokenSecretId, cancellationToken);
        var source = SharedProviderSourceTransitions.Create(
            request.Name,
            canonicalBaseUri.AbsoluteUri,
            request.ApiTokenSecretId,
            request.AllowInsecurePrivateNetwork,
            request.IsEnabled,
            clock.GetUtcNow());
        dbContext.Add(source);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new SharedProviderSourceWriteResult(source.Id, source.ConcurrencyToken);
    }

    public async Task<SharedProviderSourceWriteResult> UpdateAsync(
        Guid sourceId,
        Guid expectedConcurrencyToken,
        SharedProviderSourceWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateMutationIdentity(sourceId, expectedConcurrencyToken);
        var canonicalBaseUri = NormalizeBaseUri(request);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await BeginMutationAsync(
            dbContext,
            sourceId,
            cancellationToken);
        await EnsureSecretExistsAsync(dbContext, request.ApiTokenSecretId, cancellationToken);
        var source = await LoadSourceAsync(dbContext, sourceId, cancellationToken);
        EnsureConcurrencyToken(source, expectedConcurrencyToken);
        SharedProviderSourceTransitions.UpdateConfiguration(
            source,
            request.Name,
            canonicalBaseUri.AbsoluteUri,
            request.ApiTokenSecretId,
            request.AllowInsecurePrivateNetwork,
            request.IsEnabled,
            clock.GetUtcNow());
        var affectedProviderIds = await PropagateEffectiveSourceConfigurationAsync(
            dbContext,
            source,
            cancellationToken);
        await SaveAndCommitAsync(dbContext, mutationScope, sourceId, cancellationToken);
        await NotifySavedAsync(affectedProviderIds, CancellationToken.None);
        return new SharedProviderSourceWriteResult(source.Id, source.ConcurrencyToken);
    }

    public async Task<SharedProviderSourceWriteResult> SetEnabledAsync(
        Guid sourceId,
        Guid expectedConcurrencyToken,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        ValidateMutationIdentity(sourceId, expectedConcurrencyToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await BeginMutationAsync(
            dbContext,
            sourceId,
            cancellationToken);
        var source = await LoadSourceAsync(dbContext, sourceId, cancellationToken);
        EnsureConcurrencyToken(source, expectedConcurrencyToken);
        if (source.IsEnabled == isEnabled)
        {
            await mutationScope.CommitAsync(cancellationToken);
            return new SharedProviderSourceWriteResult(source.Id, source.ConcurrencyToken);
        }

        SharedProviderSourceTransitions.SetEnabled(source, isEnabled, clock.GetUtcNow());
        var affectedProviderIds = await GetProviderIdsAsync(
            dbContext,
            sourceId,
            cancellationToken);
        await SaveAndCommitAsync(dbContext, mutationScope, sourceId, cancellationToken);
        await NotifySavedAsync(affectedProviderIds, CancellationToken.None);
        return new SharedProviderSourceWriteResult(source.Id, source.ConcurrencyToken);
    }

    public async Task<SharedProviderSourceWriteResult> ResetTrustedIdentityAsync(
        Guid sourceId,
        Guid expectedConcurrencyToken,
        CancellationToken cancellationToken = default)
    {
        ValidateMutationIdentity(sourceId, expectedConcurrencyToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await BeginMutationAsync(
            dbContext,
            sourceId,
            cancellationToken);
        var source = await LoadSourceAsync(dbContext, sourceId, cancellationToken);
        EnsureConcurrencyToken(source, expectedConcurrencyToken);
        SharedProviderSourceTransitions.ResetTrustedIdentity(source, clock.GetUtcNow());
        var affectedProviderIds = await GetProviderIdsAsync(
            dbContext,
            sourceId,
            cancellationToken);
        await SaveAndCommitAsync(dbContext, mutationScope, sourceId, cancellationToken);
        await NotifySavedAsync(affectedProviderIds, CancellationToken.None);
        return new SharedProviderSourceWriteResult(source.Id, source.ConcurrencyToken);
    }

    public async Task<SharedProviderSourceDeleteResult> DeleteAsync(
        Guid sourceId,
        Guid expectedConcurrencyToken,
        CancellationToken cancellationToken = default)
    {
        ValidateMutationIdentity(sourceId, expectedConcurrencyToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await BeginMutationAsync(
            dbContext,
            sourceId,
            cancellationToken);
        var source = await LoadSourceAsync(dbContext, sourceId, cancellationToken);
        EnsureConcurrencyToken(source, expectedConcurrencyToken);
        var importCount = await dbContext.Set<SharedProviderImport>()
            .CountAsync(import => import.SourceId == sourceId, cancellationToken);
        if (importCount > 0)
        {
            throw new SharedProviderSourceDeletionBlockedException(sourceId, importCount);
        }

        dbContext.Remove(source);
        await SaveAndCommitAsync(dbContext, mutationScope, sourceId, cancellationToken);
        return new SharedProviderSourceDeleteResult(sourceId);
    }

    public async Task<SharedProviderCatalogIdentityAcceptance> RecordSuccessfulCatalogTestAsync(
        Guid sourceId,
        Guid expectedConcurrencyToken,
        SharedProviderSourceInstanceId remoteInstanceId,
        SharedProviderCatalogEntityTag entityTag,
        CancellationToken cancellationToken = default)
    {
        ValidateMutationIdentity(sourceId, expectedConcurrencyToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await BeginMutationAsync(
            dbContext,
            sourceId,
            cancellationToken);
        var source = await LoadSourceAsync(dbContext, sourceId, cancellationToken);
        EnsureConcurrencyToken(source, expectedConcurrencyToken);
        var imports = await dbContext.Set<SharedProviderImport>()
            .Where(import => import.SourceId == sourceId)
            .ToArrayAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var acceptance = SharedProviderSourceTransitions.ApplySuccessfulConnectionTest(
            source,
            remoteInstanceId,
            entityTag,
            now);
        Guid[] affectedProviderIds = [];
        if (acceptance == SharedProviderCatalogIdentityAcceptance.IdentityMismatch)
        {
            foreach (var import in imports)
            {
                SharedProviderImportTransitions.MarkTransientlyUnavailable(
                    import,
                    SharedProviderAvailabilityState.SourceIdentityMismatch,
                    now);
            }

            affectedProviderIds = imports
                .Select(import => import.ProviderProfileId)
                .Distinct()
                .ToArray();
        }

        await SaveAndCommitAsync(dbContext, mutationScope, sourceId, cancellationToken);
        await NotifySavedAsync(affectedProviderIds, CancellationToken.None);
        return acceptance;
    }

    public Task RecordFailureAsync(
        Guid sourceId,
        SharedProviderSourceFailure failure,
        CancellationToken cancellationToken = default)
        => RecordFailureCoreAsync(
            sourceId,
            expectedConcurrencyToken: null,
            failure,
            cancellationToken);

    internal Task RecordFetchFailureAsync(
        Guid sourceId,
        Guid expectedConcurrencyToken,
        SharedProviderSourceFailure failure,
        CancellationToken cancellationToken = default)
    {
        ValidateMutationIdentity(sourceId, expectedConcurrencyToken);
        return RecordFailureCoreAsync(
            sourceId,
            expectedConcurrencyToken,
            failure,
            cancellationToken);
    }

    private async Task RecordFailureCoreAsync(
        Guid sourceId,
        Guid? expectedConcurrencyToken,
        SharedProviderSourceFailure failure,
        CancellationToken cancellationToken)
    {
        ValidateSourceId(sourceId);
        ArgumentNullException.ThrowIfNull(failure);
        var (sourceStatus, importAvailability) = failure.Kind switch
        {
            SharedProviderSourceFailureKind.Connectivity => (
                SharedProviderSourceStatus.SourceOffline,
                SharedProviderAvailabilityState.SourceOffline),
            SharedProviderSourceFailureKind.Authorization => (
                SharedProviderSourceStatus.AuthorizationFailed,
                SharedProviderAvailabilityState.AuthorizationFailed),
            SharedProviderSourceFailureKind.IncompatibleContract => (
                SharedProviderSourceStatus.IncompatibleContract,
                SharedProviderAvailabilityState.IncompatibleContract),
            SharedProviderSourceFailureKind.IdentityMismatch => (
                SharedProviderSourceStatus.SourceIdentityMismatch,
                SharedProviderAvailabilityState.SourceIdentityMismatch),
            _ => throw new ArgumentOutOfRangeException(
                nameof(failure),
                failure.Kind,
                "Unknown source failure kind.")
        };

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await BeginMutationAsync(
            dbContext,
            sourceId,
            cancellationToken);
        var source = await LoadSourceAsync(dbContext, sourceId, cancellationToken);
        if (expectedConcurrencyToken is { } expectedToken)
        {
            EnsureConcurrencyToken(source, expectedToken);
        }

        var imports = await dbContext.Set<SharedProviderImport>()
            .Where(item => item.SourceId == sourceId)
            .ToArrayAsync(cancellationToken);
        var now = clock.GetUtcNow();
        SharedProviderSourceTransitions.ApplyFailure(
            source,
            sourceStatus,
            failure.StatusCode,
            BoundStatusMessage(failure.SanitizedMessage),
            now);
        foreach (var import in imports)
        {
            SharedProviderImportTransitions.MarkTransientlyUnavailable(
                import,
                importAvailability,
                now);
        }

        await SaveAndCommitAsync(dbContext, mutationScope, sourceId, cancellationToken);
        await NotifySavedAsync(
            imports.Select(import => import.ProviderProfileId),
            CancellationToken.None);
    }

    private Uri NormalizeBaseUri(SharedProviderSourceWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.BaseUri);
        return sourceUriPolicy.Normalize(
            request.BaseUri,
            ToNetworkPolicy(request.AllowInsecurePrivateNetwork));
    }

    private static SharedProviderSourceSnapshot CreateSnapshot(SharedProviderSource source)
        => new(
            source.Id,
            source.Name,
            new Uri(source.BaseUri, UriKind.Absolute),
            source.ApiTokenSecretId,
            source.IsEnabled,
            ToNetworkPolicy(source.AllowInsecurePrivateNetwork),
            source.Status,
            source.RemoteInstanceId,
            source.LastCatalogETag,
            source.LastSyncAtUtc,
            source.LastStatusCode,
            source.LastStatusMessage,
            source.ConcurrencyToken);

    private static SharedProviderSourceNetworkPolicy ToNetworkPolicy(
        bool allowInsecurePrivateNetwork)
        => allowInsecurePrivateNetwork
            ? SharedProviderSourceNetworkPolicy.AllowPrivateNetwork
            : SharedProviderSourceNetworkPolicy.PublicOnly;

    private static async Task EnsureSecretExistsAsync(
        AppDbContext dbContext,
        Guid secretId,
        CancellationToken cancellationToken)
    {
        if (secretId == Guid.Empty || !await dbContext.Set<SecretRecord>()
                .AsNoTracking()
                .AnyAsync(secret => secret.Id == secretId, cancellationToken))
        {
            throw new ArgumentException(
                "The source must reference an existing secret record.",
                nameof(secretId));
        }
    }

    private static async Task<Guid[]> PropagateEffectiveSourceConfigurationAsync(
        AppDbContext dbContext,
        SharedProviderSource source,
        CancellationToken cancellationToken)
    {
        var providerIds = await GetProviderIdsAsync(
            dbContext,
            source.Id,
            cancellationToken);
        if (providerIds.Length == 0)
        {
            return [];
        }

        var profiles = await dbContext.Set<ProviderProfile>()
            .Where(profile => providerIds.Contains(profile.Id))
            .ToArrayAsync(cancellationToken);
        var effectiveBaseUri = SharedProviderRoutes.ResolveOpenAiBase(
            new Uri(source.BaseUri, UriKind.Absolute)).AbsoluteUri;
        foreach (var profile in profiles)
        {
            profile.BaseUrl = effectiveBaseUri;
            profile.ApiKeySecretId = source.ApiTokenSecretId;
        }

        return profiles.Select(profile => profile.Id).ToArray();
    }

    private static Task<Guid[]> GetProviderIdsAsync(
        AppDbContext dbContext,
        Guid sourceId,
        CancellationToken cancellationToken)
        => dbContext.Set<SharedProviderImport>()
            .Where(import => import.SourceId == sourceId)
            .Select(import => import.ProviderProfileId)
            .ToArrayAsync(cancellationToken);

    private static async Task<SharedProviderSource> LoadSourceAsync(
        AppDbContext dbContext,
        Guid sourceId,
        CancellationToken cancellationToken)
        => await dbContext.Set<SharedProviderSource>()
            .SingleOrDefaultAsync(item => item.Id == sourceId, cancellationToken)
            ?? throw SourceNotFound(sourceId);

    private static Task<SerializableMutationScope> BeginMutationAsync(
        AppDbContext dbContext,
        Guid sourceId,
        CancellationToken cancellationToken)
        => SerializableMutationScope.BeginAsync(
            dbContext,
            $"shared-provider-source:{sourceId:D}",
            cancellationToken);

    private static async Task SaveAndCommitAsync(
        AppDbContext dbContext,
        SerializableMutationScope mutationScope,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await mutationScope.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (
            exception is DbUpdateConcurrencyException ||
            SerializableMutationScope.IsConflict(exception))
        {
            throw new SharedProviderConcurrencyException(
                nameof(SharedProviderSource),
                sourceId,
                exception);
        }
    }

    private async Task NotifySavedAsync(
        IEnumerable<Guid> providerIds,
        CancellationToken cancellationToken)
    {
        foreach (var providerId in providerIds.Distinct().Order())
        {
            foreach (var observer in providerProfileCommitObservers)
            {
                await observer.ProviderSavedAsync(providerId, cancellationToken);
            }
        }
    }

    private static void ValidateMutationIdentity(
        Guid sourceId,
        Guid expectedConcurrencyToken)
    {
        ValidateSourceId(sourceId);
        if (expectedConcurrencyToken == Guid.Empty)
        {
            throw new ArgumentException(
                "The expected concurrency token cannot be empty.",
                nameof(expectedConcurrencyToken));
        }
    }

    private static void ValidateSourceId(Guid sourceId)
    {
        if (sourceId == Guid.Empty)
        {
            throw new ArgumentException("The source id cannot be empty.", nameof(sourceId));
        }
    }

    private static void EnsureConcurrencyToken(
        SharedProviderSource source,
        Guid expectedConcurrencyToken)
    {
        if (source.ConcurrencyToken != expectedConcurrencyToken)
        {
            throw new SharedProviderConcurrencyException(
                nameof(SharedProviderSource),
                source.Id);
        }
    }

    private static string BoundStatusMessage(string sanitizedMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sanitizedMessage);
        return sanitizedMessage.Length <= MaximumPersistedStatusMessageLength
            ? sanitizedMessage
            : sanitizedMessage[..MaximumPersistedStatusMessageLength];
    }

    private static KeyNotFoundException SourceNotFound(Guid sourceId)
        => new($"Shared-provider source '{sourceId:D}' was not found.");
}
