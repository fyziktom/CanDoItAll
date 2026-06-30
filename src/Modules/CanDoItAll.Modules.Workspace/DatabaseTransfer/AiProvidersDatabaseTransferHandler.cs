using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Security;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workspace;

public sealed class AiProvidersDatabaseTransferHandler : IDatabaseTransferHandler
{
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
            .CountAsync(cancellationToken);
        var targetProviders = await context.TargetDbContext.Set<ProviderProfile>()
            .CountAsync(cancellationToken);

        return new DatabaseTransferItemPreview(
            Descriptor,
            sourceProviders > 0,
            $"{sourceProviders} provider profile(s) and {sourceSecretIds} referenced secret(s) are available.",
            sourceProviders == 0 ? "The source database does not contain AI provider profiles." : null,
            sourceProviders + sourceSecretIds,
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
}
