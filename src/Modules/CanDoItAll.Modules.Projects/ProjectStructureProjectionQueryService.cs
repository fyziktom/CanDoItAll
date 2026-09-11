using System.Linq.Expressions;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

public sealed record ProjectStructureProjectFact(Guid Id, string Name, string Description, string Objective,
    ProjectStatus Status, string CurrentPhase, DateTimeOffset CreatedAtUtc);

public sealed record ProjectStructurePhaseFact(Guid Id, Guid ProjectId, string Name, string Goal,
    ProjectPhaseStatus Status, int OrderIndex, DateTime? StartDateUtc, DateTime? EndDateUtc);

public sealed record ProjectStructureHierarchyLinkFact(Guid ParentProjectId, Guid ChildProjectId);

public sealed record ProjectStructureProjectFacts(ProjectStructureProjectFact Project,
    IReadOnlyList<ProjectStructureProjectFact> Projects,
    IReadOnlyList<ProjectStructureHierarchyLinkFact> HierarchyLinks,
    IReadOnlyList<ProjectStructurePhaseFact> Phases);

public static class ProjectStructureProjectionQueryLimits {
    public const int MaximumProjects = 2048;
    public const int MaximumLinks = 8192;
    public const int MaximumPhases = 1024;
    public const int FrontierBatchSize = 64;
}

public sealed class ProjectStructureProjectionLimitException(string dimension, int maximum)
    : InvalidOperationException($"Project structure exceeds the {dimension} projection limit of {maximum}. Select a smaller project subtree.");

public sealed class ProjectStructureProjectionQueryService(
    IDbContextFactory<ProjectsDbContext> factory,
    DbContextOptions<ProjectsDbContext> options,
    CoordinatedDatabaseTransaction transactions) {
    private static readonly Expression<Func<Project, ProjectStructureProjectFact>> ProjectFact = project => new(
        project.Id, project.Name, project.Description, project.Objective, project.Status,
        project.CurrentPhase, project.CreatedAtUtc);

    public async Task<ProjectStructureProjectFacts?> GetAsync(Guid projectId, CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await ReadAsync(context, projectId, cancellationToken);
    }

    public async Task<ProjectStructureProjectFacts?> GetForMutationAsync(Guid projectId, CancellationToken cancellationToken = default) {
        await using var context = await transactions.CreateEnlistedAsync(options, static value => new ProjectsDbContext(value), cancellationToken);
        return await ReadAsync(context, projectId, cancellationToken);
    }

    public async Task<Guid?> GetPhaseProjectIdAsync(Guid phaseId, CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<ProjectPhase>().AsNoTracking().Where(phase => phase.Id == phaseId)
            .Select(phase => (Guid?)phase.ProjectId).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetChildIdsAsync(IReadOnlyCollection<Guid> parentIds,
        IReadOnlyCollection<Guid> excludedIds, int take, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(parentIds);
        ArgumentNullException.ThrowIfNull(excludedIds);
        if (parentIds.Count > ProjectStructureProjectionQueryLimits.FrontierBatchSize ||
            excludedIds.Count > ProjectStructureProjectionQueryLimits.MaximumProjects ||
            take is < 1 or > ProjectStructureProjectionQueryLimits.MaximumProjects + 1 ||
            parentIds.Concat(excludedIds).Any(id => id == Guid.Empty)) {
            throw new ArgumentException("Project hierarchy frontier exceeds its supported bounded query or contains an empty identifier.");
        }
        if (parentIds.Count == 0) {
            return [];
        }
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var parents = parentIds.Distinct().ToArray();
        var excluded = excludedIds.Distinct().ToArray();
        return await context.Set<ProjectHierarchyLink>().AsNoTracking()
            .Where(link => parents.Contains(link.ParentProjectId) && !excluded.Contains(link.ChildProjectId))
            .Select(link => link.ChildProjectId).Distinct().OrderBy(id => id).Take(take).ToArrayAsync(cancellationToken);
    }

    private static async Task<ProjectStructureProjectFacts?> ReadAsync(ProjectsDbContext context, Guid projectId,
        CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfEqual(projectId, Guid.Empty);
        var project = await context.Set<Project>().AsNoTracking().Where(item => item.Id == projectId)
            .Select(ProjectFact).SingleOrDefaultAsync(cancellationToken);
        if (project is null) {
            return null;
        }
        var descendants = new HashSet<Guid> { projectId };
        Guid[] frontier = [projectId];
        var outgoing = new List<ProjectStructureHierarchyLinkFact>();
        while (frontier.Length > 0) {
            var next = new HashSet<Guid>();
            foreach (var parents in frontier.Chunk(ProjectStructureProjectionQueryLimits.FrontierBatchSize)) {
                var remaining = ProjectStructureProjectionQueryLimits.MaximumLinks - outgoing.Count;
                var links = await context.Set<ProjectHierarchyLink>().AsNoTracking()
                    .Where(link => parents.Contains(link.ParentProjectId))
                    .OrderBy(link => link.ParentProjectId).ThenBy(link => link.ChildProjectId)
                    .Select(link => new ProjectStructureHierarchyLinkFact(link.ParentProjectId, link.ChildProjectId))
                    .Take(remaining + 1).ToListAsync(cancellationToken);
                RequireLimit(outgoing.Count + links.Count, ProjectStructureProjectionQueryLimits.MaximumLinks, "hierarchy links");
                outgoing.AddRange(links);
                foreach (var link in links) {
                    if (descendants.Add(link.ChildProjectId)) {
                        RequireLimit(descendants.Count, ProjectStructureProjectionQueryLimits.MaximumProjects, "projects");
                        next.Add(link.ChildProjectId);
                    }
                }
            }
            frontier = next.ToArray();
        }
        var descendantIds = descendants.ToArray();
        var incoming = await context.Set<ProjectHierarchyLink>().AsNoTracking()
            .Where(link => descendantIds.Contains(link.ChildProjectId))
            .OrderBy(link => link.ParentProjectId).ThenBy(link => link.ChildProjectId)
            .Select(link => new ProjectStructureHierarchyLinkFact(link.ParentProjectId, link.ChildProjectId))
            .Take(ProjectStructureProjectionQueryLimits.MaximumLinks + 1).ToListAsync(cancellationToken);
        RequireLimit(incoming.Count, ProjectStructureProjectionQueryLimits.MaximumLinks, "hierarchy links");
        var projectIds = descendants.Concat(incoming.Select(link => link.ParentProjectId)).Distinct().ToArray();
        RequireLimit(projectIds.Length, ProjectStructureProjectionQueryLimits.MaximumProjects, "projects");
        var projects = await context.Set<Project>().AsNoTracking().Where(item => projectIds.Contains(item.Id))
            .Select(ProjectFact).ToListAsync(cancellationToken);
        var existing = projects.Select(item => item.Id).ToHashSet();
        var linksInScope = incoming.Where(link => existing.Contains(link.ParentProjectId) && existing.Contains(link.ChildProjectId)).ToArray();
        var phases = await context.Set<ProjectPhase>().AsNoTracking().Where(item => item.ProjectId == projectId)
            .OrderBy(item => item.OrderIndex).ThenBy(item => item.Id)
            .Select(item => new ProjectStructurePhaseFact(item.Id, item.ProjectId, item.Name, item.Goal,
                item.Status, item.OrderIndex, item.StartDateUtc, item.EndDateUtc))
            .Take(ProjectStructureProjectionQueryLimits.MaximumPhases + 1).ToListAsync(cancellationToken);
        RequireLimit(phases.Count, ProjectStructureProjectionQueryLimits.MaximumPhases, "phases");
        return new ProjectStructureProjectFacts(project, projects, linksInScope, phases);
    }

    private static void RequireLimit(int count, int maximum, string dimension) {
        if (count > maximum) {
            throw new ProjectStructureProjectionLimitException(dimension, maximum);
        }
    }
}
