using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workspace;

public sealed class WorkspaceDefaultProviderDatabaseTransferHandler(DatabaseTransferOperationRunner operations, DatabaseTransferOwnerSessionRunner sessions) :
    IDatabaseTransferHandler {
    public DatabaseTransferItemDescriptor Descriptor { get; } = new(
        "workspace-default-provider",
        "Workspace default provider",
        "Copies only the opaque workspace default-provider preference.",
        SortOrder: 21,
        IsSensitive: false);

    public async Task<DatabaseTransferItemPreview> PreviewAsync(
        DatabaseTransferOperation context,
        CancellationToken cancellationToken = default) {
        var sourceProviderId = await ReadDefaultProviderIdAsync(context.SourceProfile, cancellationToken);
        var targetProviderId = await ReadDefaultProviderIdAsync(context.TargetProfile, cancellationToken);
        return new DatabaseTransferItemPreview(
            Descriptor,
            sourceProviderId.HasValue,
            sourceProviderId.HasValue
                ? "A workspace default-provider preference is available."
                : "The source workspace has no default-provider preference.",
            null,
            sourceProviderId.HasValue ? 1 : 0,
            targetProviderId.HasValue ? 1 : 0);
    }

    public Task<DatabaseTransferItemResult> TransferAsync(DatabaseTransferOperation context, CancellationToken cancellationToken = default)
        => operations.RunTransferAsync(context, TransferCoreAsync, allowInMemoryTest: true, cancellationToken);

    private async Task<DatabaseTransferItemResult> TransferCoreAsync(DatabaseTransferOwnerRequest transfer, CancellationToken cancellationToken) {
        await using var source = await sessions.CreateSourceAsync<WorkspaceSettingsDbContext>(transfer, static options => new WorkspaceSettingsDbContext(options), cancellationToken);
        await using var target = await sessions.CreateTargetAsync<WorkspaceSettingsDbContext>(transfer, static options => new WorkspaceSettingsDbContext(options), cancellationToken);
        await sessions.AcquireTargetTableLocksAsync(transfer, target.Model.GetEntityTypes().Select(DatabaseTransferTable.From).ToArray(), cancellationToken);
        var sourceProviderId = await ReadDefaultProviderIdAsync(source, cancellationToken);
        if (!sourceProviderId.HasValue) {
            return new(Descriptor.Key, Descriptor.Label, false, "The source workspace has no default-provider preference.", 0);
        }
        var targetSettings = await target.Set<WorkspaceSettings>().OrderByDescending(settings => settings.UpdatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (targetSettings is null) {
            targetSettings = new WorkspaceSettings();
            target.Add(targetSettings);
        }
        targetSettings.DefaultProviderProfileId = sourceProviderId;
        targetSettings.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await target.SaveChangesAsync(cancellationToken);
        return new(Descriptor.Key, Descriptor.Label, true, "Copied the workspace default-provider preference.", 1);
    }

    private Task<Guid?> ReadDefaultProviderIdAsync(ResolvedDatabaseProfile profile, CancellationToken cancellationToken)
        => operations.RunIndependentAsync(profile, async (session, token) => {
            await using var owner = await operations.CreateOwnerAsync<WorkspaceSettingsDbContext>(session, static options => new WorkspaceSettingsDbContext(options), token);
            return await ReadDefaultProviderIdAsync(owner, token);
        }, cancellationToken);

    private static async Task<Guid?> ReadDefaultProviderIdAsync(
        WorkspaceSettingsDbContext dbContext,
        CancellationToken cancellationToken)
        => await dbContext.Set<WorkspaceSettings>()
            .AsNoTracking()
            .OrderByDescending(settings => settings.UpdatedAtUtc)
            .Select(settings => settings.DefaultProviderProfileId)
            .FirstOrDefaultAsync(cancellationToken);
}
