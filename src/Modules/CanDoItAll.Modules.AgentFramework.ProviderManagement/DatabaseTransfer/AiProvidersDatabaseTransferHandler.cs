using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class AiProvidersDatabaseTransferHandler(ISecretDatabaseTransferParticipant secretTransfer,
    DatabaseTransferOwnerSessionRunner sessions, DatabaseTransferOperationRunner operations, IEnumerable<IProviderDatabaseTransferGuard>? transferGuards = null) : IDatabaseTransferHandler {
    private readonly IReadOnlyList<IProviderDatabaseTransferGuard> guards = transferGuards?.ToArray() ?? [];

    public DatabaseTransferItemDescriptor Descriptor { get; } = new(
        "ai-providers", "AI providers", "Copies AI provider profiles and referenced encrypted secrets.", SortOrder: 20, IsSensitive: true);

    public async Task<DatabaseTransferItemPreview> PreviewAsync(DatabaseTransferOperation context, CancellationToken cancellationToken = default) {
        if (context.SourceProfile.Profile.ProviderKind != context.TargetProfile.Profile.ProviderKind ||
            context.SourceProfile.Profile.ProviderKind is not (DatabaseProviderKind.PostgreSql or DatabaseProviderKind.InMemory)) {
            throw new InvalidOperationException("AI provider transfer requires two PostgreSQL databases or two explicit InMemory test stores; mixed-provider transfer is unsupported.");
        }
        var sourceFacts = await operations.RunIndependentAsync(context.SourceProfile, async (session, token) => {
            await using var source = await operations.CreateOwnerAsync<ProvidersDbContext>(session, static options => new ProvidersDbContext(options), token);
            return (Count: await source.Set<ProviderProfile>().CountAsync(token),
                Secrets: await source.Set<ProviderProfile>().Where(profile => profile.ApiKeySecretId.HasValue)
                    .Select(profile => profile.ApiKeySecretId!.Value).Distinct().ToArrayAsync(token),
                Shared: await HasSharedProviderReferencesAsync(source, token));
        }, cancellationToken);
        var targetFacts = await operations.RunIndependentAsync(context.TargetProfile, async (session, token) => {
            await using var target = await operations.CreateOwnerAsync<ProvidersDbContext>(session, static options => new ProvidersDbContext(options), token);
            return (Count: await target.Set<ProviderProfile>().CountAsync(token), Shared: await HasSharedProviderReferencesAsync(target, token),
                SecretConflict: sourceFacts.Secrets.Length > 0 && await target.Set<SharedProviderSource>().AsNoTracking()
                    .AnyAsync(item => sourceFacts.Secrets.Contains(item.ApiTokenSecretId), token));
        }, cancellationToken);
        var sourceProviders = sourceFacts.Count;
        var sourceSecretIds = sourceFacts.Secrets;
        var targetProviders = targetFacts.Count;
        var blockReason = await FindTransferBlockReasonAsync(new(sourceFacts.Shared, targetFacts.Shared, targetFacts.SecretConflict), cancellationToken);
        return new(Descriptor, sourceProviders > 0 && blockReason is null,
            $"{sourceProviders} provider profile(s) and {sourceSecretIds.Length} referenced secret(s) are available.",
            sourceProviders == 0 ? "The source database does not contain AI provider profiles." : blockReason,
            sourceProviders + sourceSecretIds.Length, targetProviders);
    }

    public Task<DatabaseTransferItemResult> TransferAsync(DatabaseTransferOperation context, CancellationToken cancellationToken = default)
        => operations.RunTransferAsync(context, (transfer, token) => TransferCoreAsync(transfer, token),
            allowInMemoryTest: true, cancellationToken: cancellationToken);

    private async Task<DatabaseTransferItemResult> TransferCoreAsync(DatabaseTransferOwnerRequest transfer, CancellationToken cancellationToken) {
        await using var source = await sessions.CreateSourceAsync<ProvidersDbContext>(transfer, static options => new ProvidersDbContext(options), cancellationToken);
        await using var target = await sessions.CreateTargetAsync<ProvidersDbContext>(transfer, static options => new ProvidersDbContext(options), cancellationToken);
        var sourceProviders = await source.Set<ProviderProfile>().AsNoTracking().ToArrayAsync(cancellationToken);
        if (sourceProviders.Length == 0) {
            return new(Descriptor.Key, Descriptor.Label, false, "The source database has no AI provider profiles to transfer.", 0);
        }
        var secretIds = sourceProviders.Where(provider => provider.ApiKeySecretId.HasValue)
            .Select(provider => provider.ApiKeySecretId!.Value).Distinct().ToArray();
        Type[] lockTypes = [typeof(ProviderProfile), typeof(ProviderSharePublication), typeof(SharedProviderImport), typeof(SharedProviderSource)];
        var tables = lockTypes.Select(type => DatabaseTransferTable.From(target.Model.FindEntityType(type)
            ?? throw new InvalidOperationException("The Provider transfer owner has an incomplete model."))).ToList();
        if (secretIds.Length > 0) {
            tables.Add(await secretTransfer.GetTargetTableAsync(transfer, cancellationToken));
        }
        await sessions.AcquireTargetTableLocksAsync(transfer, tables, cancellationToken);
        var blockReason = await FindTransferBlockReasonAsync(source, target, secretIds, cancellationToken);
        if (blockReason is not null) {
            throw new InvalidOperationException(blockReason);
        }
        var targetProviders = await target.Set<ProviderProfile>().ToArrayAsync(cancellationToken);
        target.RemoveRange(targetProviders);
        await target.SaveChangesAsync(cancellationToken);
        target.ChangeTracker.Clear();
        var secretsCopied = secretIds.Length == 0 ? 0 : await secretTransfer.CopySelectedAsync(transfer, secretIds, cancellationToken);
        target.AddRange(sourceProviders);
        await target.SaveChangesAsync(cancellationToken);
        return new(Descriptor.Key, Descriptor.Label, true,
            $"Copied {sourceProviders.Length} AI provider profile(s) and {secretsCopied} referenced encrypted secret(s).",
            checked(sourceProviders.Length + secretsCopied));
    }

    private async Task<string?> FindTransferBlockReasonAsync(ProvidersDbContext source, ProvidersDbContext target,
        IReadOnlyCollection<Guid> sourceSecretIds, CancellationToken cancellationToken) {
        if (guards.Count == 0) {
            return null;
        }
        var inspection = new ProviderDatabaseTransferInspection(
            await HasSharedProviderReferencesAsync(source, cancellationToken),
            await HasSharedProviderReferencesAsync(target, cancellationToken),
            sourceSecretIds.Count > 0 && await target.Set<SharedProviderSource>().AsNoTracking()
                .AnyAsync(item => sourceSecretIds.Contains(item.ApiTokenSecretId), cancellationToken));
        return await FindTransferBlockReasonAsync(inspection, cancellationToken);
    }

    private async Task<string?> FindTransferBlockReasonAsync(ProviderDatabaseTransferInspection inspection, CancellationToken cancellationToken) {
        foreach (var guard in guards) {
            var reason = await guard.FindBlockReasonAsync(inspection, cancellationToken);
            if (!string.IsNullOrWhiteSpace(reason)) {
                return reason;
            }
        }
        return null;
    }

    private static async Task<bool> HasSharedProviderReferencesAsync(ProvidersDbContext context, CancellationToken cancellationToken)
        => await context.Set<ProviderSharePublication>().AsNoTracking().AnyAsync(cancellationToken)
            || await context.Set<SharedProviderImport>().AsNoTracking().AnyAsync(cancellationToken);

}
