using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectProcessAssetReceiptRecoveryBinding(StoragePlacementIntentId StorageIntentId,
    ProjectWriteAdmission OriginalProject, AgentToolSessionReference OriginalSession,
    AgentToolApprovalBinding OriginalProposal, string ToolName);

public sealed class ProjectProcessAssetReceiptRecoveryQuery(IDbContextFactory<WorkbenchDbContext> factory,
    ProjectProcessLaunchAuthorityService authorities) {
    public async Task<ProjectProcessAssetReceiptRecoveryBinding?> ReadAsync(StoragePlacementIntentId intentId,
        CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfEqual(intentId.Value, Guid.Empty);
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var row = await database.Set<ProjectProcessAssetContributionRecord>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.StorageIntentId == intentId.Value, cancellationToken);
        if (row is null) {
            return null;
        }
        if (row.ImportedHistory is not null) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Blocked);
        }
        var plan = ProjectProcessAssetPersistence.ReadPlan(row);
        await using var read = await authorities.AcquireResultReadAsync(plan.Execution.SourceAuthority!, cancellationToken);
        var producer = plan.Producer;
        return new(plan.StorageIntentId, plan.ProjectAdmission, producer.Session,
            new(producer.BatchId, producer.IntentId, producer.Payload.SemanticVersion, producer.Payload.Digest), producer.Payload.ToolName);
    }
}
