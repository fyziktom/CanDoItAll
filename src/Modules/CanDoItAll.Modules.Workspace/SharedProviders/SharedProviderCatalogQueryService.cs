using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workspace;

public interface ISharedProviderCatalogQueryService
{
    Task<SharedProviderCatalogSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default);
}

public interface ISharedProviderRoutingResolver
{
    Task<SharedProviderRoutingTarget?> ResolveAsync(
        SharedProviderRoutingModelId routingModelId,
        CancellationToken cancellationToken = default);
}

public sealed class SharedProviderCatalogQueryService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    SharedProviderServiceIdentityStore serviceIdentityStore,
    IProviderManifestCatalog providerManifestCatalog,
    SharedProviderPublicationEligibilityPolicy eligibilityPolicy,
    SharedProviderCatalogCache cache)
    : ISharedProviderCatalogQueryService,
      ISharedProviderRoutingResolver
{
    public async Task<SharedProviderCatalogSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
        => (await GetProjectionAsync(cancellationToken)).ToSnapshot();

    public async Task<SharedProviderRoutingTarget?> ResolveAsync(
        SharedProviderRoutingModelId routingModelId,
        CancellationToken cancellationToken = default)
    {
        if (!SharedProviderRoutingModelIdCodec.TryParse(routingModelId.Value, out _, out _))
        {
            return null;
        }

        var projection = await GetProjectionAsync(cancellationToken);
        return projection.RoutingIndex.GetValueOrDefault(routingModelId);
    }

    private async Task<SharedProviderCatalogProjection> GetProjectionAsync(
        CancellationToken cancellationToken)
    {
        var sourceInstanceId = await serviceIdentityStore.GetOrCreateAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await (
            from publication in dbContext.Set<ProviderSharePublication>().AsNoTracking()
            join profile in dbContext.Set<ProviderProfile>().AsNoTracking()
                on publication.ProviderProfileId equals profile.Id
            join secret in dbContext.Set<SecretRecord>().AsNoTracking()
                on profile.ApiKeySecretId equals (Guid?)secret.Id into matchedSecrets
            from secret in matchedSecrets.DefaultIfEmpty()
            where publication.IsPublished
            select new CatalogRow(publication, profile, secret != null))
            .ToListAsync(cancellationToken);
        var sources = rows
            .Select(row => new SharedProviderCatalogProjectionSource(
                row.Publication,
                row.Profile,
                eligibilityPolicy.Evaluate(
                    row.Profile,
                    providerManifestCatalog.ResolveManifest(
                        row.Profile.ConnectorPluginKey,
                        row.Profile.ProviderKind),
                    row.RequiredSecretExists)))
            .ToArray();
        var persistedStamp = CreatePersistedStamp(sourceInstanceId, rows);
        if (cache.TryGet(persistedStamp, out var cachedProjection))
        {
            return cachedProjection;
        }

        var projection = SharedProviderCatalogProjector.Project(sourceInstanceId, sources);
        cache.Set(persistedStamp, projection);
        return projection;
    }

    private static string CreatePersistedStamp(
        SharedProviderSourceInstanceId sourceInstanceId,
        IEnumerable<CatalogRow> rows)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("sourceInstanceId", sourceInstanceId.ToString());
            writer.WriteStartArray("publications");
            foreach (var row in rows.OrderBy(item => item.Publication.PublicId.Value))
            {
                writer.WriteStartObject();
                writer.WriteString("publicationId", row.Publication.Id);
                writer.WriteString("publicId", row.Publication.PublicId.ToString());
                writer.WriteString("publicationToken", row.Publication.ConcurrencyToken);
                writer.WriteString("profileId", row.Profile.Id);
                writer.WriteString("profileToken", row.Profile.ConcurrencyToken);
                writer.WriteBoolean("requiredSecretExists", row.RequiredSecretExists);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan));
    }

    private sealed record CatalogRow(
        ProviderSharePublication Publication,
        ProviderProfile Profile,
        bool RequiredSecretExists);
}
