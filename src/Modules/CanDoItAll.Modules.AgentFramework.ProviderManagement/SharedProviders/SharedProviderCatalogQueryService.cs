using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public interface ISharedProviderCatalogQueryService {
    Task<SharedProviderCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}

public interface ISharedProviderRoutingResolver {
    Task<SharedProviderRoutingTarget?> ResolveAsync(
        SharedProviderRoutingModelId routingModelId,
        CancellationToken cancellationToken = default);
}

public sealed class SharedProviderCatalogQueryService(
    IDbContextFactory<ProvidersDbContext> dbContextFactory,
    SecretReferenceQuery secretReferences,
    SharedProviderServiceIdentityStore serviceIdentityStore,
    IProviderManifestCatalog providerManifestCatalog,
    SharedProviderPublicationEligibilityPolicy eligibilityPolicy,
    SharedProviderCatalogCache cache)
    : ISharedProviderCatalogQueryService, ISharedProviderRoutingResolver {
    public async Task<SharedProviderCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        => (await GetProjectionAsync(cancellationToken)).ToSnapshot();

    public async Task<SharedProviderRoutingTarget?> ResolveAsync(
        SharedProviderRoutingModelId routingModelId,
        CancellationToken cancellationToken = default) {
        if (!SharedProviderRoutingModelIdCodec.TryParse(routingModelId.Value, out _, out _)) {
            return null;
        }

        var projection = await GetProjectionAsync(cancellationToken);
        return projection.RoutingIndex.GetValueOrDefault(routingModelId);
    }

    private async Task<SharedProviderCatalogProjection> GetProjectionAsync(CancellationToken cancellationToken) {
        var sourceInstanceId = await serviceIdentityStore.GetOrCreateAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query =
            from publication in dbContext.Set<ProviderSharePublication>().AsNoTracking()
            join profile in dbContext.Set<ProviderProfile>().AsNoTracking()
                on publication.ProviderProfileId equals profile.Id
            where publication.IsPublished
            select new { Publication = publication, Profile = profile };
        var versions = await query.Select(row => new CatalogVersion(
            row.Publication.Id,
            row.Publication.PublicId,
            row.Publication.ConcurrencyToken,
            row.Profile.Id,
            row.Profile.ConcurrencyToken,
            row.Profile.ApiKeySecretId,
            false)).ToListAsync(cancellationToken);
        var existingSecretIds = await secretReferences.GetExistingIdsAsync(
            versions.Where(version => version.SecretRecordId.HasValue)
                .Select(version => version.SecretRecordId!.Value).Distinct().ToArray(), cancellationToken);
        var observedVersions = versions.Select(version => version with {
            RequiredSecretExists = version.SecretRecordId is { } secretId && existingSecretIds.Contains(secretId)
        });
        if (cache.TryGet(CreatePersistedStamp(sourceInstanceId, observedVersions), out var cachedProjection)) {
            return cachedProjection;
        }

        var rows = await query.ToListAsync(cancellationToken);
        var loadedSecretIds = await secretReferences.GetExistingIdsAsync(
            rows.Where(row => row.Profile.ApiKeySecretId.HasValue)
                .Select(row => row.Profile.ApiKeySecretId!.Value).Distinct().ToArray(), cancellationToken);
        var sources = rows.Select(row => new SharedProviderCatalogProjectionSource(
            row.Publication,
            row.Profile,
            eligibilityPolicy.Evaluate(
                row.Profile,
                providerManifestCatalog.ResolveManifest(row.Profile.ConnectorPluginKey, row.Profile.ProviderKind),
                row.Profile.ApiKeySecretId is { } secretId && loadedSecretIds.Contains(secretId)))).ToArray();
        var projection = SharedProviderCatalogProjector.Project(sourceInstanceId, sources);
        var loadedVersions = rows.Select(row => new CatalogVersion(
            row.Publication.Id,
            row.Publication.PublicId,
            row.Publication.ConcurrencyToken,
            row.Profile.Id,
            row.Profile.ConcurrencyToken,
            row.Profile.ApiKeySecretId,
            row.Profile.ApiKeySecretId is { } secretId && loadedSecretIds.Contains(secretId)));
        cache.Set(CreatePersistedStamp(sourceInstanceId, loadedVersions), projection);
        return projection;
    }

    private static string CreatePersistedStamp(
        SharedProviderSourceInstanceId sourceInstanceId,
        IEnumerable<CatalogVersion> versions) {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer)) {
            writer.WriteStartObject();
            writer.WriteString("sourceInstanceId", sourceInstanceId.ToString());
            writer.WriteStartArray("publications");
            foreach (var version in versions.OrderBy(item => item.PublicId.Value)) {
                writer.WriteStartObject();
                writer.WriteString("publicationId", version.PublicationId);
                writer.WriteString("publicId", version.PublicId.ToString());
                writer.WriteString("publicationToken", version.PublicationToken);
                writer.WriteString("profileId", version.ProfileId);
                writer.WriteString("profileToken", version.ProfileToken);
                writer.WriteBoolean("requiredSecretExists", version.RequiredSecretExists);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan));
    }

    private sealed record CatalogVersion(
        Guid PublicationId,
        SharedProviderPublicationId PublicId,
        Guid PublicationToken,
        Guid ProfileId,
        Guid ProfileToken,
        Guid? SecretRecordId,
        bool RequiredSecretExists);
}
