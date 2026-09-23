using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class SharedProviderProfileOwnershipQuery(IDbContextFactory<ProvidersDbContext> factory) {
    public async Task<bool> IsSourceManagedAsync(Guid providerId, CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var connector = await context.Set<ProviderProfile>().AsNoTracking()
            .Where(provider => provider.Id == providerId).Select(provider => provider.ConnectorPluginKey)
            .SingleOrDefaultAsync(cancellationToken);
        return SharedProviderProfileOwnershipPolicy.IsSourceManagedConnector(connector);
    }
}
