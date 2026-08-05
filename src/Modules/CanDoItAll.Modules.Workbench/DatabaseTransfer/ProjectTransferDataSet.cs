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

    public bool HasStorageBindings => NodeBindings.Any(binding =>
        !string.IsNullOrWhiteSpace(binding.MediaRelativePath) ||
        !string.IsNullOrWhiteSpace(binding.StorageObjectReferenceJson));

    public bool HasCrossModuleMutations => CrossModuleMutations.Count > 0;

    public void PrepareForPackageExport()
    {
        if (CrossModuleMutations.Any(mutation =>
                mutation.Status != ProjectCrossModuleMutationStatus.Completed))
        {
            throw new InvalidDataException(
                "Project package export cannot capture pending or failed cross-module recovery work. Complete or resolve it before exporting.");
        }

        CrossModuleMutations.Clear();
    }

    public void ValidatePackageImportSafety()
    {
        if (CrossModuleMutations.Count > 0)
        {
            throw new InvalidDataException(
                "Project package v2 cannot import executable cross-module mutation records.");
        }
    }

    public void ValidateForImport()
    {
        ValidateUniqueIds(Projects, item => item.Id, "project");
        ValidateUniqueIds(Phases, item => item.Id, "project phase");
        ValidateUniqueIds(Options, item => item.Id, "project option");
        ValidateUniqueIds(HierarchyLinks, item => item.Id, "project hierarchy link");
        ValidateUniqueIds(Objects, item => item.Id, "project object");
        ValidateUniqueIds(ObjectLinks, item => item.Id, "project object link");
        ValidateUniqueIds(ProjectionLayouts, item => item.Id, "project projection layout");
        ValidateUniqueIds(NodeBindings, item => item.Id, "project node binding");
        ValidateUniqueIds(NodeReferences, item => item.Id, "project node reference");
        ValidateUniqueIds(NodeLifecycleEvents, item => item.Id, "project node lifecycle event");
        ValidateUniqueIds(CrossModuleMutations, item => item.Id, "project cross-module mutation");
        ValidateUniqueIds(ViewStates, item => item.Id, "project view state");

        var projectIds = Projects.Select(item => item.Id).ToHashSet();
        ValidateProjectReferences(Phases, item => item.ProjectId, projectIds, "project phase");
        ValidateProjectReferences(Options, item => item.ProjectId, projectIds, "project option");
        ValidateProjectReferences(Objects, item => item.ProjectId, projectIds, "project object");
        ValidateProjectReferences(ObjectLinks, item => item.ProjectId, projectIds, "project object link");
        ValidateProjectReferences(ProjectionLayouts, item => item.ProjectId, projectIds, "project projection layout");
        ValidateProjectReferences(NodeLifecycleEvents, item => item.ProjectId, projectIds, "project node lifecycle event");
        ValidateProjectReferences(CrossModuleMutations, item => item.ProjectId, projectIds, "project cross-module mutation");
        ValidateProjectReferences(ViewStates, item => item.ProjectId, projectIds, "project view state");

        var hierarchyEdges = new HashSet<(Guid ParentId, Guid ChildId)>();
        foreach (var hierarchyLink in HierarchyLinks)
        {
            if (!projectIds.Contains(hierarchyLink.ParentProjectId) ||
                !projectIds.Contains(hierarchyLink.ChildProjectId) ||
                hierarchyLink.ParentProjectId == hierarchyLink.ChildProjectId ||
                !hierarchyEdges.Add((
                    hierarchyLink.ParentProjectId,
                    hierarchyLink.ChildProjectId)))
            {
                throw InvalidReference("project hierarchy link", hierarchyLink.Id);
            }
        }
        ValidateProjectHierarchyIsAcyclic(projectIds);

        var objectsById = Objects.ToDictionary(item => item.Id);
        var objectKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var projectObject in Objects)
        {
            if (string.IsNullOrWhiteSpace(projectObject.NodeKey) ||
                !objectKeys.Add(ToNodeIdentity(
                    projectObject.ProjectId,
                    projectObject.NodeKey)))
            {
                throw new InvalidDataException(
                    $"Project package object '{projectObject.Id:D}' has an empty or duplicate node key.");
            }
        }

        foreach (var projectObject in Objects.Where(item => !string.IsNullOrWhiteSpace(item.ParentNodeKey)))
        {
            if (!IsCanonicalProjectRoot(projectObject.ProjectId, projectObject.ParentNodeKey!) &&
                !objectKeys.Contains(ToNodeIdentity(
                    projectObject.ProjectId,
                    projectObject.ParentNodeKey!)))
            {
                throw InvalidReference("project object parent", projectObject.Id);
            }
        }
        ValidateNodeHierarchyIsAcyclic();

        foreach (var objectLink in ObjectLinks)
        {
            if (!objectKeys.Contains(ToNodeIdentity(
                    objectLink.ProjectId,
                    objectLink.SourceNodeKey)) ||
                !objectKeys.Contains(ToNodeIdentity(
                    objectLink.ProjectId,
                    objectLink.TargetNodeKey)) ||
                string.Equals(
                    objectLink.SourceNodeKey,
                    objectLink.TargetNodeKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidReference("project object link", objectLink.Id);
            }
        }

        foreach (var layout in ProjectionLayouts)
        {
            if (!objectKeys.Contains(ToNodeIdentity(
                    layout.ProjectId,
                    layout.NodeKey)))
            {
                throw InvalidReference("project projection layout", layout.Id);
            }
        }

        ValidateObjectReferences(NodeBindings, item => item.ProjectObjectId, objectsById, "project node binding");
        ValidateObjectReferences(NodeReferences, item => item.ProjectObjectId, objectsById, "project node reference");
        ValidateObjectReferences(NodeLifecycleEvents, item => item.ProjectObjectId, objectsById, "project node lifecycle event");

        if (NodeBindings.Select(item => item.ProjectObjectId).Distinct().Count() != NodeBindings.Count)
        {
            throw new InvalidDataException("Project package contains duplicate node bindings for one project object.");
        }
    }

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

    private static void ValidateUniqueIds<T>(
        IReadOnlyCollection<T> rows,
        Func<T, Guid> idSelector,
        string label)
    {
        var ids = new HashSet<Guid>();
        foreach (var row in rows)
        {
            var id = idSelector(row);
            if (id == Guid.Empty || !ids.Add(id))
            {
                throw new InvalidDataException(
                    $"Project package contains an empty or duplicate {label} id.");
            }
        }
    }

    private static void ValidateProjectReferences<T>(
        IReadOnlyCollection<T> rows,
        Func<T, Guid> projectIdSelector,
        IReadOnlySet<Guid> projectIds,
        string label)
    {
        foreach (var row in rows)
        {
            var projectId = projectIdSelector(row);
            if (!projectIds.Contains(projectId))
            {
                throw new InvalidDataException(
                    $"Project package {label} references missing project '{projectId:D}'.");
            }
        }
    }

    private static void ValidateObjectReferences<T>(
        IReadOnlyCollection<T> rows,
        Func<T, Guid> objectIdSelector,
        IReadOnlyDictionary<Guid, ProjectObjectRecord> objectsById,
        string label)
    {
        foreach (var row in rows)
        {
            var objectId = objectIdSelector(row);
            if (!objectsById.ContainsKey(objectId))
            {
                throw new InvalidDataException(
                    $"Project package {label} references missing project object '{objectId:D}'.");
            }
        }
    }

    private static InvalidDataException InvalidReference(string label, Guid id)
        => new($"Project package {label} '{id:D}' contains a dangling reference.");

    private static string ToNodeIdentity(Guid projectId, string nodeKey)
        => $"{projectId:N}\0{nodeKey}";

    private static bool IsCanonicalProjectRoot(Guid projectId, string nodeKey)
        => string.Equals(
            nodeKey,
            ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId),
            StringComparison.OrdinalIgnoreCase);

    private void ValidateProjectHierarchyIsAcyclic(IReadOnlySet<Guid> projectIds)
    {
        var childrenByParent = projectIds.ToDictionary(
            projectId => projectId,
            _ => new List<Guid>());
        var incomingEdges = projectIds.ToDictionary(
            projectId => projectId,
            _ => 0);
        foreach (var link in HierarchyLinks)
        {
            childrenByParent[link.ParentProjectId].Add(link.ChildProjectId);
            incomingEdges[link.ChildProjectId]++;
        }

        var ready = new Queue<Guid>(incomingEdges
            .Where(item => item.Value == 0)
            .Select(item => item.Key));
        var visited = 0;
        while (ready.TryDequeue(out var projectId))
        {
            visited++;
            foreach (var childProjectId in childrenByParent[projectId])
            {
                incomingEdges[childProjectId]--;
                if (incomingEdges[childProjectId] == 0)
                {
                    ready.Enqueue(childProjectId);
                }
            }
        }

        if (visited != projectIds.Count)
        {
            throw new InvalidDataException(
                "Project package hierarchy contains a cycle.");
        }
    }

    private void ValidateNodeHierarchyIsAcyclic()
    {
        foreach (var projectObjects in Objects.GroupBy(item => item.ProjectId))
        {
            var parentByNode = projectObjects.ToDictionary(
                item => item.NodeKey,
                item => item.ParentNodeKey,
                StringComparer.OrdinalIgnoreCase);
            var completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var nodeKey in parentByNode.Keys)
            {
                if (completed.Contains(nodeKey))
                {
                    continue;
                }

                var currentPath = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var visitedPath = new List<string>();
                string? currentNodeKey = nodeKey;
                while (!string.IsNullOrWhiteSpace(currentNodeKey) &&
                       !IsCanonicalProjectRoot(projectObjects.Key, currentNodeKey) &&
                       !completed.Contains(currentNodeKey))
                {
                    if (!currentPath.Add(currentNodeKey))
                    {
                        throw new InvalidDataException(
                            "Project package node parent graph contains a cycle.");
                    }

                    visitedPath.Add(currentNodeKey);
                    currentNodeKey = parentByNode[currentNodeKey];
                }

                completed.UnionWith(visitedPath);
            }
        }
    }
}
