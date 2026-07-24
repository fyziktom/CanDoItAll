using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureSerializableMutationScope :
    IAsyncDisposable
{
    private readonly SerializableMutationScope innerScope;

    private ProjectStructureSerializableMutationScope(
        SerializableMutationScope innerScope)
    {
        this.innerScope = innerScope;
    }

    public static async Task<ProjectStructureSerializableMutationScope> BeginAsync(
        AppDbContext dbContext,
        string scopeKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);
        return new(await SerializableMutationScope.BeginAsync(
            dbContext,
            scopeKey,
            cancellationToken));
    }

    public static async Task<ProjectStructureSerializableMutationScope> BeginAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<string> scopeKeys,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(scopeKeys);
        return new(await SerializableMutationScope.BeginAsync(
            dbContext,
            scopeKeys,
            cancellationToken));
    }

    public Task CommitAsync(CancellationToken cancellationToken)
        => innerScope.CommitAsync(cancellationToken);

    public ValueTask DisposeAsync()
        => innerScope.DisposeAsync();

    public static string ForProject(Guid projectId)
        => $"project:{projectId:D}";

    public static IReadOnlyCollection<string> ForProjects(
        Guid firstProjectId,
        Guid secondProjectId)
        => new[] { firstProjectId, secondProjectId }
            .Distinct()
            .OrderBy(static projectId => projectId)
            .Select(ForProject)
            .ToArray();
}
