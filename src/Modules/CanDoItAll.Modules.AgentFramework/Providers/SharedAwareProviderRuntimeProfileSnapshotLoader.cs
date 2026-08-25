using System.Security.Cryptography;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Workspace;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

using PersistedProviderProfile =
    CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile;
using ProviderProfileWellKnownIds =
    CanDoItAll.AgentFramework.Models.ProviderProfileWellKnownIds;

internal sealed class DatabaseProviderRuntimeProfileSnapshotLoader(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ProviderProfileMapper providerMapper,
    SharedProviderProfileMapper sharedProviderMapper,
    SharedProviderRuntimeProfileMaterializer sharedProviderMaterializer) :
    IProviderRuntimeProfileSnapshotLoader
{
    public async Task<IReadOnlyList<CanonicalProviderRuntimeProfile>>
        LoadAllAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var providers = await dbContext.Set<PersistedProviderProfile>()
            .AsNoTracking()
            .Where(item =>
                item.Id != ProviderProfileWellKnownIds.RuntimeFallbackOllama)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
        var imports = await dbContext.Set<SharedProviderImport>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var sources = await dbContext.Set<SharedProviderSource>()
            .AsNoTracking()
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var importsByProfile = imports
            .GroupBy(item => item.ProviderProfileId)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single());
        var projected = new List<CanonicalProviderRuntimeProfile>();
        foreach (var provider in providers)
        {
            var mapped = Map(provider, importsByProfile, sources);
            if (mapped is not null)
            {
                projected.Add(mapped);
            }
        }

        projected.Add(new CanonicalProviderRuntimeProfile(
            providerMapper.CreateRuntimeFallback(),
            ConfigurationRevision: null));
        return projected
            .OrderBy(
                item => item.Profile.Name,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<CanonicalProviderRuntimeProfile?> LoadAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        if (providerId ==
            ProviderProfileWellKnownIds.RuntimeFallbackOllama)
        {
            return new CanonicalProviderRuntimeProfile(
                providerMapper.CreateRuntimeFallback(),
                ConfigurationRevision: null);
        }

        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var provider = await dbContext.Set<PersistedProviderProfile>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == providerId,
                cancellationToken);
        if (provider is null)
        {
            return null;
        }

        if (!SharedProviderProfileOwnershipPolicy.IsSourceManagedConnector(
                provider.ConnectorPluginKey))
        {
            return MapPersonal(provider);
        }

        var imports = await dbContext.Set<SharedProviderImport>()
            .AsNoTracking()
            .Where(item => item.ProviderProfileId == provider.Id)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (imports.Count != 1)
        {
            return null;
        }

        var source = await dbContext.Set<SharedProviderSource>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == imports[0].SourceId,
                cancellationToken);
        return MapShared(provider, imports[0], source);
    }

    public async Task<ProviderConfigurationRevision?> LoadRevisionAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        var provider = await LoadAsync(providerId, cancellationToken);
        return provider?.ConfigurationRevision;
    }

    public async Task<IReadOnlyDictionary<
        Guid,
        ProviderConfigurationRevision>> LoadRevisionsAsync(
        CancellationToken cancellationToken = default)
    {
        var providers = await LoadAllAsync(cancellationToken);
        return providers
            .Where(item => item.ConfigurationRevision.HasValue)
            .ToDictionary(
                item => item.Profile.Id,
                item => item.ConfigurationRevision.GetValueOrDefault());
    }

    private CanonicalProviderRuntimeProfile? Map(
        PersistedProviderProfile provider,
        IReadOnlyDictionary<Guid, SharedProviderImport> importsByProfile,
        IReadOnlyDictionary<Guid, SharedProviderSource> sources)
    {
        if (!SharedProviderProfileOwnershipPolicy.IsSourceManagedConnector(
                provider.ConnectorPluginKey))
        {
            return MapPersonal(provider);
        }

        if (!importsByProfile.TryGetValue(provider.Id, out var import) ||
            !sources.TryGetValue(import.SourceId, out var source))
        {
            return null;
        }

        return MapShared(provider, import, source);
    }

    private CanonicalProviderRuntimeProfile MapPersonal(
        PersistedProviderProfile provider)
    {
        return new CanonicalProviderRuntimeProfile(
            providerMapper.Map(provider),
            new ProviderConfigurationRevision(provider.ConcurrencyToken));
    }

    private CanonicalProviderRuntimeProfile? MapShared(
        PersistedProviderProfile provider,
        SharedProviderImport import,
        SharedProviderSource? source)
    {
        var materialization = sharedProviderMaterializer.Materialize(
            provider,
            import,
            source);
        return materialization.Profile is null || source is null
            ? null
            : new CanonicalProviderRuntimeProfile(
                sharedProviderMapper.Map(materialization),
                CreateCompositeRevision(provider, import, source));
    }

    private static ProviderConfigurationRevision CreateCompositeRevision(
        PersistedProviderProfile provider,
        SharedProviderImport import,
        SharedProviderSource source)
    {
        Span<byte> material = stackalloc byte[96];
        WriteGuid(material, 0, provider.Id);
        WriteGuid(material, 16, provider.ConcurrencyToken);
        WriteGuid(material, 32, import.Id);
        WriteGuid(material, 48, import.ConcurrencyToken);
        WriteGuid(material, 64, source.Id);
        WriteGuid(material, 80, source.ConcurrencyToken);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(material, hash);
        return new ProviderConfigurationRevision(new Guid(hash[..16]));
    }

    private static void WriteGuid(
        Span<byte> destination,
        int offset,
        Guid value)
    {
        if (!value.TryWriteBytes(destination[offset..(offset + 16)]))
        {
            throw new InvalidOperationException(
                "The provider revision identifier could not be serialized.");
        }
    }
}
