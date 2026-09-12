namespace CanDoItAll.Modules.Projects;

public interface IProjectSummaryQueryService {
    Task<ProjectSummary?> GetSummaryAsync(Guid projectId, CancellationToken cancellationToken = default);
}
