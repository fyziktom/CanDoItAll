using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workspace;

public sealed class SharedProviderDatabaseTransferGuard :
    IProviderDatabaseTransferGuard
{
    public async Task<string?> FindBlockReasonAsync(
        DatabaseTransferContext context,
        IReadOnlyCollection<Guid> transferredSecretIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var causes = new List<string>();
        if (await HasReferencedProviderProfilesAsync(
                context.SourceDbContext,
                cancellationToken))
        {
            causes.Add("the source contains provider profiles referenced by shared-provider publications or imports");
        }

        if (await HasReferencedProviderProfilesAsync(
                context.TargetDbContext,
                cancellationToken))
        {
            causes.Add("the target contains provider profiles referenced by shared-provider publications or imports");
        }

        if (transferredSecretIds.Count > 0 &&
            await context.TargetDbContext.Set<SharedProviderSource>()
                .AsNoTracking()
                .AnyAsync(
                    source => transferredSecretIds.Contains(source.ApiTokenSecretId),
                    cancellationToken))
        {
            causes.Add("a target shared-provider source uses a secret that the transfer would replace");
        }

        return causes.Count == 0
            ? null
            : $"AI provider transfer is blocked because {string.Join("; ", causes)}. Transfer shared-provider state through its owning workflow first.";
    }

    private static async Task<bool> HasReferencedProviderProfilesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
        => await dbContext.Set<ProviderSharePublication>()
                .AsNoTracking()
                .AnyAsync(cancellationToken) ||
            await dbContext.Set<SharedProviderImport>()
                .AsNoTracking()
                .AnyAsync(cancellationToken);
}
