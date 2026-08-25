using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workspace;

public sealed class SharedProviderImageExecutionTarget
{
    public SharedProviderImageExecutionTarget(ProviderProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Profile = profile;
    }

    public ProviderProfile Profile { get; }
}

public interface ISharedProviderImageExecutionTargetResolver
{
    Task<SharedProviderImageExecutionTarget?> ResolveAsync(
        SharedProviderImageCapabilityRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed class SharedProviderImageExecutionTargetResolver(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IProviderManifestCatalog providerManifestCatalog,
    SharedProviderPublicationEligibilityPolicy eligibilityPolicy,
    ISharedProviderRelaySupportCatalog relaySupportCatalog) :
    ISharedProviderImageExecutionTargetResolver
{
    public async Task<SharedProviderImageExecutionTarget?> ResolveAsync(
        SharedProviderImageCapabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProviderProfileId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Model) ||
            request.Count is < 1 or > SharedProviderRelaySupportDescriptor.MaximumAllowedImageCount)
        {
            return null;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await (
            from publication in dbContext.Set<ProviderSharePublication>().AsNoTracking()
            join profile in dbContext.Set<ProviderProfile>().AsNoTracking()
                on publication.ProviderProfileId equals profile.Id
            join secret in dbContext.Set<SecretRecord>().AsNoTracking()
                on profile.ApiKeySecretId equals (Guid?)secret.Id into matchedSecrets
            from secret in matchedSecrets.DefaultIfEmpty()
            where publication.IsPublished &&
                publication.PublicId == request.PublicationId &&
                publication.ProviderProfileId == request.ProviderProfileId
            select new PersistedImageProfile(profile, secret != null))
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var eligibility = eligibilityPolicy.Evaluate(
            row.Profile,
            providerManifestCatalog.ResolveManifest(
                row.Profile.ConnectorPluginKey,
                row.Profile.ProviderKind),
            row.RequiredSecretExists);
        if (!eligibility.IsEligible ||
            eligibility.Purpose != SharedProviderPurpose.ImageGeneration ||
            !relaySupportCatalog.TryGet(
                row.Profile.ConnectorPluginKey,
                SharedProviderPurpose.ImageGeneration,
                out var relayDescriptor) ||
            relayDescriptor.Classification != SharedProviderRelayAdapterClassification.Production ||
            !relayDescriptor.Support.Operations.Contains(SharedProviderRelayOperation.ImageGenerations) ||
            !relayDescriptor.Support.SupportsBase64Images ||
            request.Count > relayDescriptor.Support.MaximumImageCount ||
            !eligibility.Models.Any(model =>
                string.Equals(model.UpstreamModelId, request.Model, StringComparison.Ordinal) &&
                model.Capabilities.Contains(SharedProviderCapability.ImageGenerations) &&
                model.Capabilities.Contains(SharedProviderCapability.Base64Json)))
        {
            return null;
        }

        return new SharedProviderImageExecutionTarget(row.Profile);
    }

    private sealed record PersistedImageProfile(
        ProviderProfile Profile,
        bool RequiredSecretExists);
}
