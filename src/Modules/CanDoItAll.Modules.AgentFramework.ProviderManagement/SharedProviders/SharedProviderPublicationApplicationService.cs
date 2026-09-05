using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public enum SharedProviderPublicationAction
{
    Publish,
    Unpublish
}

public sealed record SharedProviderPublicationChangeRequest(
    Guid ProviderProfileId,
    SharedProviderPublicationAction Action,
    Guid? ExpectedConcurrencyToken);

public sealed class SharedProviderPublicationApplicationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IProviderManifestCatalog providerManifestCatalog,
    SharedProviderPublicationEligibilityPolicy eligibilityPolicy,
    IActivityStream activityStream,
    IClock clock,
    IEnumerable<ISharedProviderPublicationCommitObserver> commitObservers)
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
        if (request.ProviderProfileId == Guid.Empty || request.ExpectedConcurrencyToken == Guid.Empty ||
            !Enum.IsDefined(request.Action)) {
            throw new ArgumentException("A valid provider, publication action and optional expected revision are required.", nameof(request));
        }
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutation = await SerializableMutationScope.BeginAsync(
            db, $"shared-provider-publication:{request.ProviderProfileId:D}", cancellationToken);
        var profile = await db.Set<ProviderProfile>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.ProviderProfileId, cancellationToken)
            ?? throw new KeyNotFoundException("The provider profile was not found.");
        var publication = await db.Set<ProviderSharePublication>()
            .SingleOrDefaultAsync(item => item.ProviderProfileId == request.ProviderProfileId, cancellationToken);
        var publish = request.Action == SharedProviderPublicationAction.Publish;
        if (publication is null) {
            if (request.ExpectedConcurrencyToken.HasValue || !publish) {
                throw new SharedProviderConcurrencyException(nameof(ProviderSharePublication), profile.Id);
            }
        } else {
            if (publication.ConcurrencyToken != request.ExpectedConcurrencyToken) {
                throw new SharedProviderConcurrencyException(nameof(ProviderSharePublication), publication.Id);
            }
            if (publication.IsPublished == publish) {
                return ToResult(publication);
            }
        }
        if (publish) {
            var secretExists = profile.ApiKeySecretId.HasValue &&
                await db.Set<SecretRecord>().AsNoTracking().AnyAsync(
                    secret => secret.Id == profile.ApiKeySecretId.Value, cancellationToken);
            var eligibility = eligibilityPolicy.Evaluate(profile,
                providerManifestCatalog.ResolveManifest(profile.ConnectorPluginKey, profile.ProviderKind), secretExists);
            if (!eligibility.IsEligible) {
                throw new SharedProviderPublicationEligibilityException(profile.Id, eligibility);
            }
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
            await db.SaveChangesAsync(cancellationToken);
            committed = db.Database.CurrentTransaction is null;
            await mutation.CommitAsync(cancellationToken);
            committed = true;
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
        return ToResult(publication) with { Change = change };
    }

    private static SharedProviderPublicationWriteResult ToResult(
        ProviderSharePublication publication)
        => new(
            publication.Id,
            publication.PublicId,
            publication.IsPublished,
            publication.ConcurrencyToken);
}
