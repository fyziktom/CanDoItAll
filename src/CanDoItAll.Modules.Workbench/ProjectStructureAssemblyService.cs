using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Validation;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureProjectionLayoutRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string NodeKey { get; set; } = string.Empty;
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class ProjectStructureProjectionLayoutRecordConfiguration : IEntityTypeConfiguration<ProjectStructureProjectionLayoutRecord>
{
    public void Configure(EntityTypeBuilder<ProjectStructureProjectionLayoutRecord> builder)
    {
        builder.ToTable("Workbench_ProjectProjectionLayouts");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.NodeKey).HasMaxLength(160).IsRequired();
        builder.HasIndex(item => new { item.ProjectId, item.NodeKey }).IsUnique();
    }
}

public sealed record ProjectStructureAssemblySnapshot(
    IReadOnlyList<ProjectObjectRecord> Nodes,
    IReadOnlyList<ProjectObjectLinkRecord> Links);

public interface IProjectStructureProjectionContributor
{
    Task ContributeAsync(ProjectStructureProjectionContext context, CancellationToken cancellationToken);
}

public sealed class ProjectStructureProjectionContext(
    AppDbContext dbContext,
    Guid projectId,
    DateTimeOffset assembledAtUtc,
    IReadOnlyDictionary<string, ProjectStructureProjectionLayoutRecord> layoutOverrides)
{
    private readonly Dictionary<string, ProjectObjectRecord> _nodesByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProjectObjectLinkRecord> _linksByKey = new(StringComparer.Ordinal);

    public AppDbContext DbContext => dbContext;

    public Guid ProjectId => projectId;

    public DateTimeOffset AssembledAtUtc => assembledAtUtc;

    public IReadOnlyList<ProjectObjectRecord> Nodes => _nodesByKey.Values.ToList();

    public IReadOnlyList<ProjectObjectLinkRecord> Links => _linksByKey.Values.ToList();

    public void AddNode(ProjectObjectRecord node)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(node.NodeKey);

        node.ProjectId = projectId;
        node.IsSystemManaged = true;
        node.MetadataJson = string.IsNullOrWhiteSpace(node.MetadataJson) ? "{}" : node.MetadataJson;
        node.ProgressMode ??= string.Empty;
        node.ObjectSubtype ??= string.Empty;
        node.Title ??= string.Empty;
        node.Subtitle ??= string.Empty;
        node.Status ??= string.Empty;
        node.Notes ??= string.Empty;
        node.Binding = ProjectNodeBindingStorage.ResolveForRuntime(node);
        var normalizedMarkers = ProjectNodeMarkerState.Parse(node.MarkersJson);
        node.MarkersJson = normalizedMarkers.Count > 0
            ? ProjectNodeMarkerState.Serialize(normalizedMarkers)
            : ProjectNodeLegacyMetadata.ReadLegacyMarkers(node.MetadataJson) is { Count: > 0 } legacyMarkers
                ? ProjectNodeMarkerState.Serialize(legacyMarkers)
                : "[]";

        if (layoutOverrides.TryGetValue(node.NodeKey, out var layout))
        {
            node.PositionX = layout.PositionX;
            node.PositionY = layout.PositionY;
            node.UpdatedAtUtc = layout.UpdatedAtUtc > node.UpdatedAtUtc
                ? layout.UpdatedAtUtc
                : node.UpdatedAtUtc;
        }

        _nodesByKey[node.NodeKey] = node;
    }

    public void AddLink(string sourceNodeKey, string targetNodeKey, ProjectObjectLinkKind linkKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceNodeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetNodeKey);

        var key = $"{sourceNodeKey}|{targetNodeKey}|{(int)linkKind}";
        if (_linksByKey.ContainsKey(key))
        {
            return;
        }

        _linksByKey[key] = new ProjectObjectLinkRecord
        {
            ProjectId = projectId,
            SourceNodeKey = sourceNodeKey,
            TargetNodeKey = targetNodeKey,
            LinkKind = linkKind,
            IsSystemManaged = true,
            CreatedAtUtc = assembledAtUtc
        };
    }

    public bool ContainsNode(string nodeKey)
    {
        return _nodesByKey.ContainsKey(nodeKey);
    }
}

public sealed class ProjectStructureAssemblyService(
    IEnumerable<IProjectStructureProjectionContributor> projectionContributors,
    IClock clock)
{
    private static readonly TimeSpan[] SqliteBusyRetryDelays =
    [
        TimeSpan.FromMilliseconds(40),
        TimeSpan.FromMilliseconds(90),
        TimeSpan.FromMilliseconds(180)
    ];

    private readonly IReadOnlyList<IProjectStructureProjectionContributor> _projectionContributors = projectionContributors.ToList();

    public async Task<ProjectStructureAssemblySnapshot> LoadAsync(
        AppDbContext dbContext,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var canonicalNodes = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId && !item.IsSystemManaged)
            .ToListAsync(cancellationToken);
        foreach (var canonicalNode in canonicalNodes)
        {
            canonicalNode.MarkersJson = NormalizeMarkersJson(canonicalNode.MarkersJson, canonicalNode.MetadataJson);
        }
        await ProjectNodeBindingStorage.LoadAsync(dbContext, canonicalNodes, cancellationToken);
        foreach (var canonicalNode in canonicalNodes)
        {
            dbContext.Entry(canonicalNode).State = EntityState.Unchanged;
        }
        var persistedUserLinks = await dbContext.Set<ProjectObjectLinkRecord>()
            .Where(item => item.ProjectId == projectId && !item.IsSystemManaged)
            .ToListAsync(cancellationToken);
        var layoutOverrides = await dbContext.Set<ProjectStructureProjectionLayoutRecord>()
            .Where(item => item.ProjectId == projectId)
            .ToDictionaryAsync(item => item.NodeKey, StringComparer.Ordinal, cancellationToken);

        var context = new ProjectStructureProjectionContext(
            dbContext,
            projectId,
            clock.GetUtcNow(),
            layoutOverrides);

        foreach (var contributor in _projectionContributors)
        {
            await contributor.ContributeAsync(context, cancellationToken);
        }

        return new ProjectStructureAssemblySnapshot(
            canonicalNodes
                .Concat(context.Nodes)
                .OrderBy(item => item.PositionY)
                .ThenBy(item => item.PositionX)
                .ToList(),
            FilterLegacyCanonicalHierarchyLinks(persistedUserLinks, canonicalNodes)
                .Concat(BuildCanonicalHierarchyLinks(projectId, canonicalNodes, context.AssembledAtUtc))
                .Concat(context.Links)
                .OrderBy(item => item.SourceNodeKey)
                .ThenBy(item => item.TargetNodeKey)
                .ToList());
    }

    public async Task<ProjectObjectRecord?> FindNodeAsync(
        AppDbContext dbContext,
        Guid projectId,
        string nodeKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodeKey))
        {
            return null;
        }

        var snapshot = await LoadAsync(dbContext, projectId, cancellationToken);
        return snapshot.Nodes.FirstOrDefault(item => string.Equals(item.NodeKey, nodeKey, StringComparison.Ordinal));
    }

    public async Task<IReadOnlyList<string>> UpdatePositionsAsync(
        AppDbContext dbContext,
        Guid projectId,
        IReadOnlyCollection<ProjectNodeMoveRequest> positions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(positions);

        if (positions.Count == 0)
        {
            return [];
        }

        var requestedPositions = positions
            .Where(position => !string.IsNullOrWhiteSpace(position.NodeId))
            .GroupBy(position => position.NodeId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToList();
        if (requestedPositions.Count == 0)
        {
            return [];
        }

        var validProjectionNodeKeys = await LoadProjectionNodeKeysAsync(dbContext, projectId, cancellationToken);
        var nodeKeys = requestedPositions
            .Select(item => item.NodeId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var canonicalNodes = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId && nodeKeys.Contains(item.NodeKey))
            .ToDictionaryAsync(item => item.NodeKey, StringComparer.Ordinal, cancellationToken);
        var existingLayouts = await dbContext.Set<ProjectStructureProjectionLayoutRecord>()
            .Where(item => item.ProjectId == projectId && nodeKeys.Contains(item.NodeKey))
            .ToDictionaryAsync(item => item.NodeKey, StringComparer.Ordinal, cancellationToken);

        var updatedNodeIds = new List<string>(requestedPositions.Count);
        var updatedAtUtc = clock.GetUtcNow();

        foreach (var position in requestedPositions)
        {
            if (canonicalNodes.TryGetValue(position.NodeId, out var canonicalNode))
            {
                if (Math.Abs(canonicalNode.PositionX - position.X) < 0.5d &&
                    Math.Abs(canonicalNode.PositionY - position.Y) < 0.5d)
                {
                    continue;
                }

                canonicalNode.PositionX = position.X;
                canonicalNode.PositionY = position.Y;
                canonicalNode.UpdatedAtUtc = updatedAtUtc;
                updatedNodeIds.Add(canonicalNode.NodeKey);
                continue;
            }

            if (!validProjectionNodeKeys.Contains(position.NodeId))
            {
                continue;
            }

            if (existingLayouts.TryGetValue(position.NodeId, out var layout))
            {
                if (Math.Abs(layout.PositionX - position.X) < 0.5d &&
                    Math.Abs(layout.PositionY - position.Y) < 0.5d)
                {
                    continue;
                }

                layout.PositionX = position.X;
                layout.PositionY = position.Y;
                layout.UpdatedAtUtc = updatedAtUtc;
                updatedNodeIds.Add(position.NodeId);
                continue;
            }

            await dbContext.Set<ProjectStructureProjectionLayoutRecord>().AddAsync(
                new ProjectStructureProjectionLayoutRecord
                {
                    ProjectId = projectId,
                    NodeKey = position.NodeId,
                    PositionX = position.X,
                    PositionY = position.Y,
                    UpdatedAtUtc = updatedAtUtc
                },
                cancellationToken);
            updatedNodeIds.Add(position.NodeId);
        }

        if (updatedNodeIds.Count > 0)
        {
            await SaveChangesAsync(dbContext, cancellationToken);
        }

        return updatedNodeIds;
    }

    internal static bool IsSqliteBusy(Exception exception)
        => SqliteWriteCoordination.IsBusy(exception);

    private static async Task SaveChangesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException ex) when (IsSqliteBusy(ex) && attempt < SqliteBusyRetryDelays.Length)
            {
                await Task.Delay(SqliteBusyRetryDelays[attempt], cancellationToken);
            }
        }
    }

    private async Task<HashSet<string>> LoadProjectionNodeKeysAsync(
        AppDbContext dbContext,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var context = new ProjectStructureProjectionContext(
            dbContext,
            projectId,
            clock.GetUtcNow(),
            new Dictionary<string, ProjectStructureProjectionLayoutRecord>(StringComparer.Ordinal));

        foreach (var contributor in _projectionContributors)
        {
            await contributor.ContributeAsync(context, cancellationToken);
        }

        return context.Nodes
            .Select(item => item.NodeKey)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlyList<ProjectObjectLinkRecord> FilterLegacyCanonicalHierarchyLinks(
        IReadOnlyList<ProjectObjectLinkRecord> persistedUserLinks,
        IReadOnlyCollection<ProjectObjectRecord> canonicalNodes)
    {
        var canonicalNodeKeys = canonicalNodes
            .Select(item => item.NodeKey)
            .ToHashSet(StringComparer.Ordinal);

        return persistedUserLinks
            .Where(item =>
                !(canonicalNodeKeys.Contains(item.TargetNodeKey) &&
                  (item.LinkKind == ProjectObjectLinkKind.Contains || item.LinkKind == ProjectObjectLinkKind.BelongsTo)))
            .ToList();
    }

    private static IReadOnlyList<ProjectObjectLinkRecord> BuildCanonicalHierarchyLinks(
        Guid projectId,
        IReadOnlyCollection<ProjectObjectRecord> canonicalNodes,
        DateTimeOffset assembledAtUtc)
    {
        return canonicalNodes
            .Where(item => !string.IsNullOrWhiteSpace(item.ParentNodeKey))
            .Select(item => new ProjectObjectLinkRecord
            {
                ProjectId = projectId,
                SourceNodeKey = item.ParentNodeKey!,
                TargetNodeKey = item.NodeKey,
                LinkKind = ProjectWorkbenchGraphConventions.ResolveHierarchyLinkKind(projectId, item.ParentNodeKey!),
                IsSystemManaged = true,
                CreatedAtUtc = item.UpdatedAtUtc == default
                    ? assembledAtUtc
                    : item.UpdatedAtUtc
            })
            .ToList();
    }

    private static string NormalizeMarkersJson(string? markersJson, string? metadataJson)
    {
        var normalizedMarkers = ProjectNodeMarkerState.Parse(markersJson);
        if (normalizedMarkers.Count > 0)
        {
            return ProjectNodeMarkerState.Serialize(normalizedMarkers);
        }

        var legacyMarkers = ProjectNodeLegacyMetadata.ReadLegacyMarkers(metadataJson);
        return NormalizeLegacyMarkers(metadataJson);
    }

    private static string NormalizeLegacyMarkers(string? metadataJson)
    {
        var legacyMarkers = ProjectNodeLegacyMetadata.ReadLegacyMarkers(metadataJson);
        return legacyMarkers.Count == 0
            ? "[]"
            : ProjectNodeMarkerState.Serialize(legacyMarkers);
    }
}

internal static class ProjectStructureProjectionBindingFactory
{
    public static ProjectNodeBindingState Create(string route, string externalArtifactKind, Guid? externalArtifactId)
    {
        return new ProjectNodeBindingState(
            route,
            externalArtifactKind,
            externalArtifactId,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);
    }
}

internal sealed class ProjectHierarchyProjectionContributor(IClock clock) : IProjectStructureProjectionContributor
{
    public async Task ContributeAsync(ProjectStructureProjectionContext context, CancellationToken cancellationToken)
    {
        var project = await context.DbContext.Set<Project>()
            .FirstAsync(item => item.Id == context.ProjectId, cancellationToken);
        var phases = await context.DbContext.Set<ProjectPhase>()
            .Where(item => item.ProjectId == context.ProjectId)
            .OrderBy(item => item.OrderIndex)
            .ToListAsync(cancellationToken);
        var allProjects = await context.DbContext.Set<Project>().ToListAsync(cancellationToken);
        var allHierarchyLinks = await context.DbContext.Set<ProjectHierarchyLink>().ToListAsync(cancellationToken);
        var projection = BuildProjectHierarchyProjection(project, allProjects, allHierarchyLinks, clock.GetUtcNow());

        context.AddNode(new ProjectObjectRecord
        {
            ProjectId = context.ProjectId,
            NodeKey = BuildProjectRootNodeKey(project.Id),
            ObjectType = ProjectObjectType.ProjectRoot,
            Title = project.Name,
            Subtitle = project.Objective,
            Status = project.Status.ToString(),
            Notes = project.Description,
            Binding = ProjectStructureProjectionBindingFactory.Create($"/projects?projectId={project.Id}", "project", project.Id),
            PositionX = 140,
            PositionY = 240,
            CreatedAtUtc = project.CreatedAtUtc,
            UpdatedAtUtc = context.AssembledAtUtc
        });

        foreach (var parentNode in projection.ParentNodes)
        {
            context.AddNode(parentNode);
        }

        foreach (var descendantNode in projection.DescendantNodes)
        {
            context.AddNode(descendantNode);
        }

        foreach (var phase in phases.Select((phase, index) => new { Phase = phase, Index = index }))
        {
            var startUtc = phase.Phase.StartDateUtc.HasValue
                ? (DateTimeOffset?)new DateTimeOffset(DateTime.SpecifyKind(phase.Phase.StartDateUtc.Value, DateTimeKind.Utc))
                : null;
            var endUtc = phase.Phase.EndDateUtc.HasValue
                ? (DateTimeOffset?)new DateTimeOffset(DateTime.SpecifyKind(phase.Phase.EndDateUtc.Value, DateTimeKind.Utc))
                : null;
            context.AddNode(new ProjectObjectRecord
            {
                ProjectId = context.ProjectId,
                NodeKey = $"phase:{phase.Phase.Id}",
                ObjectType = ProjectObjectType.Phase,
                Title = phase.Phase.Name,
                Subtitle = phase.Phase.Goal,
                Status = phase.Phase.Status.ToString(),
                Notes = phase.Phase.Goal,
                Binding = ProjectStructureProjectionBindingFactory.Create($"/projects?projectId={context.ProjectId}", "phase", phase.Phase.Id),
                ParentNodeKey = BuildProjectRootNodeKey(project.Id),
                PositionX = 420,
                PositionY = 120 + (phase.Index * 180),
                StartUtc = startUtc,
                EndUtc = endUtc,
                DurationSeconds = NormalizeDurationSeconds(startUtc, endUtc),
                CreatedAtUtc = project.CreatedAtUtc,
                UpdatedAtUtc = context.AssembledAtUtc
            });
        }

        foreach (var link in projection.Links)
        {
            context.AddLink(link.Source, link.Target, link.Kind);
        }

        foreach (var phase in phases)
        {
            context.AddLink(BuildProjectRootNodeKey(project.Id), $"phase:{phase.Id}", ProjectObjectLinkKind.Contains);
        }
    }

    private static string BuildProjectRootNodeKey(Guid projectId)
    {
        return $"project:{projectId}";
    }

    private static string BuildProjectChildNodeKey(Guid projectId)
    {
        return $"project-child:{projectId}";
    }

    private static string BuildRelatedParentNodeKey(Guid projectId)
    {
        return $"project-related-parent:{projectId}";
    }

    private static string ResolveRelatedProjectSubtitle(Project project, string fallbackLabel)
    {
        return string.IsNullOrWhiteSpace(project.CurrentPhase)
            ? fallbackLabel
            : $"{fallbackLabel} · {project.CurrentPhase}";
    }

    private static string ResolveRelatedProjectNotes(Project project, string fallbackLabel)
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                project.Description?.Trim(),
                string.IsNullOrWhiteSpace(project.Objective) ? null : $"Objective: {project.Objective.Trim()}",
                $"Role: {fallbackLabel}"
            }.Where(item => !string.IsNullOrWhiteSpace(item)));
    }

    private static string ResolveProjectSortKey(Project project)
    {
        return string.IsNullOrWhiteSpace(project.Name)
            ? project.Id.ToString("N")
            : project.Name.Trim();
    }

    private static IReadOnlySet<Guid> CollectDescendantProjectIds(
        Guid projectId,
        IReadOnlyDictionary<Guid, List<Guid>> childProjectIdsByParent)
    {
        var descendantProjectIds = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(projectId);

        while (queue.Count > 0)
        {
            var currentProjectId = queue.Dequeue();
            if (!childProjectIdsByParent.TryGetValue(currentProjectId, out var childProjectIds))
            {
                continue;
            }

            foreach (var childProjectId in childProjectIds)
            {
                if (descendantProjectIds.Add(childProjectId))
                {
                    queue.Enqueue(childProjectId);
                }
            }
        }

        return descendantProjectIds;
    }

    private static string ResolveProjectHierarchyLinkSourceNodeKey(
        Guid parentProjectId,
        Guid rootProjectId,
        IReadOnlySet<Guid> visibleDescendantProjectIds)
    {
        return parentProjectId == rootProjectId
            ? BuildProjectRootNodeKey(rootProjectId)
            : visibleDescendantProjectIds.Contains(parentProjectId)
                ? BuildProjectChildNodeKey(parentProjectId)
                : BuildRelatedParentNodeKey(parentProjectId);
    }

    private static ProjectObjectRecord CreateRelatedParentNode(
        Guid projectId,
        Project parentProject,
        string fallbackLabel,
        double x,
        double y,
        DateTimeOffset updatedAtUtc)
    {
        return new ProjectObjectRecord
        {
            ProjectId = projectId,
            NodeKey = BuildRelatedParentNodeKey(parentProject.Id),
            ObjectType = ProjectObjectType.ProjectRoot,
            Title = parentProject.Name,
            Subtitle = ResolveRelatedProjectSubtitle(parentProject, fallbackLabel),
            Status = parentProject.Status.ToString(),
            Notes = ResolveRelatedProjectNotes(parentProject, fallbackLabel),
            Binding = ProjectStructureProjectionBindingFactory.Create($"/projects/{parentProject.Id}/structure", "project", parentProject.Id),
            PositionX = x,
            PositionY = y,
            CreatedAtUtc = parentProject.CreatedAtUtc,
            UpdatedAtUtc = updatedAtUtc,
            IsSystemManaged = true
        };
    }

    private static ProjectHierarchyProjection BuildProjectHierarchyProjection(
        Project project,
        IReadOnlyList<Project> allProjects,
        IReadOnlyList<ProjectHierarchyLink> allHierarchyLinks,
        DateTimeOffset updatedAtUtc)
    {
        var projectMap = allProjects.ToDictionary(item => item.Id);
        if (!projectMap.ContainsKey(project.Id))
        {
            return new ProjectHierarchyProjection([], [], []);
        }

        var childProjectIdsByParent = allHierarchyLinks
            .GroupBy(item => item.ParentProjectId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(item => item.ChildProjectId)
                    .Distinct()
                    .OrderBy(item => ResolveProjectSortKey(projectMap[item]), StringComparer.OrdinalIgnoreCase)
                    .ToList());
        var parentProjectIdsByChild = allHierarchyLinks
            .GroupBy(item => item.ChildProjectId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(item => item.ParentProjectId)
                    .Distinct()
                    .ToList());

        var descendantProjectIds = CollectDescendantProjectIds(project.Id, childProjectIdsByParent);
        var primaryParentByProjectId = new Dictionary<Guid, Guid>();
        var depthByProjectId = new Dictionary<Guid, int>();
        var orderedDescendantProjectIds = new List<Guid>();
        var visitedDescendantProjectIds = new HashSet<Guid>();

        WalkDescendantProjects(project.Id, 1);

        var descendantPositions = new Dictionary<Guid, (double X, double Y)>();
        for (var index = 0; index < orderedDescendantProjectIds.Count; index++)
        {
            var descendantProjectId = orderedDescendantProjectIds[index];
            var depth = depthByProjectId[descendantProjectId];
            descendantPositions[descendantProjectId] = (
                X: 760 + ((depth - 1) * 320),
                Y: 520 + (index * 180));
        }

        var directParentProjectIds = parentProjectIdsByChild.TryGetValue(project.Id, out var directParents)
            ? directParents
                .Where(projectMap.ContainsKey)
                .OrderBy(parentProjectId => ResolveProjectSortKey(projectMap[parentProjectId]), StringComparer.OrdinalIgnoreCase)
                .ThenBy(parentProjectId => parentProjectId)
                .ToList()
            : [];
        var extraParentLinks = orderedDescendantProjectIds
            .SelectMany(childProjectId =>
            {
                if (!parentProjectIdsByChild.TryGetValue(childProjectId, out var parentProjectIds))
                {
                    return Enumerable.Empty<(Guid ParentProjectId, Guid ChildProjectId)>();
                }

                return parentProjectIds
                    .Where(parentProjectId =>
                        parentProjectId != primaryParentByProjectId[childProjectId] &&
                        (parentProjectId == project.Id || projectMap.ContainsKey(parentProjectId)))
                    .Select(parentProjectId => (
                        ParentProjectId: parentProjectId,
                        ChildProjectId: childProjectId));
            })
            .Distinct()
            .ToList();
        var visibleDescendantProjectIds = orderedDescendantProjectIds.ToHashSet();
        var visibleRelatedParentProjectIds = directParentProjectIds
            .Concat(extraParentLinks
                .Select(link => link.ParentProjectId)
                .Where(parentProjectId =>
                    parentProjectId != project.Id &&
                    !visibleDescendantProjectIds.Contains(parentProjectId)))
            .Distinct()
            .Where(projectMap.ContainsKey)
            .ToHashSet();

        var parentNodes = directParentProjectIds
            .Select((parentProjectId, index) => CreateRelatedParentNode(
                project.Id,
                projectMap[parentProjectId],
                "Parent project",
                120,
                40 + (index * 160),
                updatedAtUtc))
            .ToList();
        parentNodes.AddRange(visibleRelatedParentProjectIds
            .Where(parentProjectId => !directParentProjectIds.Contains(parentProjectId))
            .OrderBy(parentProjectId => ResolveProjectSortKey(projectMap[parentProjectId]), StringComparer.OrdinalIgnoreCase)
            .ThenBy(parentProjectId => parentProjectId)
            .Select(parentProjectId =>
            {
                var childPositions = extraParentLinks
                    .Where(link => link.ParentProjectId == parentProjectId)
                    .Select(link => descendantPositions.GetValueOrDefault(link.ChildProjectId))
                    .ToList();
                var x = childPositions.Count == 0
                    ? 1120d
                    : Math.Max(260d, childPositions.Min(position => position.X) - 220d);
                var y = childPositions.Count == 0
                    ? 460d
                    : Math.Round(childPositions.Average(position => position.Y), 0, MidpointRounding.AwayFromZero);
                return CreateRelatedParentNode(
                    project.Id,
                    projectMap[parentProjectId],
                    "Shared parent",
                    x,
                    y,
                    updatedAtUtc);
            }));

        var descendantNodes = orderedDescendantProjectIds
            .Select(descendantProjectId =>
            {
                var descendantProject = projectMap[descendantProjectId];
                var position = descendantPositions[descendantProjectId];
                var parentProjectId = primaryParentByProjectId[descendantProjectId];
                return new ProjectObjectRecord
                {
                    ProjectId = project.Id,
                    NodeKey = BuildProjectChildNodeKey(descendantProject.Id),
                    ObjectType = ProjectObjectType.ProjectRoot,
                    Title = descendantProject.Name,
                    Subtitle = ResolveRelatedProjectSubtitle(descendantProject, "Subproject"),
                    Status = descendantProject.Status.ToString(),
                    Notes = ResolveRelatedProjectNotes(descendantProject, "Subproject"),
                    Binding = ProjectStructureProjectionBindingFactory.Create($"/projects/{descendantProject.Id}/structure", "project", descendantProject.Id),
                    ParentNodeKey = parentProjectId == project.Id
                        ? BuildProjectRootNodeKey(project.Id)
                        : BuildProjectChildNodeKey(parentProjectId),
                    PositionX = position.X,
                    PositionY = position.Y,
                    CreatedAtUtc = descendantProject.CreatedAtUtc,
                    UpdatedAtUtc = updatedAtUtc,
                    IsSystemManaged = true
                };
            })
            .ToList();

        var projectLinks = new List<(string Source, string Target, ProjectObjectLinkKind Kind)>();
        projectLinks.AddRange(directParentProjectIds.Select(parentProjectId => (
            BuildRelatedParentNodeKey(parentProjectId),
            BuildProjectRootNodeKey(project.Id),
            ProjectObjectLinkKind.BelongsTo)));
        projectLinks.AddRange(orderedDescendantProjectIds.Select(descendantProjectId =>
        {
            var parentProjectId = primaryParentByProjectId[descendantProjectId];
            var sourceNodeKey = parentProjectId == project.Id
                ? BuildProjectRootNodeKey(project.Id)
                : BuildProjectChildNodeKey(parentProjectId);
            return (sourceNodeKey, BuildProjectChildNodeKey(descendantProjectId), ProjectObjectLinkKind.Contains);
        }));
        projectLinks.AddRange(extraParentLinks.Select(link =>
        {
            var sourceNodeKey = ResolveProjectHierarchyLinkSourceNodeKey(link.ParentProjectId, project.Id, visibleDescendantProjectIds);
            return (sourceNodeKey, BuildProjectChildNodeKey(link.ChildProjectId), ProjectObjectLinkKind.BelongsTo);
        }));

        return new ProjectHierarchyProjection(
            parentNodes,
            descendantNodes,
            projectLinks
                .Distinct()
                .ToList());

        void WalkDescendantProjects(Guid parentProjectId, int depth)
        {
            if (!childProjectIdsByParent.TryGetValue(parentProjectId, out var childProjectIds))
            {
                return;
            }

            foreach (var childProjectId in childProjectIds
                .Where(descendantProjectIds.Contains)
                .Where(projectMap.ContainsKey)
                .OrderBy(childProjectId => ResolveProjectSortKey(projectMap[childProjectId]), StringComparer.OrdinalIgnoreCase)
                .ThenBy(childProjectId => childProjectId))
            {
                if (!visitedDescendantProjectIds.Add(childProjectId))
                {
                    continue;
                }

                primaryParentByProjectId[childProjectId] = parentProjectId;
                depthByProjectId[childProjectId] = depth;
                orderedDescendantProjectIds.Add(childProjectId);
                WalkDescendantProjects(childProjectId, depth + 1);
            }
        }
    }

    private static int? NormalizeDurationSeconds(DateTimeOffset? startUtc, DateTimeOffset? endUtc)
    {
        if (!startUtc.HasValue || !endUtc.HasValue)
        {
            return null;
        }

        var totalSeconds = (int)Math.Round((endUtc.Value - startUtc.Value).TotalSeconds);
        return totalSeconds > 0
            ? totalSeconds
            : null;
    }

    private sealed record ProjectHierarchyProjection(
        IReadOnlyList<ProjectObjectRecord> ParentNodes,
        IReadOnlyList<ProjectObjectRecord> DescendantNodes,
        IReadOnlyList<(string Source, string Target, ProjectObjectLinkKind Kind)> Links);
}

internal sealed class ProjectResourceProjectionContributor(
    ResourceConnectorPluginRegistry resourceConnectorPluginRegistry) : IProjectStructureProjectionContributor
{
    public async Task ContributeAsync(ProjectStructureProjectionContext context, CancellationToken cancellationToken)
    {
        var resources = await context.DbContext.Set<ProjectResource>()
            .Where(item => item.ProjectId == context.ProjectId)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        foreach (var resource in resources.Select((resource, index) => new { Resource = resource, Index = index }))
        {
            var connectorPlugin = resourceConnectorPluginRegistry.Resolve(resource.Resource);
            context.AddNode(new ProjectObjectRecord
            {
                ProjectId = context.ProjectId,
                NodeKey = $"resource:{resource.Resource.Id}",
                ObjectType = connectorPlugin.ResolveWorkbenchObjectType(resource.Resource),
                ObjectSubtype = connectorPlugin.ResolveWorkbenchObjectSubtype(resource.Resource),
                Title = resource.Resource.Name,
                Subtitle = resource.Resource.LocationOrIdentifier,
                Status = resource.Resource.ValidationStatus.ToString(),
                Notes = resource.Resource.Description,
                Binding = ProjectStructureProjectionBindingFactory.Create($"/resources?resourceId={resource.Resource.Id}", "resource", resource.Resource.Id),
                ParentNodeKey = $"project:{context.ProjectId}",
                PositionX = 760,
                PositionY = 100 + (resource.Index * 120),
                CreatedAtUtc = resource.Resource.CreatedAtUtc,
                UpdatedAtUtc = context.AssembledAtUtc
            });
            context.AddLink($"project:{context.ProjectId}", $"resource:{resource.Resource.Id}", ProjectObjectLinkKind.Uses);
        }
    }
}

internal sealed class PromptFactoryProjectionContributor : IProjectStructureProjectionContributor
{
    public async Task ContributeAsync(ProjectStructureProjectionContext context, CancellationToken cancellationToken)
    {
        var phases = await context.DbContext.Set<ProjectPhase>()
            .Where(item => item.ProjectId == context.ProjectId)
            .ToListAsync(cancellationToken);
        var runs = (await context.DbContext.Set<PromptRun>()
                .Where(item => item.ProjectId == context.ProjectId)
                .ToListAsync(cancellationToken))
            .OrderBy(item => item.CreatedAtUtc)
            .ToList();
        var runIds = runs.Select(item => item.Id).ToArray();
        var runNodes = runIds.Length == 0
            ? []
            : await context.DbContext.Set<PromptRunNode>()
                .Where(item => runIds.Contains(item.PromptRunId))
                .OrderBy(item => item.Sequence)
                .ToListAsync(cancellationToken);

        foreach (var run in runs.Select((run, index) => new { Run = run, Index = index }))
        {
            var phaseNodeKey = phases.FirstOrDefault(phase =>
                string.Equals(phase.Name, run.Run.Phase, StringComparison.OrdinalIgnoreCase)) is { } phase
                ? $"phase:{phase.Id}"
                : $"project:{context.ProjectId}";
            context.AddNode(new ProjectObjectRecord
            {
                ProjectId = context.ProjectId,
                NodeKey = $"prompt-run:{run.Run.Id}",
                ObjectType = ProjectObjectType.PromptSession,
                Title = run.Run.Name,
                Subtitle = run.Run.Phase,
                Status = "Active",
                Notes = run.Run.Phase,
                Binding = ProjectStructureProjectionBindingFactory.Create($"/prompt-factory?runId={run.Run.Id}", "prompt-run", run.Run.Id),
                ParentNodeKey = phaseNodeKey,
                PositionX = 1080,
                PositionY = 100 + (run.Index * 160),
                CreatedAtUtc = run.Run.CreatedAtUtc,
                UpdatedAtUtc = context.AssembledAtUtc
            });
            context.AddLink(phaseNodeKey, $"prompt-run:{run.Run.Id}", ProjectObjectLinkKind.BelongsTo);
        }

        foreach (var node in runNodes.Select((node, index) => new { Node = node, Index = index }))
        {
            var parentNodeKey = node.Node.ParentPromptRunNodeId.HasValue
                ? $"prompt-node:{node.Node.ParentPromptRunNodeId.Value}"
                : $"prompt-run:{node.Node.PromptRunId}";
            context.AddNode(new ProjectObjectRecord
            {
                ProjectId = context.ProjectId,
                NodeKey = $"prompt-node:{node.Node.Id}",
                ObjectType = ProjectObjectType.PromptStep,
                Title = node.Node.Title,
                Subtitle = node.Node.BranchLabel,
                Status = node.Node.State.ToString(),
                Notes = node.Node.Notes,
                Binding = ProjectStructureProjectionBindingFactory.Create(
                    node.Node.PromptArtifactId.HasValue
                        ? $"/prompt-gallery?promptId={node.Node.PromptArtifactId}"
                        : $"/prompt-factory?runId={node.Node.PromptRunId}",
                    "prompt-node",
                    node.Node.Id),
                ParentNodeKey = parentNodeKey,
                PositionX = 1400,
                PositionY = 100 + (node.Index * 120),
                CreatedAtUtc = context.AssembledAtUtc,
                UpdatedAtUtc = context.AssembledAtUtc
            });
            context.AddLink(
                parentNodeKey,
                $"prompt-node:{node.Node.Id}",
                node.Node.ParentPromptRunNodeId.HasValue
                    ? ProjectObjectLinkKind.DerivedFrom
                    : ProjectObjectLinkKind.Contains);
        }
    }
}

internal sealed class ProcessProjectionContributor : IProjectStructureProjectionContributor
{
    public async Task ContributeAsync(ProjectStructureProjectionContext context, CancellationToken cancellationToken)
    {
        var linkedGlobalDefinitionIds = (await context.DbContext.Set<ProjectObjectLinkRecord>()
                .Where(item => item.ProjectId == context.ProjectId && !item.IsSystemManaged)
                .Select(item => new
                {
                    item.SourceNodeKey,
                    item.TargetNodeKey
                })
                .ToListAsync(cancellationToken))
            .SelectMany(item => new[] { item.SourceNodeKey, item.TargetNodeKey })
            .Select(TryResolveLinkedProcessDefinitionId)
            .Where(definitionId => definitionId.HasValue)
            .Select(definitionId => definitionId!.Value)
            .ToHashSet();

        var definitions = await context.DbContext.Set<ProcessDefinition>()
            .Where(item =>
                item.ProjectId == context.ProjectId ||
                (!item.ProjectId.HasValue && linkedGlobalDefinitionIds.Contains(item.Id)))
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
        if (definitions.Count == 0)
        {
            return;
        }

        var definitionIds = definitions.Select(item => item.Id).ToArray();
        var activeVersionIds = definitions
            .Where(item => item.ActivePublishedVersionId.HasValue)
            .Select(item => item.ActivePublishedVersionId!.Value)
            .ToArray();
        var roleCountsByVersionId = activeVersionIds.Length == 0
            ? new Dictionary<Guid, int>()
            : await context.DbContext.Set<ProcessRoleRequirement>()
                .Where(item => activeVersionIds.Contains(item.ProcessDefinitionVersionId))
                .GroupBy(item => item.ProcessDefinitionVersionId)
                .ToDictionaryAsync(group => group.Key, group => group.Count(), cancellationToken);
        var stepCountsByVersionId = activeVersionIds.Length == 0
            ? new Dictionary<Guid, int>()
            : await context.DbContext.Set<ProcessStepDefinition>()
                .Where(item => activeVersionIds.Contains(item.ProcessDefinitionVersionId))
                .GroupBy(item => item.ProcessDefinitionVersionId)
                .ToDictionaryAsync(group => group.Key, group => group.Count(), cancellationToken);
        var runs = definitionIds.Length == 0
            ? []
            : await context.DbContext.Set<ProcessRun>()
                .Where(item => item.ProjectId == context.ProjectId && definitionIds.Contains(item.ProcessDefinitionId))
                .OrderBy(item => item.Name)
                .ToListAsync(cancellationToken);
        var runIds = runs.Select(item => item.Id).ToArray();
        var stepRunStatsByRunId = runIds.Length == 0
            ? new Dictionary<Guid, ProcessRunProjectionStats>()
            : await context.DbContext.Set<ProcessStepRun>()
                .Where(item => runIds.Contains(item.ProcessRunId))
                .GroupBy(item => item.ProcessRunId)
                .Select(group => new ProcessRunProjectionStats(
                    group.Key,
                    group.Count(),
                    group.Count(item => item.Status == ProcessStepRunStatus.Completed),
                    group.Count(item => item.Status == ProcessStepRunStatus.Blocked),
                    group.Count(item => item.Status == ProcessStepRunStatus.WaitingApproval)))
                .ToDictionaryAsync(item => item.RunId, cancellationToken);
        var outputFoldersByRunId = runIds.Length == 0
            ? new Dictionary<Guid, IReadOnlyList<ProcessRunOutputFolderProjection>>()
            : (await context.DbContext.Set<ProcessArtifactRecord>()
                    .Where(item => runIds.Contains(item.ProcessRunId) && !string.IsNullOrWhiteSpace(item.ManagedStoragePath))
                    .ToListAsync(cancellationToken))
                .Select(item => new
                {
                    item.ProcessRunId,
                    item.Title,
                    item.CreatedAtUtc,
                    DirectoryPath = ResolveManagedOutputDirectoryPath(item.ManagedStoragePath)
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.DirectoryPath))
                .GroupBy(item => new
                {
                    item.ProcessRunId,
                    DirectoryPath = item.DirectoryPath!
                })
                .Select(group => new ProcessRunOutputFolderProjection(
                    group.Key.ProcessRunId,
                    group.Key.DirectoryPath,
                    group.Count(),
                    group.Select(item => item.Title.Trim())
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    group.Max(item => item.CreatedAtUtc)))
                .GroupBy(item => item.RunId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<ProcessRunOutputFolderProjection>)group
                        .OrderBy(item => item.DirectoryPath, StringComparer.OrdinalIgnoreCase)
                        .ToList());
        var runsByDefinitionId = runs
            .GroupBy(item => item.ProcessDefinitionId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedAtUtc).ToList());

        foreach (var definition in definitions.Select((item, index) => new { Definition = item, Index = index }))
        {
            var roleCount = definition.Definition.ActivePublishedVersionId.HasValue &&
                            roleCountsByVersionId.TryGetValue(definition.Definition.ActivePublishedVersionId.Value, out var resolvedRoleCount)
                ? resolvedRoleCount
                : 0;
            var stepCount = definition.Definition.ActivePublishedVersionId.HasValue &&
                            stepCountsByVersionId.TryGetValue(definition.Definition.ActivePublishedVersionId.Value, out var resolvedStepCount)
                ? resolvedStepCount
                : 0;
            var definitionNodeKey = BuildProcessDefinitionNodeKey(definition.Definition.Id);
            var definitionPosition = ProjectWorkbenchGraphConventions.GetDefaultPosition(ProjectObjectType.ProcessDefinition, definition.Index);

            context.AddNode(new ProjectObjectRecord
            {
                ProjectId = context.ProjectId,
                NodeKey = definitionNodeKey,
                ObjectType = ProjectObjectType.ProcessDefinition,
                Title = definition.Definition.Name,
                Subtitle = BuildProcessDefinitionSubtitle(definition.Definition, roleCount, stepCount),
                Status = definition.Definition.Status.ToString(),
                Notes = BuildProcessDefinitionNotes(definition.Definition, roleCount, stepCount),
                Binding = ProjectStructureProjectionBindingFactory.Create(
                    $"/projects/{context.ProjectId}/processes?processId={definition.Definition.Id}",
                    "process-definition",
                    definition.Definition.Id),
                ParentNodeKey = $"project:{context.ProjectId}",
                PositionX = definitionPosition.X,
                PositionY = definitionPosition.Y,
                CreatedAtUtc = definition.Definition.CreatedAtUtc,
                UpdatedAtUtc = definition.Definition.UpdatedAtUtc
            });
            context.AddLink($"project:{context.ProjectId}", definitionNodeKey, ProjectObjectLinkKind.Contains);

            if (!runsByDefinitionId.TryGetValue(definition.Definition.Id, out var definitionRuns))
            {
                continue;
            }

            foreach (var run in definitionRuns.Select((item, index) => new { Run = item, Index = index }))
            {
                var stats = stepRunStatsByRunId.GetValueOrDefault(run.Run.Id, new ProcessRunProjectionStats(run.Run.Id, 0, 0, 0, 0));
                var runNodeKey = BuildProcessRunNodeKey(run.Run.Id);
                var runPosition = ProjectWorkbenchGraphConventions.GetDefaultPosition(ProjectObjectType.ProcessRun, definition.Index + run.Index);
                var runNodeY = definitionPosition.Y + 70 + (run.Index * 120);

                context.AddNode(new ProjectObjectRecord
                {
                    ProjectId = context.ProjectId,
                    NodeKey = runNodeKey,
                    ObjectType = ProjectObjectType.ProcessRun,
                    Title = run.Run.Name,
                    Subtitle = BuildProcessRunSubtitle(run.Run, stats),
                    Status = run.Run.Status.ToString(),
                    Notes = BuildProcessRunNotes(run.Run, stats),
                    Binding = ProjectStructureProjectionBindingFactory.Create(
                        $"/projects/{context.ProjectId}/processes?runId={run.Run.Id}",
                        "process-run",
                        run.Run.Id),
                    ParentNodeKey = definitionNodeKey,
                    PositionX = runPosition.X,
                    PositionY = runNodeY,
                    CreatedAtUtc = run.Run.CreatedAtUtc,
                    UpdatedAtUtc = run.Run.UpdatedAtUtc
                });
                context.AddLink(definitionNodeKey, runNodeKey, ProjectObjectLinkKind.Contains);

                if (!outputFoldersByRunId.TryGetValue(run.Run.Id, out var outputFolders))
                {
                    continue;
                }

                foreach (var outputFolder in outputFolders.Select((item, index) => new { Folder = item, Index = index }))
                {
                    var outputNodeKey = BuildProcessRunOutputFolderNodeKey(run.Run.Id, outputFolder.Folder.DirectoryPath);
                    var storageReference = CreateManagedStorageReference(outputFolder.Folder.DirectoryPath);
                    var metadata = new ProjectObjectMetadataEnvelope
                    {
                        File = new ProjectFileMetadata
                        {
                            SourceHint = "Process run output folder",
                            ExternalPath = outputFolder.Folder.DirectoryPath
                        }
                    };

                    context.AddNode(new ProjectObjectRecord
                    {
                        ProjectId = context.ProjectId,
                        NodeKey = outputNodeKey,
                        ObjectType = ProjectObjectType.File,
                        Title = BuildProcessRunOutputFolderTitle(outputFolder.Folder.DirectoryPath),
                        Subtitle = BuildProcessRunOutputFolderSubtitle(outputFolder.Folder.ArtifactCount),
                        Status = "Stored",
                        Notes = BuildProcessRunOutputFolderNotes(outputFolder.Folder),
                        MetadataJson = ProjectObjectMetadataSerializer.Serialize(metadata),
                        Binding = new ProjectNodeBindingState(
                            $"/projects/{context.ProjectId}/processes?runId={run.Run.Id}",
                            "process-run-output-folder",
                            run.Run.Id,
                            outputFolder.Folder.DirectoryPath,
                            "application/x-directory",
                            string.Empty,
                            StorageJson.SerializeReference(storageReference)),
                        ParentNodeKey = runNodeKey,
                        PositionX = runPosition.X + 320,
                        PositionY = runNodeY + 34 + (outputFolder.Index * 72),
                        CreatedAtUtc = outputFolder.Folder.LastRecordedAtUtc,
                        UpdatedAtUtc = outputFolder.Folder.LastRecordedAtUtc
                    });
                    context.AddLink(runNodeKey, outputNodeKey, ProjectObjectLinkKind.Contains);
                }
            }
        }
    }

    private static string BuildProcessDefinitionNodeKey(Guid definitionId)
    {
        return $"process-definition:{definitionId}";
    }

    private static Guid? TryResolveLinkedProcessDefinitionId(string? nodeKey)
    {
        if (string.IsNullOrWhiteSpace(nodeKey) ||
            !nodeKey.StartsWith("process-definition:", StringComparison.Ordinal) ||
            !Guid.TryParse(nodeKey["process-definition:".Length..], out var definitionId))
        {
            return null;
        }

        return definitionId;
    }

    private static string BuildProcessRunNodeKey(Guid runId)
    {
        return $"process-run:{runId}";
    }

    private static string BuildProcessRunOutputFolderNodeKey(Guid runId, string directoryPath)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(directoryPath)))
            .ToLowerInvariant();
        return $"process-run-output:{runId}:{hash[..12]}";
    }

    private static string BuildProcessDefinitionSubtitle(ProcessDefinition definition, int roleCount, int stepCount)
    {
        return $"{definition.Status} · {roleCount} role(s) · {stepCount} step(s)";
    }

    private static string BuildProcessDefinitionNotes(ProcessDefinition definition, int roleCount, int stepCount)
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                definition.Summary?.Trim(),
                string.IsNullOrWhiteSpace(definition.ValueStatement) ? null : $"Value: {definition.ValueStatement.Trim()}",
                string.IsNullOrWhiteSpace(definition.OwnerName) ? null : $"Owner: {definition.OwnerName.Trim()}",
                string.IsNullOrWhiteSpace(definition.CustomerName) ? null : $"Customer: {definition.CustomerName.Trim()}",
                $"Role-first contract: {roleCount} role(s), {stepCount} step(s)."
            }.Where(item => !string.IsNullOrWhiteSpace(item)));
    }

    private static string BuildProcessRunSubtitle(ProcessRun run, ProcessRunProjectionStats stats)
    {
        var subtitleParts = new List<string>
        {
            run.OperatingMode.ToString(),
            $"{stats.CompletedStepCount}/{stats.TotalStepCount} step(s) complete"
        };
        if (stats.BlockedStepCount > 0)
        {
            subtitleParts.Add($"{stats.BlockedStepCount} blocked");
        }

        if (stats.WaitingApprovalCount > 0)
        {
            subtitleParts.Add($"{stats.WaitingApprovalCount} waiting approval");
        }

        return string.Join(" · ", subtitleParts);
    }

    private static string BuildProcessRunNotes(ProcessRun run, ProcessRunProjectionStats stats)
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                string.IsNullOrWhiteSpace(run.TriggerReason) ? null : $"Trigger: {run.TriggerReason.Trim()}",
                string.IsNullOrWhiteSpace(run.ExecutorSnapshotSummary) ? null : $"Executors: {run.ExecutorSnapshotSummary.Trim()}",
                $"Estimated cost: {run.EstimatedCost:C} | Actual cost: {run.ActualCost:C}",
                stats.BlockedStepCount > 0 ? $"Blocked step(s): {stats.BlockedStepCount}" : null
            }.Where(item => !string.IsNullOrWhiteSpace(item)));
    }

    private static string BuildProcessRunOutputFolderTitle(string directoryPath)
    {
        var leaf = directoryPath
            .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();
        return string.IsNullOrWhiteSpace(leaf)
            ? "Run outputs"
            : leaf.Trim();
    }

    private static string BuildProcessRunOutputFolderSubtitle(int artifactCount)
    {
        return artifactCount == 1
            ? "1 stored artifact"
            : $"{artifactCount} stored artifacts";
    }

    private static string BuildProcessRunOutputFolderNotes(ProcessRunOutputFolderProjection folder)
    {
        var noteLines = new List<string>
        {
            $"Managed path: {folder.DirectoryPath}"
        };
        if (folder.ArtifactTitles.Count == 1)
        {
            noteLines.Add($"Artifact: {folder.ArtifactTitles[0]}");
        }
        else if (folder.ArtifactTitles.Count > 1)
        {
            noteLines.Add($"Artifacts: {string.Join(", ", folder.ArtifactTitles.Take(4))}");
            if (folder.ArtifactTitles.Count > 4)
            {
                noteLines.Add($"+{folder.ArtifactTitles.Count - 4} more artifact title(s)");
            }
        }

        return string.Join(Environment.NewLine, noteLines);
    }

    private static string ResolveManagedOutputDirectoryPath(string managedStoragePath)
    {
        var normalizedPath = managedStoragePath
            .Trim()
            .Replace('\\', '/')
            .Trim('/');
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return string.Empty;
        }

        var platformPath = normalizedPath.Replace('/', Path.DirectorySeparatorChar);
        var directoryPath = Path.GetDirectoryName(platformPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return normalizedPath;
        }

        return directoryPath.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static StorageObjectReference CreateManagedStorageReference(string relativePath)
    {
        var normalizedPath = relativePath
            .Trim()
            .Replace('\\', '/')
            .TrimStart('/');
        var route = normalizedPath.StartsWith("managed-files/", StringComparison.OrdinalIgnoreCase)
            ? "/" + normalizedPath
            : $"/managed-files/{normalizedPath}";

        return new StorageObjectReference(
            null,
            StorageProviderKind.FileSystem,
            StorageLocatorKind.RelativePath,
            normalizedPath,
            string.Empty,
            "application/x-directory",
            null,
            route,
            "{}");
    }

    private sealed record ProcessRunProjectionStats(
        Guid RunId,
        int TotalStepCount,
        int CompletedStepCount,
        int BlockedStepCount,
        int WaitingApprovalCount);

    private sealed record ProcessRunOutputFolderProjection(
        Guid RunId,
        string DirectoryPath,
        int ArtifactCount,
        IReadOnlyList<string> ArtifactTitles,
        DateTimeOffset LastRecordedAtUtc);
}

internal sealed class ValidationProjectionContributor : IProjectStructureProjectionContributor
{
    public async Task ContributeAsync(ProjectStructureProjectionContext context, CancellationToken cancellationToken)
    {
        var validations = (await context.DbContext.Set<ValidationRun>()
                .Where(item => item.ProjectId == context.ProjectId)
                .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();

        foreach (var validation in validations.Select((validation, index) => new { Validation = validation, Index = index }))
        {
            context.AddNode(new ProjectObjectRecord
            {
                ProjectId = context.ProjectId,
                NodeKey = $"validation:{validation.Validation.Id}",
                ObjectType = ProjectObjectType.ValidationRun,
                Title = validation.Validation.ArtifactTitle,
                Subtitle = validation.Validation.ValidationType.ToString(),
                Status = validation.Validation.Decision.ToString(),
                Notes = validation.Validation.Summary,
                Binding = ProjectStructureProjectionBindingFactory.Create($"/validation?runId={validation.Validation.Id}", "validation-run", validation.Validation.Id),
                ParentNodeKey = $"project:{context.ProjectId}",
                PositionX = 780,
                PositionY = 580 + (validation.Index * 120),
                StartUtc = validation.Validation.UpdatedAtUtc,
                EndUtc = validation.Validation.UpdatedAtUtc.AddHours(1),
                DurationSeconds = 3600,
                CreatedAtUtc = validation.Validation.CreatedAtUtc,
                UpdatedAtUtc = context.AssembledAtUtc
            });
            context.AddLink($"project:{context.ProjectId}", $"validation:{validation.Validation.Id}", ProjectObjectLinkKind.Validates);
        }
    }
}

internal sealed class TestPlanProjectionContributor : IProjectStructureProjectionContributor
{
    public async Task ContributeAsync(ProjectStructureProjectionContext context, CancellationToken cancellationToken)
    {
        var testPlans = (await context.DbContext.Set<TestPlan>()
                .Where(item => item.ProjectId == context.ProjectId)
                .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();

        foreach (var testPlan in testPlans.Select((testPlan, index) => new { TestPlan = testPlan, Index = index }))
        {
            context.AddNode(new ProjectObjectRecord
            {
                ProjectId = context.ProjectId,
                NodeKey = $"test-plan:{testPlan.TestPlan.Id}",
                ObjectType = ProjectObjectType.TestPlan,
                Title = testPlan.TestPlan.Title,
                Subtitle = testPlan.TestPlan.Phase,
                Status = "Planned",
                Notes = testPlan.TestPlan.CoverageGoal,
                Binding = ProjectStructureProjectionBindingFactory.Create($"/test-lab?planId={testPlan.TestPlan.Id}", "test-plan", testPlan.TestPlan.Id),
                ParentNodeKey = $"project:{context.ProjectId}",
                PositionX = 1100,
                PositionY = 620 + (testPlan.Index * 140),
                StartUtc = testPlan.TestPlan.UpdatedAtUtc,
                EndUtc = testPlan.TestPlan.UpdatedAtUtc.AddHours(1),
                DurationSeconds = 3600,
                CreatedAtUtc = testPlan.TestPlan.CreatedAtUtc,
                UpdatedAtUtc = context.AssembledAtUtc
            });
            context.AddLink($"project:{context.ProjectId}", $"test-plan:{testPlan.TestPlan.Id}", ProjectObjectLinkKind.Tests);
        }
    }
}
