using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed partial class ProjectWorkbenchService {
    internal async Task<ProjectWorkflowPreparedContribution?> FindPreparedWorkflowAssetAsync(StoragePlacementIntentId storageIntent,
        CancellationToken cancellationToken) {
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await database.Set<ProjectWorkflowContributionRecord>().AsNoTracking()
            .Where(row => row.StoragePlacementIntentId == storageIntent.Value).Take(2).ToArrayAsync(cancellationToken);
        if (rows.Length > 1) {
            throw new InvalidOperationException("The Storage intent has more than one Workflow contribution association.");
        }
        if (rows.Length == 0 || rows[0].PlanJson.Length == 0) {
            return null;
        }
        var prepared = ReadPreparedContribution(rows[0]);
        if (prepared.Plan.Kind != WorkflowStructureOutputKind.Asset || prepared.StoragePlacementIntentId != storageIntent.Value) {
            throw new InvalidOperationException("The Storage intent does not match the original Workflow asset contribution.");
        }
        return prepared;
    }
}
