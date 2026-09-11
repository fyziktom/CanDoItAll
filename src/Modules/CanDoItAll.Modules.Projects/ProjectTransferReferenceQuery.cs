using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

public interface IProjectTransferReferenceQuery {
    Task<bool> TargetContainsAllAsync(DatabaseTransferOwnerRequest transfer, IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default);
}

public sealed class ProjectTransferReferenceQuery(DatabaseTransferOwnerSessionRunner sessions) : IProjectTransferReferenceQuery {
    public async Task<bool> TargetContainsAllAsync(DatabaseTransferOwnerRequest transfer, IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(projectIds);
        var ids = projectIds.Distinct().ToArray();
        await using var target = await sessions.CreateTargetAsync<ProjectsDbContext>(transfer, static options => new ProjectsDbContext(options), cancellationToken);
        return await target.Set<Project>().AsNoTracking().CountAsync(project => ids.Contains(project.Id), cancellationToken) == ids.Length;
    }
}
