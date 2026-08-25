using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workspace;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

using PersistedProviderProfile =
    CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile;
using IProviderProfileCommitObserver =
    CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderProfileCommitObserver;
using IProviderRuntimeProfileSnapshotLoader =
    CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeProfileSnapshotLoader;
using ProviderCatalogProjectionException =
    CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderCatalogProjectionException;
using ProviderCatalogProjectionOperationKind =
    CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderCatalogProjectionOperationKind;
using SharedProviderProfileOwnershipPolicy =
    CanDoItAll.Modules.AgentFramework.ProviderManagement.SharedProviderProfileOwnershipPolicy;

internal sealed class SharedProviderCatalogProjectionCommitObserver(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IProviderRuntimeProfileSnapshotLoader runtimeProfileLoader,
    ISandboxWorkspaceCatalogStore catalogStore,
    ILogger<SharedProviderCatalogProjectionCommitObserver> logger) :
    IProviderProfileCommitObserver
{
    public async Task ProviderSavedAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsSharedProviderAsync(providerId, cancellationToken))
        {
            return;
        }

        try
        {
            var canonical = await runtimeProfileLoader.LoadAsync(
                providerId,
                cancellationToken);
            await catalogStore.UpdateCatalogAsync(catalog => catalog with
            {
                Providers = canonical is null
                    ? catalog.Providers
                        .Where(provider => provider.Id != providerId)
                        .ToList()
                    : catalog.Providers
                        .Where(provider => provider.Id != providerId)
                        .Append(canonical.Profile)
                        .OrderBy(provider => provider.Name,
                            StringComparer.OrdinalIgnoreCase)
                        .ToList()
            }, cancellationToken);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            var projectionException = new ProviderCatalogProjectionException(
                providerId,
                ProviderCatalogProjectionOperationKind.Upsert,
                "Retry shared-provider synchronization to repair the catalog projection.",
                exception);
            logger.LogError(
                projectionException,
                "Shared-provider catalog projection failed after the canonical database commit. ProviderId={ProviderId} RepairAction={RepairAction}",
                providerId,
                projectionException.RepairAction);
            throw projectionException;
        }
    }

    public async Task ProviderDeletedAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        var currentCatalog = await catalogStore.LoadCatalogAsync(
            cancellationToken);
        var currentProvider = currentCatalog.Providers
            .SingleOrDefault(provider => provider.Id == providerId);
        if (currentProvider is null ||
            !SharedProviderProfileOwnershipPolicy.IsSourceManagedConnector(
                currentProvider.ConnectorPluginKey))
        {
            return;
        }

        await catalogStore.UpdateCatalogAsync(catalog => catalog with
        {
            Providers = catalog.Providers
                .Where(provider => provider.Id != providerId)
                .ToList()
        }, cancellationToken);
    }

    private async Task<bool> IsSharedProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var connectorPluginKey = await dbContext
            .Set<PersistedProviderProfile>()
            .AsNoTracking()
            .Where(provider => provider.Id == providerId)
            .Select(provider => provider.ConnectorPluginKey)
            .SingleOrDefaultAsync(cancellationToken);
        return SharedProviderProfileOwnershipPolicy.IsSourceManagedConnector(
            connectorPluginKey);
    }
}
