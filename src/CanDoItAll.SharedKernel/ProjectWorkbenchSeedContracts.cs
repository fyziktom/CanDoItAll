namespace CanDoItAll.SharedKernel;

public sealed record ProjectObjectSeedDraft(
    ProjectObjectType ObjectType,
    string Title,
    string Subtitle,
    string Notes,
    DateTimeOffset? StartUtc = null,
    DateTimeOffset? EndUtc = null);

public interface IProjectWorkbenchSeedService
{
    Task SeedProjectObjectsAsync(Guid projectId, IReadOnlyCollection<ProjectObjectSeedDraft> seeds, CancellationToken cancellationToken = default);
}
