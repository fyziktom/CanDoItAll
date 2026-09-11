using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureSerializableMutationScope(SerializableMutationScope innerScope) : IAsyncDisposable {
    internal const string ManagedStorageBindingScopeKey = "workbench:managed-storage-bindings";

    public Task CommitAsync(CancellationToken cancellationToken) => innerScope.CommitAsync(cancellationToken);
    public ValueTask DisposeAsync() => innerScope.DisposeAsync();
    public static string ForProject(Guid projectId) => ProjectMutationScopeKeys.ForProject(projectId);
    public static IReadOnlyCollection<string> ForProjects(Guid firstProjectId, Guid secondProjectId) =>
        new[] { firstProjectId, secondProjectId }.Distinct().OrderBy(projectId => projectId).Select(ForProject).ToArray();
}

public sealed class ProjectStructureMutationScopeFactory(
    ProjectRecordQueryService projects,
    CoordinatedDatabaseTransaction transactions) {
    internal Task<ProjectStructureSerializableMutationScope> BeginAsync(WorkbenchDbContext context,
        string scopeKey, CancellationToken cancellationToken) => BeginAsync(context, [scopeKey], cancellationToken);

    internal Task<ProjectStructureSerializableMutationScope> BeginBindingWriteAsync(WorkbenchDbContext context,
        string scopeKey, CancellationToken cancellationToken) => BeginBindingWriteAsync(context, [scopeKey], cancellationToken);

    internal Task<ProjectStructureSerializableMutationScope> BeginBindingWriteAsync(WorkbenchDbContext context,
        IReadOnlyCollection<string> scopeKeys, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(scopeKeys);
        return BeginAsync(context, scopeKeys.Append(ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey).ToArray(), cancellationToken);
    }

    internal async Task<ProjectStructureSerializableMutationScope> BeginAsync(WorkbenchDbContext context,
        IReadOnlyCollection<string> scopeKeys, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(scopeKeys);
        var scope = new ProjectStructureSerializableMutationScope(
            await SerializableMutationScope.BeginAsync(context, scopeKeys, cancellationToken));
        try {
            var projectIds = scopeKeys.Select(TryParseProjectId).OfType<Guid>().Distinct().ToArray();
            if (projectIds.Length > 0) {
                using var coordination = transactions.Enter(context);
                var existing = await projects.GetManyForMutationAsync(projectIds, cancellationToken);
                var missing = projectIds.Except(existing.Select(project => project.Id)).Select(id => (Guid?)id).FirstOrDefault();
                if (missing.HasValue) {
                    throw new ProjectStructureAgentException(404, "ProjectNotFound",
                        $"Project '{missing.Value:D}' does not exist and cannot accept project-structure mutations.");
                }
            }
            return scope;
        } catch {
            await scope.DisposeAsync();
            throw;
        }
    }

    private static Guid? TryParseProjectId(string scopeKey) {
        const string prefix = "project:";
        return scopeKey.StartsWith(prefix, StringComparison.Ordinal) && Guid.TryParse(scopeKey[prefix.Length..], out var projectId)
            ? projectId : null;
    }
}
