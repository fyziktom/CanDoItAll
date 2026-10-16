using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

public interface IProjectTransferReferenceQuery {
    IReadOnlyCollection<Type> TargetEntityTypes { get; }
    Task<bool> TargetContainsAllAsync(DatabaseTransferOwnerRequest transfer, IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default);
}

public sealed class ProjectTransferReferenceQuery(DatabaseTransferOwnerSessionRunner sessions) : IProjectTransferReferenceQuery {
    public IReadOnlyCollection<Type> TargetEntityTypes { get; } = [
        typeof(Project), typeof(ProjectPhase), typeof(ProjectOptionSelection), typeof(ProjectHierarchyLink),
        typeof(ProjectRetirementRecord), typeof(ProjectCreationReservationRecord), typeof(ProjectOperationStateRecord),
        typeof(ProjectOperationEpochRecord), typeof(ProjectDefinitionReceiptRecord)
    ];

    public async Task<bool> TargetContainsAllAsync(DatabaseTransferOwnerRequest transfer, IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(projectIds);
        var ids = projectIds.Distinct().ToArray();
        await using var target = await sessions.CreateTargetAsync<ProjectsDbContext>(transfer, static options => new ProjectsDbContext(options), cancellationToken);
        return await target.Set<Project>().AsNoTracking().CountAsync(project => ids.Contains(project.Id), cancellationToken) == ids.Length;
    }
}
