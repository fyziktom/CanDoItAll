using System.Data;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureNodeSnapshotReader(IDbContextFactory<WorkbenchDbContext> contexts,
    CoordinatedDatabaseTransaction transactions, ProjectWriteAdmissionService admissions) {
    public async Task<ProjectStructureNode> ReadAsync(ProjectWriteAdmission project, string nodeId, CancellationToken cancellationToken) {
        await using var database = await contexts.CreateDbContextAsync(cancellationToken);
        await using var snapshot = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken) : null;
        using var participation = transactions.Enter(database);
        await admissions.RequireManyForMutationAsync([project], cancellationToken);
        var record = await database.Set<ProjectObjectRecord>().AsNoTracking().SingleOrDefaultAsync(
            node => node.ProjectId == project.ProjectId && node.NodeKey == nodeId && !node.IsSystemManaged, cancellationToken)
            ?? throw new ProjectStructureEditConflictException("The original node was deleted or is no longer editable. Your draft is retained.");
        await ProjectNodeBindingStorage.LoadAsync(database, [record], cancellationToken);
        return ProjectWorkbenchNodeMapper.MapStructureNode(record);
    }
}
