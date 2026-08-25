using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workspace;

public sealed class AiProvidersDatabaseTransferHandler : IDatabaseTransferHandler
{
    [Flags]
    private enum SharedProviderTransferBlockReason
    {
        None = 0,
        SourceContainsReferencedProfiles = 1,
        TargetContainsReferencedProfiles = 2,
        TargetSourceUsesTransferredSecret = 4
    }

    public DatabaseTransferItemDescriptor Descriptor { get; } = new(
        "ai-providers",
        "AI providers",
        "Copies AI provider profiles, referenced encrypted secrets, and the default provider selection.",
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
        var blockReason = await FindSharedProviderTransferBlockAsync(
            context,
            sourceSecretIds,
            cancellationToken);

        return new DatabaseTransferItemPreview(
            Descriptor,
            sourceProviders > 0 && blockReason == SharedProviderTransferBlockReason.None,
            $"{sourceProviders} provider profile(s) and {sourceSecretIds.Length} referenced secret(s) are available.",
            sourceProviders == 0
                ? "The source database does not contain AI provider profiles."
                : blockReason == SharedProviderTransferBlockReason.None
                    ? null
                    : DescribeTransferBlock(blockReason),
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
        var blockReason = await FindSharedProviderTransferBlockAsync(
            context,
            secretIds,
            cancellationToken);
        if (blockReason != SharedProviderTransferBlockReason.None)
        {
            throw new InvalidOperationException(DescribeTransferBlock(blockReason));
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
        await CopyDefaultProviderSelectionAsync(context, sourceProviders.Select(provider => provider.Id).ToHashSet(), cancellationToken);
        await context.TargetDbContext.SaveChangesAsync(cancellationToken);

        return new DatabaseTransferItemResult(
            Descriptor.Key,
            Descriptor.Label,
            true,
            $"Copied {sourceProviders.Count} AI provider profile(s) and {sourceSecrets.Count} referenced encrypted secret(s).",
            sourceProviders.Count + sourceSecrets.Count);
    }

    private static async Task CopyDefaultProviderSelectionAsync(
        DatabaseTransferContext context,
        IReadOnlySet<Guid> copiedProviderIds,
        CancellationToken cancellationToken)
    {
        var sourceSettings = await context.SourceDbContext.Set<WorkspaceSettings>()
            .AsNoTracking()
            .OrderByDescending(settings => settings.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (sourceSettings?.DefaultProviderProfileId is null ||
            !copiedProviderIds.Contains(sourceSettings.DefaultProviderProfileId.Value))
        {
            return;
        }

        var targetSettings = await context.TargetDbContext.Set<WorkspaceSettings>()
            .OrderByDescending(settings => settings.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (targetSettings is null)
        {
            targetSettings = new WorkspaceSettings
            {
                WorkspaceName = sourceSettings.WorkspaceName,
                DefaultPromptOutputFormat = sourceSettings.DefaultPromptOutputFormat,
                CurrencyCode = sourceSettings.CurrencyCode,
                CurrencyCultureName = sourceSettings.CurrencyCultureName,
                Notes = sourceSettings.Notes
            };
            await context.TargetDbContext.Set<WorkspaceSettings>().AddAsync(targetSettings, cancellationToken);
        }

        targetSettings.DefaultProviderProfileId = sourceSettings.DefaultProviderProfileId;
        targetSettings.CurrencyCode = sourceSettings.CurrencyCode;
        targetSettings.CurrencyCultureName = sourceSettings.CurrencyCultureName;
        targetSettings.UpdatedAtUtc = sourceSettings.UpdatedAtUtc;
    }

    private static async Task<SharedProviderTransferBlockReason> FindSharedProviderTransferBlockAsync(
        DatabaseTransferContext context,
        IReadOnlyCollection<Guid> sourceSecretIds,
        CancellationToken cancellationToken)
    {
        var reason = SharedProviderTransferBlockReason.None;
        if (await HasReferencedProviderProfilesAsync(context.SourceDbContext, cancellationToken))
        {
            reason |= SharedProviderTransferBlockReason.SourceContainsReferencedProfiles;
        }

        if (await HasReferencedProviderProfilesAsync(context.TargetDbContext, cancellationToken))
        {
            reason |= SharedProviderTransferBlockReason.TargetContainsReferencedProfiles;
        }

        if (sourceSecretIds.Count > 0 &&
            await context.TargetDbContext.Set<SharedProviderSource>()
                .AsNoTracking()
                .AnyAsync(
                    source => sourceSecretIds.Contains(source.ApiTokenSecretId),
                    cancellationToken))
        {
            reason |= SharedProviderTransferBlockReason.TargetSourceUsesTransferredSecret;
        }

        return reason;
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

    private static string DescribeTransferBlock(SharedProviderTransferBlockReason reason)
    {
        if (reason == SharedProviderTransferBlockReason.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reason),
                reason,
                "A transfer block description requires at least one reason.");
        }

        var causes = new List<string>();
        if (reason.HasFlag(SharedProviderTransferBlockReason.SourceContainsReferencedProfiles))
        {
            causes.Add("the source contains provider profiles referenced by shared-provider publications or imports");
        }

        if (reason.HasFlag(SharedProviderTransferBlockReason.TargetContainsReferencedProfiles))
        {
            causes.Add("the target contains provider profiles referenced by shared-provider publications or imports");
        }

        if (reason.HasFlag(SharedProviderTransferBlockReason.TargetSourceUsesTransferredSecret))
        {
            causes.Add("a target shared-provider source uses a secret that the transfer would replace");
        }

        return $"AI provider transfer is blocked because {string.Join("; ", causes)}. Transfer shared-provider state through its owning workflow first.";
    }
}
