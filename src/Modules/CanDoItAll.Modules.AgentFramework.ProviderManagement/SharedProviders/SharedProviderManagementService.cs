using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class SharedProviderManagementService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    SharedProviderPublicationStore publicationStore,
    SharedProviderPublicationApplicationService publicationApplicationService,
    SharedProviderPublicationEligibilityPolicy eligibilityPolicy,
    IProviderManifestCatalog providerManifestCatalog,
    SharedProviderSourceService sourceService,
    SharedProviderSourceSyncService sourceSyncService,
    IClock clock,
    IEnumerable<IProviderProfileCommitObserver> providerProfileCommitObservers)
    : ISharedProviderManagementService
{
    private readonly IReadOnlyList<IProviderProfileCommitObserver> commitObservers =
        providerProfileCommitObservers.ToArray();

    public async Task<SharedProviderProfileSharingSnapshot> GetProfileSharingAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default)
    {
        ValidateProviderProfileId(providerProfileId);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var imported = await LoadImportedProfileAsync(
            dbContext,
            providerProfileId,
            cancellationToken);
        if (imported is not null)
        {
            return new SharedProviderProfileSharingSnapshot(
                providerProfileId,
                SharedProviderProfileOwnership.Imported,
                Publication: null,
                Eligibility: null,
                imported);
        }

        var profile = await dbContext.Set<ProviderProfile>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == providerProfileId, cancellationToken);
        if (profile is null)
        {
            return new SharedProviderProfileSharingSnapshot(
                providerProfileId,
                SharedProviderProfileOwnership.RuntimeOnly,
                Publication: null,
                Eligibility: null,
                Import: null);
        }

        var requiredSecretExists = profile.ApiKeySecretId.HasValue &&
            await dbContext.Set<CanDoItAll.Modules.Security.SecretRecord>()
                .AsNoTracking()
                .AnyAsync(
                    secret => secret.Id == profile.ApiKeySecretId.Value,
                    cancellationToken);
        var eligibility = eligibilityPolicy.Evaluate(
            profile,
            providerManifestCatalog.ResolveManifest(
                profile.ConnectorPluginKey,
                profile.ProviderKind),
            requiredSecretExists);
        var publication = await publicationStore.FindAsync(
            providerProfileId,
            cancellationToken);
        return new SharedProviderProfileSharingSnapshot(
            providerProfileId,
            SharedProviderProfileOwnership.Local,
            publication,
            eligibility,
            Import: null);
    }

    public async Task<SharedProviderProfileSharingSnapshot> SetPublicationAsync(
        Guid providerProfileId,
        SharedProviderPublicationAction action,
        Guid? expectedConcurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await publicationApplicationService.ChangeAsync(
            new SharedProviderPublicationChangeRequest(
                providerProfileId,
                action,
                expectedConcurrencyToken),
            cancellationToken);
        return await ReadCommittedSharingAsync(providerProfileId,
            result.Change ?? new(SharedProviderChangeKind.Publication, [providerProfileId]), cancellationToken);
    }

    public async Task<IReadOnlyList<SharedProviderSourceManagementSnapshot>> ListSourcesAsync(
        CancellationToken cancellationToken = default)
    {
        var sources = await sourceService.ListAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var imports = await LoadImportedProfilesAsync(dbContext, cancellationToken);
        return Array.AsReadOnly(sources
            .Select(source => new SharedProviderSourceManagementSnapshot(
                source,
                Array.AsReadOnly(imports
                    .Where(import => import.SourceId == source.Id)
                    .OrderBy(import => import.LocalAlias, StringComparer.OrdinalIgnoreCase)
                    .ToArray())))
            .ToArray());
    }

    public Task<SharedProviderSourceWriteResult> SaveSourceAsync(
        SharedProviderSourceEditorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var writeRequest = new SharedProviderSourceWriteRequest(
            request.Name,
            request.BaseUri,
            request.ApiTokenSecretId,
            request.IsEnabled,
            request.AllowInsecurePrivateNetwork);
        if (!request.Id.HasValue)
        {
            return sourceService.CreateAsync(writeRequest, cancellationToken);
        }

        if (!request.ExpectedConcurrencyToken.HasValue)
        {
            throw new ArgumentException(
                "An existing source requires its expected concurrency token.",
                nameof(request));
        }

        return sourceService.UpdateAsync(
            request.Id.Value,
            request.ExpectedConcurrencyToken.Value,
            writeRequest,
            cancellationToken);
    }

    public Task<SharedProviderSourceWriteResult> SetSourceEnabledAsync(
        Guid sourceId,
        Guid expectedConcurrencyToken,
        bool isEnabled,
        CancellationToken cancellationToken = default)
        => sourceService.SetEnabledAsync(
            sourceId,
            expectedConcurrencyToken,
            isEnabled,
            cancellationToken);

    public Task<SharedProviderSourceDeleteResult> DeleteSourceAsync(
        Guid sourceId,
        Guid expectedConcurrencyToken,
        CancellationToken cancellationToken = default)
        => sourceService.DeleteAsync(
            sourceId,
            expectedConcurrencyToken,
            cancellationToken);

    public Task<SharedProviderSourceOperationResult> TestSourceAsync(
        Guid sourceId,
        CancellationToken cancellationToken = default)
        => sourceSyncService.TestAsync(sourceId, cancellationToken);

    public Task<SharedProviderSourceOperationResult> SynchronizeSourceAsync(
        Guid sourceId,
        IReadOnlySet<SharedProviderPublicationId> selectedPublicationIds,
        CancellationToken cancellationToken = default)
        => sourceSyncService.SynchronizeAsync(
            sourceId,
            selectedPublicationIds,
            cancellationToken);

    public async Task<SharedProviderProfileSharingSnapshot> UpdateImportedProfileAsync(
        SharedProviderImportedProfileUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var alias = NormalizeAlias(request.LocalAlias);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var (import, profile) = await LoadImportedMutationAsync(
            dbContext,
            request.ImportId,
            request.ProviderProfileId,
            cancellationToken);
        EnsureConcurrency(
            import,
            profile,
            request.ExpectedImportConcurrencyToken,
            request.ExpectedProviderConcurrencyToken);

        profile.Name = alias;
        profile.IsEnabled = request.IsEnabled;
        await SaveImportedMutationAsync(
            dbContext,
            import,
            profile,
            cancellationToken);
        var change = await SharedProviderCommitEffects.NotifySavedAsync(
            new(SharedProviderChangeKind.ImportedSettings, [profile.Id],
                retiredProviderProfileIds: [],
                remoteOwnedFieldsChanged: false, catalogMembershipMayHaveChanged: false),
            commitObservers);
        return await ReadCommittedSharingAsync(profile.Id, change, cancellationToken);
    }

    public async Task<SharedProviderProfileSharingSnapshot> RetireImportedProfileAsync(
        SharedProviderImportedProfileRetireRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var (import, profile) = await LoadImportedMutationAsync(
            dbContext,
            request.ImportId,
            request.ProviderProfileId,
            cancellationToken);
        EnsureConcurrency(
            import,
            profile,
            request.ExpectedImportConcurrencyToken,
            request.ExpectedProviderConcurrencyToken);

        SharedProviderImportTransitions.Retire(import, clock.GetUtcNow());
        await SaveImportedMutationAsync(
            dbContext,
            import,
            profile,
            cancellationToken);
        var change = await SharedProviderCommitEffects.NotifySavedAsync(
            new(SharedProviderChangeKind.ImportRetirement, [profile.Id],
                retiredProviderProfileIds: [profile.Id],
                remoteOwnedFieldsChanged: false, catalogMembershipMayHaveChanged: true),
            commitObservers);
        return await ReadCommittedSharingAsync(profile.Id, change, cancellationToken);
    }

    private async Task<SharedProviderProfileSharingSnapshot> ReadCommittedSharingAsync(
        Guid providerId, SharedProviderChange change, CancellationToken token) {
        try {
            return (await GetProfileSharingAsync(providerId, token)) with { Change = change };
        } catch (Exception exception) {
            throw new SharedProviderCommittedException(change, exception);
        }
    }

    private static async Task<SharedProviderImportedProfileSnapshot?> LoadImportedProfileAsync(
        AppDbContext dbContext,
        Guid providerProfileId,
        CancellationToken cancellationToken)
    {
        var row = await (
                from import in dbContext.Set<SharedProviderImport>().AsNoTracking()
                join source in dbContext.Set<SharedProviderSource>().AsNoTracking()
                    on import.SourceId equals source.Id
                join profile in dbContext.Set<ProviderProfile>().AsNoTracking()
                    on import.ProviderProfileId equals profile.Id
                where import.ProviderProfileId == providerProfileId
                select new ImportedProfileRow(import, source, profile))
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : CreateImportedSnapshot(row);
    }

    private static async Task<IReadOnlyList<SharedProviderImportedProfileSnapshot>> LoadImportedProfilesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var rows = await (
                from import in dbContext.Set<SharedProviderImport>().AsNoTracking()
                join source in dbContext.Set<SharedProviderSource>().AsNoTracking()
                    on import.SourceId equals source.Id
                join profile in dbContext.Set<ProviderProfile>().AsNoTracking()
                    on import.ProviderProfileId equals profile.Id
                select new ImportedProfileRow(import, source, profile))
            .ToArrayAsync(cancellationToken);
        return Array.AsReadOnly(rows.Select(CreateImportedSnapshot).ToArray());
    }

    private static SharedProviderImportedProfileSnapshot CreateImportedSnapshot(
        ImportedProfileRow row)
    {
        var hasCompatibleSnapshot = SharedProviderPublicationSnapshotReader.TryRead(row.Import, out var publication);
        return new SharedProviderImportedProfileSnapshot(
            row.Import.Id,
            row.Source.Id,
            row.Source.Name,
            row.Import.RemotePublicationId,
            row.Profile.Id,
            row.Profile.Name,
            row.Profile.IsEnabled,
            row.Import.RemoteDisplayName,
            row.Import.RemotePurpose,
            row.Import.RemoteTransport,
            row.Import.RemoteDefaultModelId,
            row.Import.SelectionState,
            hasCompatibleSnapshot ? row.Import.AvailabilityState : SharedProviderAvailabilityState.IncompatibleContract,
            hasCompatibleSnapshot ? Array.AsReadOnly(publication.Models.ToArray()) : [],
            row.Import.ConcurrencyToken,
            row.Profile.ConcurrencyToken);
    }

    private static async Task<(SharedProviderImport Import, ProviderProfile Profile)> LoadImportedMutationAsync(
        AppDbContext dbContext,
        Guid importId,
        Guid providerProfileId,
        CancellationToken cancellationToken)
    {
        if (importId == Guid.Empty || providerProfileId == Guid.Empty)
        {
            throw new ArgumentException("Imported provider identities cannot be empty.");
        }

        var import = await dbContext.Set<SharedProviderImport>()
            .SingleOrDefaultAsync(
                item => item.Id == importId && item.ProviderProfileId == providerProfileId,
                cancellationToken) ??
            throw new KeyNotFoundException(
                $"Shared-provider import '{importId:D}' was not found.");
        var profile = await dbContext.Set<ProviderProfile>()
            .SingleOrDefaultAsync(item => item.Id == providerProfileId, cancellationToken) ??
            throw new KeyNotFoundException(
                $"Provider profile '{providerProfileId:D}' was not found.");
        if (!SharedProviderProfileOwnershipPolicy.IsSourceManagedConnector(
                profile.ConnectorPluginKey))
        {
            throw new InvalidOperationException(
                "Only a source-managed provider profile can be changed through shared-provider management.");
        }

        return (import, profile);
    }

    private static void EnsureConcurrency(
        SharedProviderImport import,
        ProviderProfile profile,
        Guid expectedImportConcurrencyToken,
        Guid expectedProviderConcurrencyToken)
    {
        if (expectedImportConcurrencyToken == Guid.Empty ||
            import.ConcurrencyToken != expectedImportConcurrencyToken)
        {
            throw new SharedProviderConcurrencyException(
                nameof(SharedProviderImport),
                import.Id);
        }

        if (expectedProviderConcurrencyToken == Guid.Empty ||
            profile.ConcurrencyToken != expectedProviderConcurrencyToken)
        {
            throw new SharedProviderConcurrencyException(
                nameof(ProviderProfile),
                profile.Id);
        }
    }

    private static async Task SaveImportedMutationAsync(
        AppDbContext dbContext,
        SharedProviderImport import,
        ProviderProfile profile,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new SharedProviderConcurrencyException(
                nameof(SharedProviderImport),
                import.Id,
                exception);
        }
    }


    private static string NormalizeAlias(string alias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        var normalized = alias.Trim();
        if (normalized.Length > 200 || normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                "The local provider alias must contain at most 200 visible characters.",
                nameof(alias));
        }

        return normalized;
    }

    private static void ValidateProviderProfileId(Guid providerProfileId)
    {
        if (providerProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "The provider profile id cannot be empty.",
                nameof(providerProfileId));
        }
    }

    private sealed record ImportedProfileRow(
        SharedProviderImport Import,
        SharedProviderSource Source,
        ProviderProfile Profile);
}
