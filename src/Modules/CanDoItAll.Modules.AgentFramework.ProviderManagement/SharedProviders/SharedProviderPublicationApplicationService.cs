using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public enum SharedProviderPublicationAction
{
    Publish,
    Unpublish
}

public sealed record SharedProviderPublicationChangeRequest(
    Guid ProviderProfileId,
    SharedProviderPublicationAction Action,
    Guid? ExpectedConcurrencyToken) {
    public Guid? ExpectedProviderConcurrencyToken { get; init; }
}

public sealed class SharedProviderPublicationApplicationService(
    IDbContextFactory<ProvidersDbContext> dbContextFactory,
    SecretReferenceQuery secretReferences,
    CoordinatedDatabaseTransaction transactions,
    IProviderManifestCatalog providerManifestCatalog,
    SharedProviderPublicationEligibilityPolicy eligibilityPolicy,
    IActivityStream activityStream,
    IClock clock,
    IEnumerable<ISharedProviderPublicationCommitObserver> commitObservers,
    ProviderEditingWrites editing,
    ICanonicalRuntimeDatabase database)
{
    private const string ActivityCategory = "providers";
    private const string PublishActivityAction = "publish";
    private const string UnpublishActivityAction = "unpublish";
    private const string ActivityArtifactKind = "provider-share-publication";
    private const string ActivityRoute = "/agents?tab=providers";

    private readonly IReadOnlyList<ISharedProviderPublicationCommitObserver> observers =
        commitObservers.ToArray();

    public async Task<SharedProviderPublicationWriteResult> ChangeAsync(
        SharedProviderPublicationChangeRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExpectedProviderConcurrencyToken is not { } originalProvider || originalProvider == Guid.Empty) {
            throw new SharedProviderConcurrencyException(nameof(ProviderProfile), request.ProviderProfileId);
        }
        if (request.ProviderProfileId == Guid.Empty || request.ExpectedConcurrencyToken == Guid.Empty ||
            !Enum.IsDefined(request.Action)) {
            throw new ArgumentException("A valid provider, publication action and optional expected revision are required.", nameof(request));
        }
        await editing.RequireAsync(database.Profile.Profile.Id, request.ProviderProfileId, cancellationToken);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutation = await SerializableMutationScope.BeginAsync(
            db, $"shared-provider-publication:{request.ProviderProfileId:D}", cancellationToken);
        using var coordination = transactions.Enter(db);
        var profile = await LoadOriginalAsync(db, request.ProviderProfileId, originalProvider, cancellationToken);
        var publication = await db.Set<ProviderSharePublication>()
            .SingleOrDefaultAsync(item => item.ProviderProfileId == request.ProviderProfileId, cancellationToken);
        var publish = request.Action == SharedProviderPublicationAction.Publish;
        var secretExists = profile.ApiKeySecretId.HasValue &&
            await secretReferences.ExistsForMutationAsync(profile.ApiKeySecretId.Value, cancellationToken);
        var eligibility = eligibilityPolicy.Evaluate(profile,
            providerManifestCatalog.ResolveManifest(profile.ConnectorPluginKey, profile.ProviderKind), secretExists);
        if (publication is null) {
            if (request.ExpectedConcurrencyToken.HasValue || !publish) {
                throw new SharedProviderConcurrencyException(nameof(ProviderSharePublication), profile.Id);
            }
        } else {
            if (publication.ConcurrencyToken != request.ExpectedConcurrencyToken) {
                throw new SharedProviderConcurrencyException(nameof(ProviderSharePublication), publication.Id);
            }
            if (publication.IsPublished == publish) {
                await editing.RequireAsync(database.Profile.Profile.Id, request.ProviderProfileId, cancellationToken);
                return ToResult(publication, profile, eligibility);
            }
        }
        if (publish && !eligibility.IsEligible) {
            throw new SharedProviderPublicationEligibilityException(profile.Id, eligibility);
        }
        var now = clock.GetUtcNow();
        if (publication is null) {
            publication = SharedProviderPublicationTransitions.Create(profile.Id,
                SharedProviderPublicationStore.CreatePublicId(profile.Id), now);
            db.Add(publication);
        }
        if (publish) {
            SharedProviderPublicationTransitions.Publish(publication, now);
        } else {
            SharedProviderPublicationTransitions.Unpublish(publication, now);
        }
        var change = new SharedProviderChange(SharedProviderChangeKind.Publication, [profile.Id]);
        var committed = false;
        try {
            cancellationToken.ThrowIfCancellationRequested();
            await editing.RequireAsync(database.Profile.Profile.Id, request.ProviderProfileId, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await editing.RequireAsync(database.Profile.Profile.Id, request.ProviderProfileId, cancellationToken);
            committed = db.Database.CurrentTransaction is null;
            await mutation.CommitAsync(cancellationToken);
            committed = true;
            coordination.Dispose();
            await mutation.DisposeAsync();
        } catch (Exception) when (committed) {
            change = change with { Warning = "The publication is saved, but transaction cleanup needs attention." };
        } catch (Exception exception) when (
            exception is DbUpdateConcurrencyException || SerializableMutationScope.IsConflict(exception) ||
            SharedProviderPersistenceConflictClassifier.IsPublicationProviderIdentityConflict(exception)) {
            throw new SharedProviderConcurrencyException(nameof(ProviderSharePublication), profile.Id, exception);
        }
        foreach (var observer in observers) {
            change = await SharedProviderCommitEffects.CompleteAsync(change,
                () => observer.PublicationChangedAsync(profile.Id, CancellationToken.None));
        }
        change = await SharedProviderCommitEffects.CompleteAsync(change, () => activityStream.RecordAsync(
            new ActivityWriteRequest(ActivityCategory,
                publish ? PublishActivityAction : UnpublishActivityAction,
                publish ? "Published provider profile" : "Unpublished provider profile",
                profile.Name, ArtifactKind: ActivityArtifactKind, ArtifactId: publication.Id, Route: ActivityRoute),
            CancellationToken.None));
        return ToResult(publication, profile, eligibility) with { Change = change };
    }

    private static async Task<ProviderProfile> LoadOriginalAsync(ProvidersDbContext db, Guid id, Guid original,
        CancellationToken cancellationToken) {
        try {
            var profiles = db.Database.IsNpgsql()
                ? db.Set<ProviderProfile>().FromSqlInterpolated($"SELECT * FROM \"Workspace_ProviderProfiles\" WHERE \"Id\" = {id} AND \"ConcurrencyToken\" = {original} FOR UPDATE")
                : db.Set<ProviderProfile>().Where(profile => profile.Id == id && profile.ConcurrencyToken == original);
            return await profiles.AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw new SharedProviderConcurrencyException(nameof(ProviderProfile), id);
        } catch (Exception exception) when (SerializableMutationScope.IsConflict(exception)) {
            throw new SharedProviderConcurrencyException(nameof(ProviderProfile), id, exception);
        }
    }

    private static SharedProviderPublicationWriteResult ToResult(
        ProviderSharePublication publication, ProviderProfile profile, SharedProviderPublicationEligibility eligibility)
        => new(
            publication.Id,
            publication.PublicId,
            publication.IsPublished,
            publication.ConcurrencyToken) {
                Acceptance = new(profile.ConcurrencyToken, eligibility)
            };
}
