using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workspace;

public enum SharedProviderPublicationAction
{
    Publish,
    Unpublish
}

public sealed record SharedProviderPublicationChangeRequest(
    Guid ProviderProfileId,
    SharedProviderPublicationAction Action,
    Guid ExpectedConcurrencyToken);

public sealed class SharedProviderPublicationApplicationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ProviderRegistry providerRegistry,
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProviderProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "The provider profile id cannot be empty.",
                nameof(request));
        }

        if (request.ExpectedConcurrencyToken == Guid.Empty)
        {
            throw new ArgumentException(
                "The expected publication concurrency token cannot be empty.",
                nameof(request));
        }

        if (!Enum.IsDefined(request.Action))
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var profile = await dbContext.Set<ProviderProfile>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == request.ProviderProfileId,
                cancellationToken) ??
            throw new KeyNotFoundException(
                $"Provider profile '{request.ProviderProfileId:D}' was not found.");
        var publication = await dbContext.Set<ProviderSharePublication>()
            .SingleOrDefaultAsync(
                item => item.ProviderProfileId == request.ProviderProfileId,
                cancellationToken) ??
            throw new KeyNotFoundException(
                $"Provider profile '{request.ProviderProfileId:D}' does not have a publication identity.");
        if (publication.ConcurrencyToken != request.ExpectedConcurrencyToken)
        {
            throw new SharedProviderConcurrencyException(
                nameof(ProviderSharePublication),
                publication.Id);
        }

        var targetPublishedState = request.Action == SharedProviderPublicationAction.Publish;
        if (targetPublishedState == publication.IsPublished)
        {
            return ToResult(publication);
        }

        if (targetPublishedState)
        {
            var requiredSecretExists = profile.ApiKeySecretId.HasValue &&
                await dbContext.Set<SecretRecord>()
                    .AsNoTracking()
                    .AnyAsync(
                        secret => secret.Id == profile.ApiKeySecretId.Value,
                        cancellationToken);
            var eligibility = eligibilityPolicy.Evaluate(
                profile,
                providerRegistry.Resolve(profile)?.Manifest,
                requiredSecretExists);
            if (!eligibility.IsEligible)
            {
                throw new SharedProviderPublicationEligibilityException(
                    profile.Id,
                    eligibility);
            }
        }

        var changedAtUtc = clock.GetUtcNow();
        if (targetPublishedState)
        {
            SharedProviderPublicationTransitions.Publish(publication, changedAtUtc);
        }
        else
        {
            SharedProviderPublicationTransitions.Unpublish(publication, changedAtUtc);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new SharedProviderConcurrencyException(
                nameof(ProviderSharePublication),
                publication.Id,
                exception);
        }

        foreach (var observer in observers)
        {
            await observer.PublicationChangedAsync(profile.Id, CancellationToken.None);
        }

        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                ActivityCategory,
                targetPublishedState ? PublishActivityAction : UnpublishActivityAction,
                targetPublishedState
                    ? "Published provider profile"
                    : "Unpublished provider profile",
                profile.Name,
                ArtifactKind: ActivityArtifactKind,
                ArtifactId: publication.Id,
                Route: ActivityRoute),
            CancellationToken.None);
        return ToResult(publication);
    }

    private static SharedProviderPublicationWriteResult ToResult(
        ProviderSharePublication publication)
        => new(
            publication.Id,
            publication.PublicId,
            publication.IsPublished,
            publication.ConcurrencyToken);
}
