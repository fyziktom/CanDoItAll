using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectStoragePlacementOwnerFact(StoragePlacementIntentId StorageIntentId,
    StoragePlacementRecoveryOwner Owner, Guid OriginalProjectId, ProjectWriteAdmission? OriginalAdmission,
    bool NativeReceiptPresent, StoragePlacementRecoveryBlock Block);

public sealed partial class ProjectStoragePlacementRecoveryQuery(IDbContextFactory<WorkbenchDbContext> factory,
    DbContextOptions<WorkbenchDbContext> options, CoordinatedDatabaseTransaction transactions) {
    private static readonly JsonSerializerOptions WorkflowJson = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyDictionary<Guid, ProjectStoragePlacementOwnerFact>> ObserveAsync(
        IReadOnlyCollection<StoragePlacementIntentId> intentIds, CancellationToken cancellationToken = default) {
        var ids = RequireBoundedIds(intentIds);
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        return await ReadAsync(database, ids, false, cancellationToken);
    }

    public async Task<ProjectStoragePlacementOwnerFact?> ObserveForMutationAsync(StoragePlacementIntentId intentId,
        CancellationToken cancellationToken = default) {
        var ids = RequireBoundedIds([intentId]);
        await using var database = await transactions.CreateEnlistedAsync(options, static configured => new WorkbenchDbContext(configured),
            cancellationToken);
        return (await ReadAsync(database, ids, true, cancellationToken)).GetValueOrDefault(intentId.Value);
    }

    private static Guid[] RequireBoundedIds(IReadOnlyCollection<StoragePlacementIntentId> intentIds) {
        ArgumentNullException.ThrowIfNull(intentIds);
        if (intentIds.Count > 128 || intentIds.Any(id => id.Value == Guid.Empty)) {
            throw new ArgumentException("Storage recovery observes at most 128 exact, nonempty placement intents.", nameof(intentIds));
        }
        return intentIds.Select(id => id.Value).Distinct().ToArray();
    }

    private static async Task<IReadOnlyDictionary<Guid, ProjectStoragePlacementOwnerFact>> ReadAsync(WorkbenchDbContext database,
        Guid[] ids, bool forMutation, CancellationToken cancellationToken) {
        if (ids.Length == 0) {
            return new Dictionary<Guid, ProjectStoragePlacementOwnerFact>();
        }
        IQueryable<ProjectProcessAssetContributionRecord> processRows = database.Set<ProjectProcessAssetContributionRecord>();
        if (forMutation && database.Database.IsNpgsql()) {
            processRows = database.Set<ProjectProcessAssetContributionRecord>().FromSqlInterpolated($"""
                SELECT * FROM "Workbench_ProcessAssetContributions"
                WHERE "StorageIntentId" = ANY ({ids}) ORDER BY "IntentId" FOR SHARE
                """);
        }
        var processes = await processRows.AsNoTracking().Where(row => ids.Contains(row.StorageIntentId))
            .Select(row => new {
                row.IntentId, row.DatabaseProfileId, row.ProjectId, row.ProjectLifetimeId, row.SourceExecutionRunId,
                row.NativeObjectId, row.StorageIntentId, row.PlanFingerprint, PlanPresent = row.PlanJson != "",
                Imported = row.ImportedHistory != null, ReceiptPresent = row.ReceiptJson != ""
            }).ToArrayAsync(cancellationToken);
        IQueryable<ProjectWorkflowContributionRecord> workflowRows = database.Set<ProjectWorkflowContributionRecord>();
        if (forMutation && database.Database.IsNpgsql()) {
            workflowRows = database.Set<ProjectWorkflowContributionRecord>().FromSqlInterpolated($"""
                SELECT * FROM "Workbench_WorkflowContributionReceipts"
                WHERE "StoragePlacementIntentId" = ANY ({ids}) ORDER BY "RunId", "OccurrencePath", "Slot" FOR SHARE
                """);
        }
        var workflows = await workflowRows.AsNoTracking()
            .Where(row => row.StoragePlacementIntentId.HasValue && ids.Contains(row.StoragePlacementIntentId.Value))
            .Select(row => new { Id = row.StoragePlacementIntentId!.Value, row.RunId, row.OccurrencePath, row.Slot,
                row.ProjectId, row.DatabaseProfileId, row.ProjectLifetimeId, row.NativeObjectId, row.PreparedAtUtc,
                row.PlanJson, Imported = row.ImportedHistory != null, ReceiptPresent = row.ReceiptJson != "" })
            .ToArrayAsync(cancellationToken);
        var observations = new List<ProjectStoragePlacementOwnerFact>();
        foreach (var row in processes) {
            ProjectWriteAdmission? admission = null;
            var block = StoragePlacementRecoveryBlock.None;
            if (!IsValidProcessAssociation(row.IntentId, row.DatabaseProfileId, row.ProjectId, row.ProjectLifetimeId,
                row.SourceExecutionRunId, row.NativeObjectId, row.PlanPresent, row.PlanFingerprint)) {
                block = StoragePlacementRecoveryBlock.InvalidOwnerAssociation;
            } else {
                admission = new(row.DatabaseProfileId, row.ProjectId, row.ProjectLifetimeId);
                if (row.Imported) {
                    block = StoragePlacementRecoveryBlock.ImportedHistory;
                }
            }
            observations.Add(new(new(row.StorageIntentId), StoragePlacementRecoveryOwner.ProcessAsset, row.ProjectId,
                admission, row.ReceiptPresent, block));
        }
        foreach (var row in workflows) {
            var block = WorkflowAssociationBlock(row.RunId, row.OccurrencePath, row.Slot, row.ProjectId,
                row.DatabaseProfileId, row.ProjectLifetimeId, row.NativeObjectId, row.PreparedAtUtc, row.PlanJson,
                row.ReceiptPresent, row.Imported, out var admission);
            observations.Add(new(new(row.Id), StoragePlacementRecoveryOwner.WorkflowAsset, row.ProjectId,
                admission, row.ReceiptPresent, block));
        }
        return observations.GroupBy(item => item.StorageIntentId.Value).ToDictionary(group => group.Key, group =>
            group.Count() == 1 ? group.Single() : new(new(group.Key), StoragePlacementRecoveryOwner.Unknown, Guid.Empty,
                null, false, StoragePlacementRecoveryBlock.InvalidOwnerAssociation));
    }

    private static bool IsValidProcessAssociation(Guid intentId, Guid profileId, Guid projectId, Guid lifetimeId,
        Guid executionRunId, Guid nativeObjectId, bool planPresent, string fingerprint)
        => intentId != Guid.Empty && profileId != Guid.Empty && projectId != Guid.Empty && lifetimeId != Guid.Empty &&
            executionRunId != Guid.Empty && nativeObjectId != Guid.Empty && planPresent &&
            fingerprint.Length == 64 && fingerprint.All(char.IsAsciiHexDigit);

    private static bool IsValidWorkflowIdentity(Guid runId, string occurrencePath, int slot)
        => runId != Guid.Empty && !string.IsNullOrWhiteSpace(occurrencePath) && occurrencePath.Length <= 64 && slot is >= 0 and <= 4095;

    private static StoragePlacementRecoveryBlock WorkflowAssociationBlock(Guid runId, string occurrencePath, int slot,
        Guid projectId, Guid? profileId, Guid? lifetimeId, Guid nativeObjectId, DateTimeOffset? preparedAtUtc,
        string planJson, bool receiptPresent, bool imported, out ProjectWriteAdmission? admission) {
        admission = null;
        if (imported) {
            return StoragePlacementRecoveryBlock.ImportedHistory;
        }
        if (profileId is null && lifetimeId is null) {
            return StoragePlacementRecoveryBlock.OriginalProjectLifetimeMissing;
        }
        if (profileId.GetValueOrDefault() == Guid.Empty || projectId == Guid.Empty || lifetimeId.GetValueOrDefault() == Guid.Empty ||
            !IsValidWorkflowIdentity(runId, occurrencePath, slot) || !preparedAtUtc.HasValue ||
            receiptPresent && nativeObjectId == Guid.Empty || string.IsNullOrWhiteSpace(planJson)) {
            return StoragePlacementRecoveryBlock.InvalidOwnerAssociation;
        }
        try {
            var plan = JsonSerializer.Deserialize<WorkflowStructureOutputPlan>(planJson, WorkflowJson);
            if (plan?.Identity?.Occurrence is not { } occurrence || occurrence.RunId.Value != runId || occurrence.Path != occurrencePath ||
                plan.Identity.Slot != slot || plan.ProjectId != projectId || plan.Kind != WorkflowStructureOutputKind.Asset ||
                plan.ProjectLifetime is not { } original || original.DatabaseProfileId != profileId ||
                original.ProjectId != projectId || original.LifetimeId != lifetimeId ||
                plan.Fingerprint is not { Length: 64 } || !plan.Fingerprint.All(char.IsAsciiHexDigit) ||
                plan.SourceAuthorityFingerprint is not { Length: 64 } || !plan.SourceAuthorityFingerprint.All(char.IsAsciiHexDigit)) {
                return StoragePlacementRecoveryBlock.InvalidOwnerAssociation;
            }
            admission = new(original.DatabaseProfileId, original.ProjectId, original.LifetimeId);
            return StoragePlacementRecoveryBlock.None;
        } catch (Exception exception) when (exception is JsonException or ArgumentException) {
            return StoragePlacementRecoveryBlock.InvalidOwnerAssociation;
        }
    }
}
