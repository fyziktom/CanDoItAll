using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.Workspace;

public static class SharedProviderPublicationTransitions
{
    public static ProviderSharePublication Create(
        Guid providerProfileId,
        SharedProviderPublicationId publicId,
        DateTimeOffset timestampUtc)
    {
        SharedProviderStateGuard.NonEmpty(providerProfileId, nameof(providerProfileId));
        SharedProviderStateGuard.PublicationId(publicId, nameof(publicId));
        SharedProviderStateGuard.Utc(timestampUtc, nameof(timestampUtc));
        if (providerProfileId == publicId.Value)
        {
            throw new ArgumentException(
                "A publication public id must differ from its internal provider profile id.",
                nameof(publicId));
        }

        return new ProviderSharePublication
        {
            ProviderProfileId = providerProfileId,
            PublicId = publicId,
            IsPublished = false,
            CreatedAtUtc = timestampUtc,
            UpdatedAtUtc = timestampUtc
        };
    }

    public static void Publish(
        ProviderSharePublication publication,
        DateTimeOffset timestampUtc)
    {
        Validate(publication);
        SharedProviderStateGuard.TransitionTimestamp(
            timestampUtc,
            publication.UpdatedAtUtc,
            nameof(timestampUtc));
        if (publication.IsPublished)
        {
            return;
        }

        publication.IsPublished = true;
        publication.UpdatedAtUtc = timestampUtc;
    }

    public static void Unpublish(
        ProviderSharePublication publication,
        DateTimeOffset timestampUtc)
    {
        Validate(publication);
        SharedProviderStateGuard.TransitionTimestamp(
            timestampUtc,
            publication.UpdatedAtUtc,
            nameof(timestampUtc));
        if (!publication.IsPublished)
        {
            return;
        }

        publication.IsPublished = false;
        publication.UpdatedAtUtc = timestampUtc;
    }

    private static void Validate(ProviderSharePublication publication)
    {
        ArgumentNullException.ThrowIfNull(publication);
        SharedProviderStateGuard.NonEmpty(publication.Id, nameof(publication));
        SharedProviderStateGuard.NonEmpty(publication.ProviderProfileId, nameof(publication));
        SharedProviderStateGuard.PublicationId(publication.PublicId, nameof(publication));
        SharedProviderStateGuard.Utc(publication.CreatedAtUtc, nameof(publication));
        SharedProviderStateGuard.Utc(publication.UpdatedAtUtc, nameof(publication));
        if (publication.ProviderProfileId == publication.PublicId.Value)
        {
            throw new ArgumentException(
                "A publication public id must differ from its internal provider profile id.",
                nameof(publication));
        }
    }
}
