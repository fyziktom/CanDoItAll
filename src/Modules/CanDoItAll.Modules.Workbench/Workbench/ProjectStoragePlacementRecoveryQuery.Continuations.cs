using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectStorageContinuationFact(StoragePlacementIntentId StorageIntentId,
    StoragePlacementRecoveryOwner Owner, Guid SourceRunId, Guid? AgentIntentId, string? OccurrencePath, int? Slot,
    ProjectWriteAdmission? OriginalAdmission, bool NativeReceiptPresent, bool ImportedHistory, bool SourceAssociationValid);

public sealed record ProjectStorageContinuationPage(IReadOnlyList<ProjectStorageContinuationFact> Items, int? NextOffset);

public sealed partial class ProjectStoragePlacementRecoveryQuery {
    public async Task<ProjectStorageContinuationPage> ListContinuationCandidatesAsync(Guid? projectId, int take, int offset,
        CancellationToken cancellationToken = default) {
        if (take is < 1 or > 128 || offset < 0 || offset > int.MaxValue - 129 || projectId == Guid.Empty) {
            throw new ArgumentOutOfRangeException(nameof(take));
        }
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var processQuery = database.Set<ProjectProcessAssetContributionRecord>().AsNoTracking();
        var workflowQuery = database.Set<ProjectWorkflowContributionRecord>().AsNoTracking()
            .Where(row => row.StoragePlacementIntentId.HasValue);
        if (projectId is { } selected) {
            processQuery = processQuery.Where(row => row.ProjectId == selected);
            workflowQuery = workflowQuery.Where(row => row.ProjectId == selected);
        }
        var scanned = await processQuery.Select(row => row.StorageIntentId)
            .Concat(workflowQuery.Select(row => row.StoragePlacementIntentId!.Value)).Distinct()
            .OrderBy(id => id).Skip(offset).Take(take + 1).ToArrayAsync(cancellationToken);
        var ids = scanned.Take(take).ToArray();
        var processes = await database.Set<ProjectProcessAssetContributionRecord>().AsNoTracking()
            .Where(row => ids.Contains(row.StorageIntentId)).Select(row => new {
            row.StorageIntentId, row.SourceExecutionRunId, row.IntentId, row.DatabaseProfileId, row.ProjectId,
            row.ProjectLifetimeId, row.NativeObjectId, row.PlanFingerprint, PlanPresent = row.PlanJson != "",
            NativeReceiptPresent = row.ReceiptJson != "", ImportedHistory = row.ImportedHistory != null
        }).Take(129).ToArrayAsync(cancellationToken);
        var workflows = await database.Set<ProjectWorkflowContributionRecord>().AsNoTracking()
            .Where(row => row.StoragePlacementIntentId.HasValue && ids.Contains(row.StoragePlacementIntentId.Value)).Select(row => new {
            StorageIntentId = row.StoragePlacementIntentId!.Value, row.RunId, row.OccurrencePath, row.Slot,
            row.ProjectId, row.DatabaseProfileId, row.ProjectLifetimeId, row.NativeObjectId, row.PreparedAtUtc, row.PlanJson,
            NativeReceiptPresent = row.ReceiptJson != "", ImportedHistory = row.ImportedHistory != null
        }).Take(129).ToArrayAsync(cancellationToken);
        var nextOffset = scanned.Length > take ? offset + take : (int?)null;
        if (processes.Length > 128 || workflows.Length > 128) {
            return new(ids.Where(id => id != Guid.Empty).Select(id => Unavailable(id)).ToArray(), nextOffset);
        }
        var candidates = new List<ProjectStorageContinuationFact>();
        foreach (var row in processes.Where(row => row.StorageIntentId != Guid.Empty)) {
            var valid = IsValidProcessAssociation(row.IntentId, row.DatabaseProfileId, row.ProjectId, row.ProjectLifetimeId,
                row.SourceExecutionRunId, row.NativeObjectId, row.PlanPresent, row.PlanFingerprint);
            candidates.Add(new(new(row.StorageIntentId), StoragePlacementRecoveryOwner.ProcessAsset, row.SourceExecutionRunId,
                row.IntentId, null, null, valid ? new(row.DatabaseProfileId, row.ProjectId, row.ProjectLifetimeId) : null,
                row.NativeReceiptPresent, row.ImportedHistory, valid));
        }
        foreach (var row in workflows.Where(row => row.StorageIntentId != Guid.Empty)) {
            var block = WorkflowAssociationBlock(row.RunId, row.OccurrencePath, row.Slot, row.ProjectId,
                row.DatabaseProfileId, row.ProjectLifetimeId, row.NativeObjectId, row.PreparedAtUtc, row.PlanJson,
                row.NativeReceiptPresent, row.ImportedHistory, out var admission);
            var valid = block == StoragePlacementRecoveryBlock.None ||
                block == StoragePlacementRecoveryBlock.OriginalProjectLifetimeMissing && IsValidWorkflowIdentity(row.RunId, row.OccurrencePath, row.Slot);
            candidates.Add(new(new(row.StorageIntentId), StoragePlacementRecoveryOwner.WorkflowAsset, row.RunId, null,
                row.OccurrencePath, row.Slot, admission, row.NativeReceiptPresent, row.ImportedHistory, valid));
        }
        var grouped = candidates.GroupBy(item => item.StorageIntentId.Value).ToDictionary(group => group.Key, group =>
            group.Count() == 1 ? group.Single() : Unavailable(group.Key));
        return new(ids.Where(grouped.ContainsKey).Select(id => grouped[id]).ToArray(), nextOffset);
    }

    private static ProjectStorageContinuationFact Unavailable(Guid storageIntentId)
        => new(new(storageIntentId), StoragePlacementRecoveryOwner.Unknown, Guid.Empty, null, null, null, null, false, false, false);
}
