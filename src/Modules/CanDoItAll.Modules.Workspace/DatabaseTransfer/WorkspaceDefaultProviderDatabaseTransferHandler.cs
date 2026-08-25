using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workspace;

public sealed class WorkspaceDefaultProviderDatabaseTransferHandler :
    IDatabaseTransferHandler
{
    public DatabaseTransferItemDescriptor Descriptor { get; } = new(
        "workspace-default-provider",
        "Workspace default provider",
        "Copies only the opaque workspace default-provider preference.",
        SortOrder: 21,
        IsSensitive: false);

    public async Task<DatabaseTransferItemPreview> PreviewAsync(
        DatabaseTransferContext context,
        CancellationToken cancellationToken = default)
    {
        var sourceProviderId = await ReadDefaultProviderIdAsync(
            context.SourceDbContext,
            cancellationToken);
        var targetProviderId = await ReadDefaultProviderIdAsync(
            context.TargetDbContext,
            cancellationToken);
        var targetContainsProvider = sourceProviderId.HasValue &&
            await context.TargetDbContext.Set<ProviderProfile>()
                .AsNoTracking()
                .AnyAsync(
                    provider => provider.Id == sourceProviderId.Value,
                    cancellationToken);

        return new DatabaseTransferItemPreview(
            Descriptor,
            sourceProviderId.HasValue && targetContainsProvider,
            sourceProviderId.HasValue
                ? "A workspace default-provider preference is available."
                : "The source workspace has no default-provider preference.",
            sourceProviderId.HasValue && !targetContainsProvider
                ? "Transfer the referenced provider profile before transferring this preference."
                : null,
            sourceProviderId.HasValue ? 1 : 0,
            targetProviderId.HasValue ? 1 : 0);
    }

    public async Task<DatabaseTransferItemResult> TransferAsync(
        DatabaseTransferContext context,
        CancellationToken cancellationToken = default)
    {
        var sourceProviderId = await ReadDefaultProviderIdAsync(
            context.SourceDbContext,
            cancellationToken);
        if (!sourceProviderId.HasValue)
        {
            return new DatabaseTransferItemResult(
                Descriptor.Key,
                Descriptor.Label,
                false,
                "The source workspace has no default-provider preference.",
                0);
        }

        var targetContainsProvider = await context.TargetDbContext
            .Set<ProviderProfile>()
            .AsNoTracking()
            .AnyAsync(
                provider => provider.Id == sourceProviderId.Value,
                cancellationToken);
        if (!targetContainsProvider)
        {
            throw new InvalidOperationException(
                "The target database does not contain the referenced provider profile.");
        }

        var targetSettings = await context.TargetDbContext.Set<WorkspaceSettings>()
            .OrderByDescending(settings => settings.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (targetSettings is null)
        {
            targetSettings = new WorkspaceSettings();
            await context.TargetDbContext.Set<WorkspaceSettings>()
                .AddAsync(targetSettings, cancellationToken);
        }

        targetSettings.DefaultProviderProfileId = sourceProviderId;
        targetSettings.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await context.TargetDbContext.SaveChangesAsync(cancellationToken);
        return new DatabaseTransferItemResult(
            Descriptor.Key,
            Descriptor.Label,
            true,
            "Copied the workspace default-provider preference.",
            1);
    }

    private static async Task<Guid?> ReadDefaultProviderIdAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
        => await dbContext.Set<WorkspaceSettings>()
            .AsNoTracking()
            .OrderByDescending(settings => settings.UpdatedAtUtc)
            .Select(settings => settings.DefaultProviderProfileId)
            .FirstOrDefaultAsync(cancellationToken);
}
