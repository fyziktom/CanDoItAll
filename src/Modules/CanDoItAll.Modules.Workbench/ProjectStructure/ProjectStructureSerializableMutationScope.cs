using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureSerializableMutationScope :
    IAsyncDisposable
{
    internal const string ManagedStorageBindingScopeKey =
        "workbench:managed-storage-bindings";

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
        var scope = new ProjectStructureSerializableMutationScope(
            await SerializableMutationScope.BeginAsync(
                dbContext,
                scopeKey,
                cancellationToken));
        try
        {
            await ValidateProjectScopesAsync(dbContext, [scopeKey], cancellationToken);
            return scope;
        }
        catch
        {
            await scope.DisposeAsync();
            throw;
        }
    }

    public static Task<ProjectStructureSerializableMutationScope> BeginBindingWriteAsync(
        AppDbContext dbContext,
        string scopeKey,
        CancellationToken cancellationToken)
        => BeginBindingWriteAsync(dbContext, [scopeKey], cancellationToken);

    public static Task<ProjectStructureSerializableMutationScope> BeginBindingWriteAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<string> scopeKeys,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scopeKeys);
        return BeginAsync(
            dbContext,
            scopeKeys.Append(ManagedStorageBindingScopeKey).ToArray(),
            cancellationToken);
    }

    public static async Task<ProjectStructureSerializableMutationScope> BeginAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<string> scopeKeys,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(scopeKeys);
        var scope = new ProjectStructureSerializableMutationScope(
            await SerializableMutationScope.BeginAsync(
                dbContext,
                scopeKeys,
                cancellationToken));
        try
        {
            await ValidateProjectScopesAsync(dbContext, scopeKeys, cancellationToken);
            return scope;
        }
        catch
        {
            await scope.DisposeAsync();
            throw;
        }
    }

    public Task CommitAsync(CancellationToken cancellationToken)
        => innerScope.CommitAsync(cancellationToken);

    public ValueTask DisposeAsync()
        => innerScope.DisposeAsync();

    private static async Task ValidateProjectScopesAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<string> scopeKeys,
        CancellationToken cancellationToken)
    {
        var projectIds = scopeKeys
            .Select(TryParseProjectId)
            .OfType<Guid>()
            .Distinct()
            .ToArray();
        if (projectIds.Length == 0)
        {
            return;
        }

        var existingProjectIds = await dbContext.Set<Project>()
            .AsNoTracking()
            .Where(project => projectIds.Contains(project.Id))
            .Select(project => project.Id)
            .ToListAsync(cancellationToken);
        var missingProjectId = projectIds
            .Except(existingProjectIds)
            .Select(static projectId => (Guid?)projectId)
            .FirstOrDefault();
        if (!missingProjectId.HasValue)
        {
            return;
        }

        throw new ProjectStructureAgentException(
            404,
            "ProjectNotFound",
            $"Project '{missingProjectId.Value:D}' does not exist and cannot accept project-structure mutations.");
    }

    private static Guid? TryParseProjectId(string scopeKey)
    {
        const string prefix = "project:";
        return scopeKey.StartsWith(prefix, StringComparison.Ordinal) &&
               Guid.TryParse(scopeKey[prefix.Length..], out var projectId)
            ? projectId
            : null;
    }

    public static string ForProject(Guid projectId)
        => ProjectMutationScopeKeys.ForProject(projectId);

    public static IReadOnlyCollection<string> ForProjects(
        Guid firstProjectId,
        Guid secondProjectId)
        => new[] { firstProjectId, secondProjectId }
            .Distinct()
            .OrderBy(static projectId => projectId)
            .Select(ForProject)
            .ToArray();
}
