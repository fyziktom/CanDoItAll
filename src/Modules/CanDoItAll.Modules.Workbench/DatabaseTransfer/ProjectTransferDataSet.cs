using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectTransferRecordCounts(
    int Projects,
    int Phases,
    int Options,
    int HierarchyLinks,
    int Objects,
    int ObjectLinks,
    int ProjectionLayouts,
    int NodeBindings,
    int NodeReferences,
    int NodeLifecycleEvents,
    int CrossModuleMutations,
    int ViewStates)
{
    public int Total =>
        Projects +
        Phases +
        Options +
        HierarchyLinks +
        Objects +
        ObjectLinks +
        ProjectionLayouts +
        NodeBindings +
        NodeReferences +
        NodeLifecycleEvents +
        CrossModuleMutations +
        ViewStates;
}

internal sealed class ProjectTransferDataSet
{
    public List<Project> Projects { get; set; } = [];

    public List<ProjectPhase> Phases { get; set; } = [];

    public List<ProjectOptionSelection> Options { get; set; } = [];

    public List<ProjectHierarchyLink> HierarchyLinks { get; set; } = [];

    public List<ProjectObjectRecord> Objects { get; set; } = [];

    public List<ProjectObjectLinkRecord> ObjectLinks { get; set; } = [];

    public List<ProjectStructureProjectionLayoutRecord> ProjectionLayouts { get; set; } = [];

    public List<ProjectNodeBindingRecord> NodeBindings { get; set; } = [];

    public List<ProjectNodeReferenceRecord> NodeReferences { get; set; } = [];

    public List<ProjectNodeLifecycleEventRecord> NodeLifecycleEvents { get; set; } = [];

    public List<ProjectCrossModuleMutationRecord> CrossModuleMutations { get; set; } = [];

    public List<ProjectWorkbenchViewStateRecord> ViewStates { get; set; } = [];

    public ProjectTransferRecordCounts Counts => new(
        Projects.Count,
        Phases.Count,
        Options.Count,
        HierarchyLinks.Count,
        Objects.Count,
        ObjectLinks.Count,
        ProjectionLayouts.Count,
        NodeBindings.Count,
        NodeReferences.Count,
        NodeLifecycleEvents.Count,
        CrossModuleMutations.Count,
        ViewStates.Count);

    public static async Task EnsureSchemasAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await ProjectsSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
    }

    public static async Task<ProjectTransferRecordCounts> CountAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureSchemasAsync(dbContext, cancellationToken);

        return new ProjectTransferRecordCounts(
            await dbContext.Set<Project>().CountAsync(cancellationToken),
            await dbContext.Set<ProjectPhase>().CountAsync(cancellationToken),
            await dbContext.Set<ProjectOptionSelection>().CountAsync(cancellationToken),
            await dbContext.Set<ProjectHierarchyLink>().CountAsync(cancellationToken),
            await dbContext.Set<ProjectObjectRecord>().CountAsync(cancellationToken),
            await dbContext.Set<ProjectObjectLinkRecord>().CountAsync(cancellationToken),
            await dbContext.Set<ProjectStructureProjectionLayoutRecord>().CountAsync(cancellationToken),
            await dbContext.Set<ProjectNodeBindingRecord>().CountAsync(cancellationToken),
            await dbContext.Set<ProjectNodeReferenceRecord>().CountAsync(cancellationToken),
            await dbContext.Set<ProjectNodeLifecycleEventRecord>().CountAsync(cancellationToken),
            await dbContext.Set<ProjectCrossModuleMutationRecord>().CountAsync(cancellationToken),
            await dbContext.Set<ProjectWorkbenchViewStateRecord>().CountAsync(cancellationToken));
    }

    public static async Task<ProjectTransferDataSet> LoadAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureSchemasAsync(dbContext, cancellationToken);

        return new ProjectTransferDataSet
        {
            Projects = await LoadTableAsync<Project>(dbContext, cancellationToken),
            Phases = await LoadTableAsync<ProjectPhase>(dbContext, cancellationToken),
            Options = await LoadTableAsync<ProjectOptionSelection>(dbContext, cancellationToken),
            HierarchyLinks = await LoadTableAsync<ProjectHierarchyLink>(dbContext, cancellationToken),
            Objects = await LoadTableAsync<ProjectObjectRecord>(dbContext, cancellationToken),
            ObjectLinks = await LoadTableAsync<ProjectObjectLinkRecord>(dbContext, cancellationToken),
            ProjectionLayouts = await LoadTableAsync<ProjectStructureProjectionLayoutRecord>(dbContext, cancellationToken),
            NodeBindings = await LoadTableAsync<ProjectNodeBindingRecord>(dbContext, cancellationToken),
            NodeReferences = await LoadTableAsync<ProjectNodeReferenceRecord>(dbContext, cancellationToken),
            NodeLifecycleEvents = await LoadTableAsync<ProjectNodeLifecycleEventRecord>(dbContext, cancellationToken),
            CrossModuleMutations = await LoadTableAsync<ProjectCrossModuleMutationRecord>(dbContext, cancellationToken),
            ViewStates = await LoadTableAsync<ProjectWorkbenchViewStateRecord>(dbContext, cancellationToken)
        };
    }

    public static async Task ClearAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureSchemasAsync(dbContext, cancellationToken);

        await RemoveAndSaveAsync<ProjectNodeReferenceRecord>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<ProjectNodeBindingRecord>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<ProjectNodeLifecycleEventRecord>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<ProjectObjectLinkRecord>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<ProjectStructureProjectionLayoutRecord>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<ProjectCrossModuleMutationRecord>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<ProjectWorkbenchViewStateRecord>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<ProjectObjectRecord>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<ProjectHierarchyLink>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<ProjectOptionSelection>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<ProjectPhase>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<Project>(dbContext, cancellationToken);
    }

    public static async Task SaveAsync(
        AppDbContext dbContext,
        ProjectTransferDataSet dataSet,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSet);

        await EnsureSchemasAsync(dbContext, cancellationToken);

        await AddAndSaveAsync(dbContext, dataSet.Projects, cancellationToken);
        await AddAndSaveAsync(dbContext, dataSet.Phases, cancellationToken);
        await AddAndSaveAsync(dbContext, dataSet.Options, cancellationToken);
        await AddAndSaveAsync(dbContext, dataSet.HierarchyLinks, cancellationToken);
        await AddAndSaveAsync(dbContext, dataSet.Objects, cancellationToken);
        await AddAndSaveAsync(dbContext, dataSet.ObjectLinks, cancellationToken);
        await AddAndSaveAsync(dbContext, dataSet.ProjectionLayouts, cancellationToken);
        await AddAndSaveAsync(dbContext, dataSet.NodeBindings, cancellationToken);
        await AddAndSaveAsync(dbContext, dataSet.NodeReferences, cancellationToken);
        await AddAndSaveAsync(dbContext, dataSet.NodeLifecycleEvents, cancellationToken);
        await AddAndSaveAsync(dbContext, dataSet.CrossModuleMutations, cancellationToken);
        await AddAndSaveAsync(dbContext, dataSet.ViewStates, cancellationToken);
    }

    private static Task<List<T>> LoadTableAsync<T>(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
        where T : class
    {
        return dbContext.Set<T>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    private static async Task AddAndSaveAsync<T>(
        AppDbContext dbContext,
        IReadOnlyCollection<T> entities,
        CancellationToken cancellationToken)
        where T : class
    {
        if (entities.Count == 0)
        {
            return;
        }

        await dbContext.Set<T>().AddRangeAsync(entities, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task RemoveAndSaveAsync<T>(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
        where T : class
    {
        var entities = await dbContext.Set<T>().ToListAsync(cancellationToken);
        if (entities.Count == 0)
        {
            return;
        }

        dbContext.RemoveRange(entities);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
