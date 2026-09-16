using CanDoItAll.AgentFramework.Models;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed record WorkflowAssetCheckpointIdentity(Guid RunId, string OccurrencePath, int Slot, Guid StorageIntentId,
    WorkflowProjectLifetime? ProjectLifetime = null);

public sealed class WorkflowAssetCheckpointQuery(IDbContextFactory<WorkflowDbContext> factory) {
    public async Task<IReadOnlyDictionary<WorkflowAssetCheckpointIdentity, AssetRecoveryCheckpointState>> ReadAsync(
        IReadOnlyCollection<WorkflowAssetCheckpointIdentity> identities, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identities);
        if (identities.Count > 128 || identities.Any(item => item.RunId == Guid.Empty || item.StorageIntentId == Guid.Empty ||
            string.IsNullOrWhiteSpace(item.OccurrencePath) || item.OccurrencePath.Length > 64 || item.Slot is < 0 or > 4095)) {
            throw new ArgumentException("Asset recovery observes at most 128 exact Workflow output identities.", nameof(identities));
        }
        if (identities.Count == 0) {
            return new Dictionary<WorkflowAssetCheckpointIdentity, AssetRecoveryCheckpointState>();
        }
        var ids = identities.Select(item => item.StorageIntentId).Distinct().ToArray();
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await database.Set<WorkflowStructureOutputRecord>().AsNoTracking()
            .Where(row => row.StoragePlacementIntentId.HasValue && ids.Contains(row.StoragePlacementIntentId.Value))
            .Select(row => new { row.RunId, row.OccurrencePath, row.Slot, StorageIntentId = row.StoragePlacementIntentId!.Value,
                row.DatabaseProfileId, row.ProjectId, row.ProjectLifetimeId,
                row.IsComplete, ReceiptPresent = row.ReceiptJson != "" }).Take(129).ToArrayAsync(cancellationToken);
        return identities.Distinct().ToDictionary(identity => identity, identity => {
            var matches = rows.Where(row => row.StorageIntentId == identity.StorageIntentId).ToArray();
            if (rows.Length > 128 || matches.Length != 1 || matches[0].RunId != identity.RunId ||
                matches[0].OccurrencePath != identity.OccurrencePath || matches[0].Slot != identity.Slot ||
                matches[0].DatabaseProfileId != identity.ProjectLifetime?.DatabaseProfileId ||
                matches[0].ProjectId != identity.ProjectLifetime?.ProjectId || matches[0].ProjectLifetimeId != identity.ProjectLifetime?.LifetimeId ||
                matches[0].IsComplete != matches[0].ReceiptPresent) {
                return AssetRecoveryCheckpointState.Unavailable;
            }
            return matches[0].IsComplete ? AssetRecoveryCheckpointState.Completed : AssetRecoveryCheckpointState.Pending;
        });
    }
}
