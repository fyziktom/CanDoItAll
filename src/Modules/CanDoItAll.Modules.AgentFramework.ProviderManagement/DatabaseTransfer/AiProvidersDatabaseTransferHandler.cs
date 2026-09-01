using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class AiProvidersDatabaseTransferHandler(
    IEnumerable<IProviderDatabaseTransferGuard>? transferGuards = null) :
    IDatabaseTransferHandler
{
    private readonly IReadOnlyList<IProviderDatabaseTransferGuard> guards =
        transferGuards?.ToArray() ?? [];

    public DatabaseTransferItemDescriptor Descriptor { get; } = new(
        "ai-providers",
        "AI providers",
        "Copies AI provider profiles and referenced encrypted secrets.",
        SortOrder: 20,
        IsSensitive: true);

    public async Task<DatabaseTransferItemPreview> PreviewAsync(
        DatabaseTransferContext context,
        CancellationToken cancellationToken = default)
    {
        var sourceProviders = await context.SourceDbContext.Set<ProviderProfile>()
            .CountAsync(cancellationToken);
        var sourceSecretIds = await context.SourceDbContext.Set<ProviderProfile>()
            .Where(profile => profile.ApiKeySecretId.HasValue)
            .Select(profile => profile.ApiKeySecretId!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var targetProviders = await context.TargetDbContext.Set<ProviderProfile>()
            .CountAsync(cancellationToken);
        var blockReason = await FindTransferBlockReasonAsync(
            context,
            sourceSecretIds,
            cancellationToken);

        return new DatabaseTransferItemPreview(
            Descriptor,
            sourceProviders > 0 && blockReason is null,
            $"{sourceProviders} provider profile(s) and {sourceSecretIds.Length} referenced secret(s) are available.",
            sourceProviders == 0
                ? "The source database does not contain AI provider profiles."
                : blockReason,
            sourceProviders + sourceSecretIds.Length,
            targetProviders);
    }

    public async Task<DatabaseTransferItemResult> TransferAsync(
        DatabaseTransferContext context,
        CancellationToken cancellationToken = default)
    {
        var sourceProviders = await context.SourceDbContext.Set<ProviderProfile>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (sourceProviders.Count == 0)
        {
            return new DatabaseTransferItemResult(Descriptor.Key, Descriptor.Label, false, "The source database has no AI provider profiles to transfer.", 0);
        }

        var secretIds = sourceProviders
            .Where(provider => provider.ApiKeySecretId.HasValue)
            .Select(provider => provider.ApiKeySecretId!.Value)
            .Distinct()
            .ToList();
        var blockReason = await FindTransferBlockReasonAsync(
            context,
            secretIds,
            cancellationToken);
        if (blockReason is not null)
        {
            throw new InvalidOperationException(blockReason);
        }

        List<SecretRecord> sourceSecrets = secretIds.Count == 0
            ? []
            : await context.SourceDbContext.Set<SecretRecord>()
                .AsNoTracking()
                .Where(secret => secretIds.Contains(secret.Id))
                .ToListAsync(cancellationToken);

        var targetProviders = await context.TargetDbContext.Set<ProviderProfile>()
            .ToListAsync(cancellationToken);
        context.TargetDbContext.RemoveRange(targetProviders);
        await context.TargetDbContext.SaveChangesAsync(cancellationToken);

        if (secretIds.Count > 0)
        {
            var targetSecrets = await context.TargetDbContext.Set<SecretRecord>()
                .Where(secret => secretIds.Contains(secret.Id))
                .ToListAsync(cancellationToken);
            context.TargetDbContext.RemoveRange(targetSecrets);
            await context.TargetDbContext.SaveChangesAsync(cancellationToken);
            await context.TargetDbContext.Set<SecretRecord>().AddRangeAsync(sourceSecrets, cancellationToken);
        }

        await context.TargetDbContext.Set<ProviderProfile>().AddRangeAsync(sourceProviders, cancellationToken);
        await context.TargetDbContext.SaveChangesAsync(cancellationToken);

        return new DatabaseTransferItemResult(
            Descriptor.Key,
            Descriptor.Label,
            true,
            $"Copied {sourceProviders.Count} AI provider profile(s) and {sourceSecrets.Count} referenced encrypted secret(s).",
            sourceProviders.Count + sourceSecrets.Count);
    }

    private async Task<string?> FindTransferBlockReasonAsync(
        DatabaseTransferContext context,
        IReadOnlyCollection<Guid> sourceSecretIds,
        CancellationToken cancellationToken)
    {
        foreach (var guard in guards)
        {
            var reason = await guard.FindBlockReasonAsync(
                context,
                sourceSecretIds,
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(reason))
            {
                return reason;
            }
        }

        return null;
    }
}
