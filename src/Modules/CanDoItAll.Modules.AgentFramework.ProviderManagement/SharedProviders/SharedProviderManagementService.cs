using CanDoItAll.Modules.Security;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;
using CanDoItAll.Infrastructure.ControlPlane;
using System.Data;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class SharedProviderManagementService(
    IDbContextFactory<ProvidersDbContext> dbContextFactory,
    SecretReferenceQuery secretReferences,
    SharedProviderPublicationApplicationService publicationApplicationService,
    SharedProviderPublicationEligibilityPolicy eligibilityPolicy,
    IProviderManifestCatalog providerManifestCatalog,
    SharedProviderSourceService sourceService,
    SharedProviderSourceSyncService sourceSyncService,
    IClock clock,
    IEnumerable<IProviderProfileCommitObserver> providerProfileCommitObservers,
    ProviderEditingWrites editing,
    ICanonicalRuntimeDatabase database,
    CoordinatedDatabaseTransaction transactions,
    ProviderEditingChanges changes)
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
        await using var read = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken) : null;
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

        var requiredSecretExists = profile.ApiKeySecretId is { } secretId &&
            (await secretReferences.GetExistingIdsAsync([secretId], cancellationToken)).Contains(secretId);
        var eligibility = eligibilityPolicy.Evaluate(
            profile,
            providerManifestCatalog.ResolveManifest(
                profile.ConnectorPluginKey,
                profile.ProviderKind),
            requiredSecretExists);
        var publication = await dbContext.Set<ProviderSharePublication>().AsNoTracking()
            .Where(item => item.ProviderProfileId == providerProfileId)
            .Select(item => new SharedProviderPublicationWriteResult(item.Id, item.PublicId, item.IsPublished, item.ConcurrencyToken))
            .SingleOrDefaultAsync(cancellationToken);
        return new SharedProviderProfileSharingSnapshot(
            providerProfileId,
            SharedProviderProfileOwnership.Local,
            publication,
            eligibility,
            Import: null) { ProviderConcurrencyToken = profile.ConcurrencyToken };
    }

    public async Task<SharedProviderProfileSharingSnapshot> SetPublicationAsync(
        Guid providerProfileId,
        SharedProviderPublicationAction action,
        Guid? expectedConcurrencyToken,
        Guid expectedProviderConcurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await publicationApplicationService.ChangeAsync(
            new SharedProviderPublicationChangeRequest(
                providerProfileId,
                action,
                expectedConcurrencyToken) { ExpectedProviderConcurrencyToken = expectedProviderConcurrencyToken },
            cancellationToken);
        var accepted = result.Acceptance ?? throw new InvalidOperationException("The publication owner did not return its exact accepted configuration.");
        return new(providerProfileId, SharedProviderProfileOwnership.Local, result, accepted.Eligibility, null) {
            ProviderConcurrencyToken = accepted.ProviderConcurrencyToken, Change = result.Change
        };
    }

    public async Task<IReadOnlyList<SharedProviderSourceManagementSnapshot>> ListSourcesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var read = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken) : null;
        var sources = (await dbContext.Set<SharedProviderSource>().AsNoTracking().OrderBy(source => source.Name).ThenBy(source => source.Id)
            .ToArrayAsync(cancellationToken)).Select(SharedProviderSourceService.CreateSnapshot).ToArray();
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

    public async Task<SharedProviderSourceVerificationResult> VerifySourceAsync(
        SharedProviderSourceMutationAttempt attempt, CancellationToken cancellationToken = default) {
        var sources = await ListSourcesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return SharedProviderSourceVerification.Evaluate(attempt, sources);
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
            request.AllowInsecurePrivateNetwork, request.Id);
        if (request.Id is null || request.ExpectedConcurrencyToken == Guid.Empty) {
            if (request.ExpectedConcurrencyToken is { } unexpected && unexpected != Guid.Empty) {
                throw new SharedProviderConcurrencyException(nameof(SharedProviderSource), Guid.Empty);
            }
            return sourceService.CreateAsync(writeRequest, cancellationToken);
        }

        if (!request.ExpectedConcurrencyToken.HasValue)
        {
            throw new SharedProviderConcurrencyException(nameof(SharedProviderSource), request.Id!.Value);
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

    public Task<SharedProviderSourceOperationResult> SynchronizeSourceAsync(Guid sourceId, SharedProviderSourceObservation original,
        IReadOnlySet<SharedProviderPublicationId> selectedPublicationIds, CancellationToken cancellationToken = default)
        => sourceSyncService.SynchronizeAsync(sourceId, original, selectedPublicationIds, cancellationToken);

    public Task<SharedProviderProfileSharingSnapshot> UpdateImportedProfileAsync(
        SharedProviderImportedProfileUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var alias = SharedProviderLocalAliasPolicy.Normalize(request.LocalAlias);
        return MutateImportAsync(request.ImportId, request.ProviderProfileId,
            (request.ExpectedImportConcurrencyToken, request.ExpectedProviderConcurrencyToken),
            (_, profile) => {
                profile.Name = alias;
                profile.IsEnabled = request.IsEnabled;
            }, SharedProviderChangeKind.ImportedSettings, cancellationToken);
    }

    public Task<SharedProviderProfileSharingSnapshot> RetireImportedProfileAsync(
        SharedProviderImportedProfileRetireRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return MutateImportAsync(request.ImportId, request.ProviderProfileId,
            (request.ExpectedImportConcurrencyToken, request.ExpectedProviderConcurrencyToken),
            (import, _) => SharedProviderImportTransitions.Retire(import, clock.GetUtcNow()),
            SharedProviderChangeKind.ImportRetirement, cancellationToken);
    }

    private async Task<SharedProviderProfileSharingSnapshot> MutateImportAsync(Guid importId, Guid providerId,
        (Guid Import, Guid Provider) expected, Action<SharedProviderImport, ProviderProfile> apply,
        SharedProviderChangeKind kind, CancellationToken cancellationToken) {
        if (importId == Guid.Empty || providerId == Guid.Empty) {
            throw new ArgumentException("Imported provider identities cannot be empty.");
        }
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var sourceId = await dbContext.Set<SharedProviderImport>().AsNoTracking()
            .Where(import => import.Id == importId && import.ProviderProfileId == providerId)
            .Select(import => (Guid?)import.SourceId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("The imported provider was not found.");
        await using var mutation = await SharedProviderSourceService.BeginMutationAsync(dbContext, sourceId, cancellationToken);
        using var coordination = transactions.Enter(dbContext);
        var (import, profile) = await LoadImportedMutationAsync(dbContext, importId, providerId, cancellationToken);
        EnsureConcurrency(import, profile, expected.Import, expected.Provider);
        var source = await dbContext.Set<SharedProviderSource>().AsNoTracking().SingleAsync(item => item.Id == sourceId, cancellationToken);
        apply(import, profile);
        SharedProviderImportedProfileSnapshot accepted;
        try {
            await editing.RequireAsync(database.Profile.Profile.Id, providerId, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await editing.RequireAsync(database.Profile.Profile.Id, providerId, cancellationToken);
            accepted = CreateImportedSnapshot(new(import, source, profile));
            await mutation.CommitAsync(cancellationToken);
        } catch (Exception exception) when (exception is DbUpdateConcurrencyException || SerializableMutationScope.IsConflict(exception)) {
            throw new SharedProviderConcurrencyException(nameof(SharedProviderImport), importId, exception);
        }
        var retired = kind == SharedProviderChangeKind.ImportRetirement;
        var change = new SharedProviderChange(kind, [providerId], retired ? [providerId] : [],
            remoteOwnedFieldsChanged: false, catalogMembershipMayHaveChanged: retired);
        try {
            coordination.Dispose();
        } catch (Exception) {
            change = change with { Warning = "The imported provider is saved, but transaction cleanup needs attention." };
        }
        change = await SharedProviderCommitEffects.CompleteAsync(change, async () => await mutation.DisposeAsync());
        if (retired) {
            change = await SharedProviderCommitEffects.CompleteAsync(change, () => {
                changes.SourcesChanged(sourceId);
                return Task.CompletedTask;
            });
        }
        change = await SharedProviderCommitEffects.NotifySavedAsync(change, commitObservers);
        return new(providerId, SharedProviderProfileOwnership.Imported, null, null, accepted) { Change = change };
    }

    private static async Task<SharedProviderImportedProfileSnapshot?> LoadImportedProfileAsync(
        ProvidersDbContext dbContext,
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
        ProvidersDbContext dbContext,
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
        ProvidersDbContext dbContext,
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
