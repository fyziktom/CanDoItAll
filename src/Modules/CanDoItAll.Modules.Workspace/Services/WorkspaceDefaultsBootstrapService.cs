using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workspace;

public sealed class WorkspaceDefaultsBootstrapService {
    public async Task<bool> EnsureAsync(
        ResolvedDatabaseProfile profile,
        CoordinatedDatabaseTransaction transactions,
        Guid newWorkspaceDefaultProviderId,
        Guid matchedProviderId,
        Func<Guid, CancellationToken, Task<bool>> providerExists,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(providerExists);
        var options = new DbContextOptionsBuilder<WorkspaceSettingsDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, profile);
        await using var dbContext = await transactions.CreateEnlistedAsync(options.Options,
            static value => new WorkspaceSettingsDbContext(value), cancellationToken);
        var settings = await dbContext.Set<WorkspaceSettings>().FirstOrDefaultAsync(cancellationToken);
        if (settings is null) {
            dbContext.Set<WorkspaceSettings>().Add(new WorkspaceSettings {
                DefaultProviderProfileId = newWorkspaceDefaultProviderId,
                WorkspaceName = "CanDoItAll",
                DefaultPromptOutputFormat = "Markdown",
                Notes = "Runtime bootstrap default provider.",
                UpdatedAtUtc = timestamp
            });
        } else if (settings.DefaultProviderProfileId != matchedProviderId &&
            (!settings.DefaultProviderProfileId.HasValue ||
                !await providerExists(settings.DefaultProviderProfileId.Value, cancellationToken))) {
            settings.DefaultProviderProfileId = matchedProviderId;
            settings.UpdatedAtUtc = timestamp;
        } else {
            return false;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
