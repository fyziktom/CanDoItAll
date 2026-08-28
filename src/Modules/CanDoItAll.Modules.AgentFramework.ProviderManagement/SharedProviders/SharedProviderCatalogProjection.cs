using System.Collections.Frozen;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed record SharedProviderCatalogProjectionSource(
    ProviderSharePublication Publication,
    ProviderProfile Profile,
    SharedProviderPublicationEligibility Eligibility);

public sealed record SharedProviderRoutingTarget(
    SharedProviderPublicationId PublicationId,
    Guid ProviderProfileId,
    string UpstreamModelId,
    SharedProviderPurpose Purpose,
    IReadOnlyList<SharedProviderCapability> Capabilities);

public sealed record SharedProviderCatalogSnapshot
{
    public SharedProviderCatalogSnapshot(
        SharedProviderCatalogDocument catalog,
        SharedProviderCatalogEntityTag entityTag)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        SharedProviderProtocolJson.ValidateCatalog(catalog);
        if (entityTag != SharedProviderCatalogEntityTag.FromRevision(catalog.CatalogRevision))
        {
            throw new ArgumentException(
                "The catalog entity tag must match its public revision.",
                nameof(entityTag));
        }

        Catalog = catalog;
        EntityTag = entityTag;
    }

    public SharedProviderCatalogDocument Catalog { get; }

    public SharedProviderCatalogEntityTag EntityTag { get; }
}

public sealed class SharedProviderCatalogProjection
{
    public SharedProviderCatalogProjection(
        SharedProviderCatalogDocument catalog,
        SharedProviderCatalogEntityTag entityTag,
        IReadOnlyDictionary<SharedProviderRoutingModelId, SharedProviderRoutingTarget> routingIndex)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(routingIndex);
        SharedProviderProtocolJson.ValidateCatalog(catalog);
        if (entityTag != SharedProviderCatalogEntityTag.FromRevision(catalog.CatalogRevision))
        {
            throw new ArgumentException(
                "The catalog entity tag must match its public revision.",
                nameof(entityTag));
        }

        Catalog = catalog;
        EntityTag = entityTag;
        RoutingIndex = routingIndex.ToFrozenDictionary();
    }

    public SharedProviderCatalogDocument Catalog { get; }

    public SharedProviderPublicRevision CatalogRevision => Catalog.CatalogRevision;

    public SharedProviderCatalogEntityTag EntityTag { get; }

    public IReadOnlyDictionary<SharedProviderRoutingModelId, SharedProviderRoutingTarget> RoutingIndex { get; }

    public SharedProviderCatalogSnapshot ToSnapshot() => new(Catalog, EntityTag);
}

public static class SharedProviderPublicHealthMapper
{
    public const string HealthyStatus = "Healthy";

    public static SharedProviderHealthState Map(ProviderProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!profile.LastHealthCheckAtUtc.HasValue)
        {
            return SharedProviderHealthState.Degraded;
        }

        return string.Equals(profile.LastHealthStatus, HealthyStatus, StringComparison.Ordinal)
            ? SharedProviderHealthState.Available
            : SharedProviderHealthState.Unavailable;
    }
}

public static class SharedProviderCatalogProjector
{
    private static readonly SharedProviderPublicRevision PlaceholderRevision =
        new($"{SharedProviderPublicRevision.Prefix}{new string('0', SharedProviderPublicRevision.HashLength)}");

    public static SharedProviderCatalogProjection Project(
        SharedProviderSourceInstanceId sourceInstanceId,
        IEnumerable<SharedProviderCatalogProjectionSource> sources)
    {
        if (sourceInstanceId.Value == Guid.Empty)
        {
            throw new ArgumentException("The source instance id cannot be empty.", nameof(sourceInstanceId));
        }

        ArgumentNullException.ThrowIfNull(sources);
        var publications = new List<SharedProviderCatalogPublication>();
        var routes = new Dictionary<SharedProviderRoutingModelId, SharedProviderRoutingTarget>();
        foreach (var source in sources
            .Where(item => item.Publication.IsPublished && item.Eligibility.IsEligible)
            .OrderBy(item => item.Publication.PublicId.Value))
        {
            var publication = ProjectPublication(source, routes);
            publications.Add(publication with
            {
                Revision = SharedProviderCanonicalRevision.ComputePublication(publication)
            });
        }

        var catalog = new SharedProviderCatalogDocument(
            SharedProviderProtocolVersion.Current,
            sourceInstanceId,
            PlaceholderRevision,
            new SharedProviderProtocolDescriptor(SharedProviderRoutes.OpenAiBase),
            Array.AsReadOnly(publications.ToArray()));
        catalog = catalog with
        {
            CatalogRevision = SharedProviderCanonicalRevision.ComputeCatalog(catalog)
        };
        var normalized = SharedProviderProtocolJson.DeserializeCatalog(
            SharedProviderProtocolJson.SerializeCatalog(catalog));
        return new SharedProviderCatalogProjection(
            normalized,
            SharedProviderCatalogEntityTag.FromRevision(normalized.CatalogRevision),
            routes);
    }

    private static SharedProviderCatalogPublication ProjectPublication(
        SharedProviderCatalogProjectionSource source,
        IDictionary<SharedProviderRoutingModelId, SharedProviderRoutingTarget> routes)
    {
        var profile = source.Profile;
        var eligibility = source.Eligibility;
        var purpose = eligibility.Purpose ??
            throw new InvalidOperationException("An eligible publication must define its purpose.");
        var transport = eligibility.Transport ??
            throw new InvalidOperationException("An eligible publication must define its transport.");
        var publicationId = source.Publication.PublicId;
        if (!SharedProviderProfilePublicationMetadataReader.TryRead(profile, out var metadata, out var failure)) {
            throw new InvalidOperationException(failure);
        }
        var pricing = ProviderPricingMetadata.Read(profile.ExtraSettingsJson);
        var thinkingProvider = SharedProviderThinkingCapabilityMapper.CreateSourceProvider(profile);
        var prices = ProviderPricingDefaults.NormalizeModelPrices(
                metadata.ProviderKind, profile.DefaultModel, pricing.ModelPrices)
            .ToDictionary(price => price.Model, StringComparer.OrdinalIgnoreCase);
        var models = eligibility.Models
            .Select(model =>
            {
                var routingModelId = SharedProviderRoutingModelIdCodec.Create(
                    publicationId,
                    model.UpstreamModelId);
                var publicModel = new SharedProviderCatalogModel(
                    routingModelId,
                    model.UpstreamModelId,
                    Array.AsReadOnly(model.Capabilities.ToArray())) {
                    Thinking = SharedProviderThinkingCapabilityMapper.ToCatalog(thinkingProvider, model.UpstreamModelId),
                    IsSuggested = metadata.ProviderKind != CanDoItAll.AgentFramework.Models.ProviderKind.OpenAi ||
                        metadata.Purpose != ProviderProfilePurpose.Chat ||
                        OpenAiModelSuggestions.IsMainModel(model.UpstreamModelId),
                    Price = prices.TryGetValue(model.UpstreamModelId, out var price)
                        ? SharedProviderPriceMapper.ToCatalog(price)
                        : null
                };
                if (!routes.TryAdd(
                        routingModelId,
                        new SharedProviderRoutingTarget(
                            publicationId,
                            profile.Id,
                            model.UpstreamModelId,
                            purpose,
                            Array.AsReadOnly(model.Capabilities.ToArray()))))
                {
                    throw new InvalidOperationException(
                        $"Routing model id '{routingModelId.Value}' is duplicated in the catalog projection.");
                }

                return publicModel;
            })
            .ToArray();
        var defaultModelId = SharedProviderRoutingModelIdCodec.Create(
            publicationId,
            eligibility.Models[0].UpstreamModelId);
        return new SharedProviderCatalogPublication(
            publicationId,
            PlaceholderRevision,
            profile.Name,
            purpose,
            transport,
            defaultModelId,
            Array.AsReadOnly(models),
            new SharedProviderCatalogHealth(SharedProviderPublicHealthMapper.Map(profile))) {
            IsPrivateProvider = ProviderPricingDefaults.ResolveIsPrivateProvider(
                metadata.ProviderKind, pricing.IsPrivateProvider)
        };
    }
}
