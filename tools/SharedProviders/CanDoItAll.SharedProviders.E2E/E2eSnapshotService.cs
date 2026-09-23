using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.SharedProviders.E2E;

internal sealed class E2eSnapshotService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock)
{
    public async Task<E2eStateSnapshot> CaptureAsync(
        E2eRole role,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var providers = await dbContext.Set<ProviderProfile>()
            .AsNoTracking()
            .OrderBy(provider => provider.Name)
            .ThenBy(provider => provider.Id)
            .ToArrayAsync(cancellationToken);
        var publications = await dbContext.Set<ProviderSharePublication>()
            .AsNoTracking()
            .OrderBy(publication => publication.ProviderProfileId)
            .ToArrayAsync(cancellationToken);
        var sources = await dbContext.Set<SharedProviderSource>()
            .AsNoTracking()
            .OrderBy(source => source.Name)
            .ThenBy(source => source.Id)
            .ToArrayAsync(cancellationToken);
        var imports = await dbContext.Set<SharedProviderImport>()
            .AsNoTracking()
            .OrderBy(imported => imported.RemoteDisplayName)
            .ThenBy(imported => imported.Id)
            .ToArrayAsync(cancellationToken);
        var serviceIdentityEntity = await dbContext.Set<SharedProviderServiceIdentity>()
            .AsNoTracking()
            .Where(identity => identity.Id == SharedProviderServiceIdentity.SingletonId)
            .SingleOrDefaultAsync(cancellationToken);
        var serviceIdentity = serviceIdentityEntity?.PublicId.Value;

        var providerStates = providers
            .Select(provider => new E2eProviderState(
                provider.Id,
                E2eFixtures.ResolveFixtureId(provider.Name),
                provider.Name,
                provider.ProviderKind?.ToString(),
                provider.ConnectorPluginKey,
                provider.DefaultModel,
                provider.IsEnabled,
                provider.SupportsStreaming,
                provider.SupportsToolCalling,
                provider.SupportsStructuredOutput,
                provider.SupportsVision))
            .ToArray();
        var publicationStates = publications
            .Select(publication => new E2ePublicationState(
                publication.Id,
                publication.ProviderProfileId,
                publication.PublicId.Value,
                publication.IsPublished,
                publication.UpdatedAtUtc))
            .ToArray();
        var fixtures = providerStates
            .Where(provider => provider.FixtureId is not null)
            .Select(provider =>
            {
                var publication = publicationStates.SingleOrDefault(item =>
                    item.ProviderProfileId == provider.Id);
                return new E2eFixtureIdentity(
                    provider.FixtureId!,
                    provider.Id,
                    publication?.PublicId,
                    publication?.IsPublished);
            })
            .OrderBy(fixture => fixture.FixtureId, StringComparer.Ordinal)
            .ToArray();
        var sourceStates = sources
            .Select(source => new E2eSourceState(
                source.Id,
                source.Name,
                source.BaseUri,
                source.IsEnabled,
                source.AllowInsecurePrivateNetwork,
                source.Status.ToString(),
                source.RemoteInstanceId?.Value,
                source.LastCatalogETag?.Value,
                source.LastSyncAtUtc,
                source.LastStatusCode,
                source.CreatedAtUtc,
                source.UpdatedAtUtc))
            .ToArray();
        var importStates = imports
            .Select(imported => new E2eImportState(
                imported.Id,
                imported.SourceId,
                imported.RemotePublicationId.Value,
                imported.ProviderProfileId,
                imported.RemoteDisplayName,
                imported.RemoteRevision.Value,
                imported.RemotePurpose.ToString(),
                imported.RemoteTransport.ToString(),
                imported.RemoteDefaultModelId.Value,
                imported.SelectionState.ToString(),
                imported.AvailabilityState.ToString(),
                imported.LastSeenAtUtc,
                imported.LastSyncAtUtc,
                imported.CreatedAtUtc,
                imported.UpdatedAtUtc))
            .ToArray();

        return new E2eStateSnapshot(
            SchemaVersion: 1,
            role,
            clock.GetUtcNow(),
            serviceIdentity,
            fixtures,
            providerStates,
            publicationStates,
            sourceStates,
            importStates);
    }
}

internal sealed record E2eStateSnapshot(
    int SchemaVersion,
    E2eRole Role,
    DateTimeOffset CapturedAtUtc,
    Guid? ServiceInstanceId,
    IReadOnlyList<E2eFixtureIdentity> Fixtures,
    IReadOnlyList<E2eProviderState> Providers,
    IReadOnlyList<E2ePublicationState> Publications,
    IReadOnlyList<E2eSourceState> Sources,
    IReadOnlyList<E2eImportState> Imports);

internal sealed record E2eFixtureIdentity(
    string FixtureId,
    Guid ProviderProfileId,
    Guid? PublicationId,
    bool? IsPublished);

internal sealed record E2eProviderState(
    Guid Id,
    string? FixtureId,
    string Name,
    string? ProviderKind,
    string ConnectorPluginKey,
    string DefaultModel,
    bool IsEnabled,
    bool SupportsStreaming,
    bool SupportsToolCalling,
    bool SupportsStructuredOutput,
    bool SupportsVision);

internal sealed record E2ePublicationState(
    Guid Id,
    Guid ProviderProfileId,
    Guid PublicId,
    bool IsPublished,
    DateTimeOffset UpdatedAtUtc);

internal sealed record E2eSourceState(
    Guid Id,
    string Name,
    string BaseUri,
    bool IsEnabled,
    bool AllowInsecurePrivateNetwork,
    string Status,
    Guid? RemoteInstanceId,
    string? LastCatalogETag,
    DateTimeOffset? LastSyncAtUtc,
    int? LastStatusCode,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

internal sealed record E2eImportState(
    Guid Id,
    Guid SourceId,
    Guid RemotePublicationId,
    Guid ProviderProfileId,
    string RemoteDisplayName,
    string RemoteRevision,
    string RemotePurpose,
    string RemoteTransport,
    string RemoteDefaultModelId,
    string SelectionState,
    string AvailabilityState,
    DateTimeOffset? LastSeenAtUtc,
    DateTimeOffset? LastSyncAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
