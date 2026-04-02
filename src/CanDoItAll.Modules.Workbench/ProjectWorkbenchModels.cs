using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Validation;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Workbench;

public enum ProjectStructureCommandKind
{
    Open,
    Wizard,
    Branch,
    Validate,
    Test,
    Skip,
    MarkUsed
}

public sealed class ProjectObjectRecord : IProjectObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string NodeKey { get; set; } = string.Empty;
    public ProjectObjectType ObjectType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string ExternalArtifactKind { get; set; } = string.Empty;
    public Guid? ExternalArtifactId { get; set; }
    public string ObjectSubtype { get; set; } = string.Empty;
    public string MediaRelativePath { get; set; } = string.Empty;
    public string MediaContentType { get; set; } = string.Empty;
    public string MediaOriginalFileName { get; set; } = string.Empty;
    public string ProgressMode { get; set; } = string.Empty;
    public int ProgressPercent { get; set; } = -1;
    public string MarkerIcon { get; set; } = string.Empty;
    public string MarkerTone { get; set; } = string.Empty;
    public string MarkerLabel { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string MetadataJson { get; set; } = "{}";
    public string? ParentNodeKey { get; set; }
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public DateTimeOffset? StartUtc { get; set; }
    public DateTimeOffset? EndUtc { get; set; }
    public bool IsSystemManaged { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class ProjectObjectRecordConfiguration : IEntityTypeConfiguration<ProjectObjectRecord>
{
    public void Configure(EntityTypeBuilder<ProjectObjectRecord> builder)
    {
        builder.ToTable("Workbench_ProjectObjects");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.NodeKey).HasMaxLength(160).IsRequired();
        builder.Property(item => item.Title).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Subtitle).HasMaxLength(240);
        builder.Property(item => item.Status).HasMaxLength(120);
        builder.Property(item => item.Notes).HasColumnType("TEXT");
        builder.Property(item => item.Route).HasMaxLength(800);
        builder.Property(item => item.ExternalArtifactKind).HasMaxLength(120);
        builder.Property(item => item.ObjectSubtype).HasMaxLength(120);
        builder.Property(item => item.MediaRelativePath).HasMaxLength(800);
        builder.Property(item => item.MediaContentType).HasMaxLength(160);
        builder.Property(item => item.MediaOriginalFileName).HasMaxLength(260);
        builder.Property(item => item.ProgressMode).HasMaxLength(32);
        builder.Property(item => item.MarkerIcon).HasMaxLength(80);
        builder.Property(item => item.MarkerTone).HasMaxLength(40);
        builder.Property(item => item.MarkerLabel).HasMaxLength(120);
        builder.Property(item => item.MetadataJson).HasColumnType("TEXT");
        builder.Property(item => item.ParentNodeKey).HasMaxLength(160);
        builder.HasIndex(item => new { item.ProjectId, item.NodeKey }).IsUnique();
    }
}

public sealed class ProjectObjectLinkRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string SourceNodeKey { get; set; } = string.Empty;
    public string TargetNodeKey { get; set; } = string.Empty;
    public ProjectObjectLinkKind LinkKind { get; set; }
    public bool IsSystemManaged { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class ProjectObjectLinkRecordConfiguration : IEntityTypeConfiguration<ProjectObjectLinkRecord>
{
    public void Configure(EntityTypeBuilder<ProjectObjectLinkRecord> builder)
    {
        builder.ToTable("Workbench_ProjectObjectLinks");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.SourceNodeKey).HasMaxLength(160).IsRequired();
        builder.Property(item => item.TargetNodeKey).HasMaxLength(160).IsRequired();
        builder.HasIndex(item => new { item.ProjectId, item.SourceNodeKey, item.TargetNodeKey, item.LinkKind, item.IsSystemManaged }).IsUnique();
    }
}

public sealed class ProjectWorkbenchViewStateRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string SurfaceKind { get; set; } = string.Empty;
    public string StateJson { get; set; } = "{}";
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class ProjectWorkbenchViewStateRecordConfiguration : IEntityTypeConfiguration<ProjectWorkbenchViewStateRecord>
{
    public void Configure(EntityTypeBuilder<ProjectWorkbenchViewStateRecord> builder)
    {
        builder.ToTable("Workbench_ViewStates");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.SurfaceKind).HasMaxLength(80).IsRequired();
        builder.Property(item => item.StateJson).HasColumnType("TEXT");
        builder.HasIndex(item => new { item.ProjectId, item.SurfaceKind }).IsUnique();
    }
}

public enum ProjectStructureProjectRole
{
    None,
    ActiveProject,
    Subproject,
    ParentProject,
    AdditionalParentProject
}

public sealed record ProjectStructureNode(
    string Id,
    string? ParentId,
    ProjectObjectType ObjectType,
    string ObjectSubtype,
    string Title,
    string Subtitle,
    string Status,
    string Notes,
    string Route,
    string ArtifactKind,
    Guid? ArtifactId,
    string MediaRelativePath,
    string MediaContentType,
    string MediaOriginalFileName,
    double X,
    double Y,
    ProjectObjectVisualProfile VisualProfile,
    IReadOnlyList<string> Badges,
    string ProgressMode,
    int ProgressPercent,
    string MarkerIcon,
    string MarkerTone,
    string MarkerLabel,
    IReadOnlyList<ProjectNodeMarker> Markers,
    int Priority,
    DateTimeOffset? StartUtc = null,
    DateTimeOffset? EndUtc = null,
    string MetadataJson = "{}",
    ProjectStructureProjectRole ProjectRole = ProjectStructureProjectRole.None,
    Guid? RelatedProjectId = null,
    int ParentProjectCount = 0);

public sealed record ProjectStructureLink(string SourceId, string TargetId, ProjectObjectLinkKind Kind, bool IsUserAuthored);

public sealed record ProjectStructureSurface(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<ProjectStructureNode> Nodes,
    IReadOnlyList<ProjectStructureLink> Links,
    string? ViewStateJson);

public sealed record ProjectCalendarEvent(
    Guid Id,
    string Title,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string Status,
    string Route,
    string ArtifactKind,
    Guid? ArtifactId,
    ProjectObjectType ObjectType,
    string AccentColor);

public sealed record ProjectCalendarSurface(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<ProjectCalendarEvent> Events,
    string PreferredView,
    string? ViewStateJson);

public sealed record ProjectWorkbenchUnavailableState(
    Guid ProjectId,
    string Title,
    string Description,
    string SafeRoute);

public sealed record ProjectStructureLoadResult(
    ProjectStructureSurface? Surface,
    ProjectWorkbenchUnavailableState? UnavailableState)
{
    public bool IsSuccess => Surface is not null;
}

public sealed record ProjectCalendarLoadResult(
    ProjectCalendarSurface? Surface,
    ProjectWorkbenchUnavailableState? UnavailableState)
{
    public bool IsSuccess => Surface is not null;
}

public sealed record ProjectObjectCreateRequest(
    ProjectObjectType ObjectType,
    string Title,
    string Subtitle,
    string Notes,
    string? ParentNodeKey,
    double? X = null,
    double? Y = null,
    DateTimeOffset? StartUtc = null,
    DateTimeOffset? EndUtc = null,
    string? ObjectSubtype = null,
    ProjectObjectMediaPayload? Media = null,
    string? MetadataJson = null);

public sealed record ProjectObjectEditRequest(
    string Title,
    string Subtitle,
    string Notes,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    string MetadataJson);

public sealed record ProjectObjectReclassificationRequest(
    ProjectObjectType TargetObjectType,
    string TargetObjectSubtype,
    string Title,
    string Subtitle,
    string Notes,
    string MetadataJson = "{}");

public sealed record ProjectObjectSeedRequest(
    ProjectObjectType ObjectType,
    string Title,
    string Subtitle,
    string Notes,
    DateTimeOffset? StartUtc = null,
    DateTimeOffset? EndUtc = null,
    string? ObjectSubtype = null,
    string? MetadataJson = null);

public sealed record ProjectObjectMediaPayload(
    string FileName,
    string ContentType,
    string Base64Data);

public sealed record ProjectCanvasContextActionRequest(
    string? NodeId,
    string Action,
    double X,
    double Y);

public sealed record ProjectNodeMoveRequest(
    string NodeId,
    double X,
    double Y);

public sealed record ProjectStructureSubprojectTransferResult(
    Guid TargetProjectId,
    int MovedNodeCount,
    int MovedRootCount);

public sealed record ProjectStructureSubtreeRecompositionResult(
    string RootNodeId,
    int DescendantCount,
    int RepositionedNodeCount);

internal sealed record SavedMediaDescriptor(
    string RelativePath,
    string Route,
    string ContentType,
    string OriginalFileName,
    string ArtifactKind);

/* codex-capsule
kind: service
name: ProjectWorkbenchService
summary: Owns the unified project object graph, workbench projections, calendar view state, and typed structure commands.
owns: project-graph, structure-canvas-projection, calendar-projection, view-state
deps: AppDbContext
risks: stale-cross-module-sync, graph-drift
tests: integration:ProjectWorkbenchServiceTests
inputs: project id, command requests, graph mutations
outputs: ProjectStructureSurface, ProjectCalendarSurface, ArtifactReference
*/
public sealed class ProjectWorkbenchService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    IFileStore fileStore,
    PromptFactoryService promptFactoryService) : IProjectWorkbenchSeedService
{
    private const string ProjectRootNodePrefix = "project:";
    private const string ProjectChildNodePrefix = "project-child:";
    private const string ProjectRelatedParentNodePrefix = "project-related-parent:";

    private enum ProjectHierarchyNodeKind
    {
        None,
        ActiveProject,
        Subproject,
        RelatedParent
    }

    private enum ProjectMarkerMutationMode
    {
        ReplaceAll,
        Add,
        Remove,
        Toggle,
        ClearAll
    }

    public async Task<ProjectStructureSurface> GetStructureAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var loadResult = await TryGetStructureAsync(projectId, cancellationToken);
        return loadResult.Surface
            ?? throw new InvalidOperationException($"Project '{projectId}' was not found in the active database profile.");
    }

    public async Task<ProjectStructureLoadResult> TryGetStructureAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var project = await dbContext.Set<Project>().FirstOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project is null)
        {
            return new ProjectStructureLoadResult(
                null,
                new ProjectWorkbenchUnavailableState(
                    projectId,
                    "Project structure unavailable",
                    "This project does not exist in the active database profile anymore. Return to the project list or switch back to the previous database profile.",
                    "/projects"));
        }

        await SyncGraphAsync(dbContext, projectId, cancellationToken);

        var nodes = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId)
            .OrderBy(item => item.PositionY)
            .ThenBy(item => item.PositionX)
            .ToListAsync(cancellationToken);
        var links = await dbContext.Set<ProjectObjectLinkRecord>()
            .Where(item => item.ProjectId == projectId)
            .OrderBy(item => item.SourceNodeKey)
            .ThenBy(item => item.TargetNodeKey)
            .ToListAsync(cancellationToken);
        var viewState = await LoadViewStateAsync(dbContext, projectId, "structure", cancellationToken);
        var mappedNodes = MapStructureNodes(nodes, links);

        return new ProjectStructureLoadResult(
            new ProjectStructureSurface(
                project.Id,
                project.Name,
                mappedNodes,
                links.Select(link => new ProjectStructureLink(link.SourceNodeKey, link.TargetNodeKey, link.LinkKind, !link.IsSystemManaged)).ToList(),
                viewState),
            null);
    }

    public async Task<ProjectCalendarSurface> GetCalendarAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var loadResult = await TryGetCalendarAsync(projectId, cancellationToken);
        return loadResult.Surface
            ?? throw new InvalidOperationException($"Project '{projectId}' was not found in the active database profile.");
    }

    public async Task<ProjectCalendarLoadResult> TryGetCalendarAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var project = await dbContext.Set<Project>().FirstOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project is null)
        {
            return new ProjectCalendarLoadResult(
                null,
                new ProjectWorkbenchUnavailableState(
                    projectId,
                    "Project calendar unavailable",
                    "This project does not exist in the active database profile anymore. Return to the project list or switch back to the previous database profile.",
                    "/projects"));
        }

        await SyncGraphAsync(dbContext, projectId, cancellationToken);

        var viewState = await LoadViewStateAsync(dbContext, projectId, "calendar", cancellationToken);
        var preferredView = ResolvePreferredCalendarView(viewState);
        var eventRecords = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId && item.StartUtc.HasValue && item.EndUtc.HasValue)
            .ToListAsync(cancellationToken);
        var events = eventRecords
            .OrderBy(item => item.StartUtc)
            .ThenBy(item => item.EndUtc)
            .ThenBy(item => item.Title)
            .Select(item => new ProjectCalendarEvent(
                item.Id,
                item.Title,
                item.StartUtc!.Value,
                item.EndUtc!.Value,
                item.Status,
                item.Route,
                item.ExternalArtifactKind,
                item.ExternalArtifactId,
                item.ObjectType,
                ResolveVisualProfile(item.ObjectType, item.ObjectSubtype, item.Status).AccentColor))
            .ToList();

        return new ProjectCalendarLoadResult(
            new ProjectCalendarSurface(project.Id, project.Name, events, preferredView, viewState),
            null);
    }

    public async Task<ProjectStructureNode> CreateObjectAsync(Guid projectId, ProjectObjectCreateRequest request, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var existingCount = await dbContext.Set<ProjectObjectRecord>().CountAsync(item => item.ProjectId == projectId && !item.IsSystemManaged, cancellationToken);
        var normalizedParentNodeKey = NormalizeEditableParentNodeKey(projectId, request.ParentNodeKey);
        var position = request.X.HasValue && request.Y.HasValue
            ? (request.X.Value, request.Y.Value)
            : GetDefaultPosition(request.ObjectType, existingCount + 1);
        var media = await SaveMediaAsync(projectId, request.ObjectType, request.Media, cancellationToken);
        var route = media?.Route ?? $"/projects/{projectId}/structure";
        var artifactKind = media?.ArtifactKind ?? request.ObjectType.ToString();
        var metadataJson = ResolveMetadataJson(request.ObjectType, request.ObjectSubtype, request.MetadataJson, media);

        var record = new ProjectObjectRecord
        {
            ProjectId = projectId,
            NodeKey = $"custom:{Guid.NewGuid():N}",
            ObjectType = request.ObjectType,
            Title = string.IsNullOrWhiteSpace(request.Title) ? request.ObjectType.ToString() : request.Title.Trim(),
            Subtitle = request.Subtitle?.Trim() ?? string.Empty,
            Status = "Draft",
            Notes = request.Notes?.Trim() ?? string.Empty,
            Route = route,
            ExternalArtifactKind = artifactKind,
            ObjectSubtype = request.ObjectSubtype?.Trim() ?? string.Empty,
            MediaRelativePath = media?.RelativePath ?? string.Empty,
            MediaContentType = media?.ContentType ?? string.Empty,
            MediaOriginalFileName = media?.OriginalFileName ?? string.Empty,
            ProgressMode = "progress",
            ProgressPercent = 0,
            MetadataJson = metadataJson,
            ParentNodeKey = normalizedParentNodeKey,
            PositionX = position.Item1,
            PositionY = position.Item2,
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc ?? request.StartUtc?.AddHours(1),
            CreatedAtUtc = clock.GetUtcNow(),
            UpdatedAtUtc = clock.GetUtcNow()
        };

        await dbContext.Set<ProjectObjectRecord>().AddAsync(record, cancellationToken);
        await UpsertLinkAsync(
            dbContext,
            projectId,
            normalizedParentNodeKey,
            record.NodeKey,
            ResolveHierarchyLinkKind(projectId, normalizedParentNodeKey),
            isSystemManaged: false,
            cancellationToken);

        if (request.ObjectType == ProjectObjectType.PromptFlow)
        {
            await EnsurePromptFlowWizardAsync(dbContext, projectId, record, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapStructureNode(record);
    }

    public async Task SeedProjectObjectsAsync(Guid projectId, IReadOnlyCollection<ProjectObjectSeedRequest> seeds, CancellationToken cancellationToken = default)
    {
        if (seeds.Count == 0)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var existingCount = await dbContext.Set<ProjectObjectRecord>().CountAsync(item => item.ProjectId == projectId && !item.IsSystemManaged, cancellationToken);
        var index = 0;
        var projectRootNodeKey = BuildProjectRootNodeKey(projectId);

        foreach (var seed in seeds.Where(seed => !string.IsNullOrWhiteSpace(seed.Title)))
        {
            index++;
            var nodeKey = $"custom:{Guid.NewGuid():N}";
            var position = GetDefaultPosition(seed.ObjectType, existingCount + index);
            await dbContext.Set<ProjectObjectRecord>().AddAsync(new ProjectObjectRecord
            {
                ProjectId = projectId,
                NodeKey = nodeKey,
                ObjectType = seed.ObjectType,
                Title = seed.Title.Trim(),
                Subtitle = seed.Subtitle?.Trim() ?? string.Empty,
                Status = "Planned",
                Notes = seed.Notes?.Trim() ?? string.Empty,
                Route = $"/projects/{projectId}/structure",
                ExternalArtifactKind = seed.ObjectType.ToString(),
                ObjectSubtype = seed.ObjectSubtype?.Trim() ?? string.Empty,
                ProgressMode = "progress",
                ProgressPercent = 0,
                MetadataJson = ResolveMetadataJson(seed.ObjectType, seed.ObjectSubtype, seed.MetadataJson, null),
                ParentNodeKey = projectRootNodeKey,
                PositionX = position.Item1,
                PositionY = position.Item2,
                StartUtc = seed.StartUtc,
                EndUtc = seed.EndUtc ?? seed.StartUtc?.AddHours(1),
                CreatedAtUtc = clock.GetUtcNow(),
                UpdatedAtUtc = clock.GetUtcNow()
            }, cancellationToken);

            await UpsertLinkAsync(dbContext, projectId, projectRootNodeKey, nodeKey, ProjectObjectLinkKind.Contains, isSystemManaged: false, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    async Task IProjectWorkbenchSeedService.SeedProjectObjectsAsync(Guid projectId, IReadOnlyCollection<ProjectObjectSeedDraft> seeds, CancellationToken cancellationToken)
        => await SeedProjectObjectsAsync(
            projectId,
            seeds.Select(seed => new ProjectObjectSeedRequest(seed.ObjectType, seed.Title, seed.Subtitle, seed.Notes, seed.StartUtc, seed.EndUtc, null)).ToList(),
            cancellationToken);

    public async Task LinkObjectsAsync(Guid projectId, string sourceNodeKey, string targetNodeKey, ProjectObjectLinkKind linkKind, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        await UpsertLinkAsync(dbContext, projectId, sourceNodeKey, targetNodeKey, linkKind, isSystemManaged: false, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProjectStructureNode?> ReparentObjectAsync(
        Guid projectId,
        string nodeKey,
        string? parentNodeKey,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var node = await dbContext.Set<ProjectObjectRecord>()
            .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.NodeKey == nodeKey && !item.IsSystemManaged, cancellationToken);
        if (node is null)
        {
            return null;
        }

        var normalizedParentNodeKey = NormalizeEditableParentNodeKey(projectId, parentNodeKey);
        if (string.Equals(node.ParentNodeKey, normalizedParentNodeKey, StringComparison.Ordinal))
        {
            return MapStructureNode(node);
        }

        var parentLinks = await dbContext.Set<ProjectObjectLinkRecord>()
            .Where(item => item.ProjectId == projectId &&
                item.TargetNodeKey == node.NodeKey &&
                !item.IsSystemManaged &&
                (item.LinkKind == ProjectObjectLinkKind.BelongsTo || item.LinkKind == ProjectObjectLinkKind.Contains))
            .ToListAsync(cancellationToken);
        if (parentLinks.Count > 0)
        {
            dbContext.RemoveRange(parentLinks);
        }

        node.ParentNodeKey = normalizedParentNodeKey;
        node.UpdatedAtUtc = clock.GetUtcNow();

        await UpsertLinkAsync(
            dbContext,
            projectId,
            normalizedParentNodeKey,
            node.NodeKey,
            ResolveHierarchyLinkKind(projectId, normalizedParentNodeKey),
            isSystemManaged: false,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapStructureNode(node);
    }

    public async Task<int> DeleteObjectAsync(Guid projectId, string nodeKey, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var records = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var root = records.FirstOrDefault(item => item.NodeKey == nodeKey && !item.IsSystemManaged);
        if (root is null)
        {
            return 0;
        }

        var childrenByParent = records
            .Where(item => !item.IsSystemManaged && !string.IsNullOrWhiteSpace(item.ParentNodeKey))
            .GroupBy(item => item.ParentNodeKey!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var keysToDelete = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(root.NodeKey);

        while (queue.Count > 0)
        {
            var currentNodeKey = queue.Dequeue();
            if (!keysToDelete.Add(currentNodeKey))
            {
                continue;
            }

            if (!childrenByParent.TryGetValue(currentNodeKey, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                queue.Enqueue(child.NodeKey);
            }
        }

        var linksToDelete = await dbContext.Set<ProjectObjectLinkRecord>()
            .Where(item => item.ProjectId == projectId &&
                (keysToDelete.Contains(item.SourceNodeKey) || keysToDelete.Contains(item.TargetNodeKey)))
            .ToListAsync(cancellationToken);
        if (linksToDelete.Count > 0)
        {
            dbContext.RemoveRange(linksToDelete);
        }

        var recordsToDelete = records
            .Where(item => !item.IsSystemManaged && keysToDelete.Contains(item.NodeKey))
            .ToList();
        dbContext.RemoveRange(recordsToDelete);
        await dbContext.SaveChangesAsync(cancellationToken);
        return recordsToDelete.Count;
    }

    public async Task MoveObjectAsync(Guid projectId, string nodeKey, double x, double y, CancellationToken cancellationToken = default)
    {
        await MoveObjectsAsync(
            projectId,
            [new ProjectNodeMoveRequest(nodeKey, x, y)],
            cancellationToken);
    }

    public async Task<IReadOnlyList<string>> MoveObjectsAsync(
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

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var nodeKeys = requestedPositions
            .Select(position => position.NodeId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var nodes = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId && nodeKeys.Contains(item.NodeKey))
            .ToListAsync(cancellationToken);
        if (nodes.Count == 0)
        {
            return [];
        }

        var nodesByKey = nodes.ToDictionary(item => item.NodeKey, StringComparer.Ordinal);
        var updatedNodeIds = new List<string>(requestedPositions.Count);
        var updatedAtUtc = clock.GetUtcNow();

        foreach (var position in requestedPositions)
        {
            if (!nodesByKey.TryGetValue(position.NodeId, out var node))
            {
                continue;
            }

            if (Math.Abs(node.PositionX - position.X) < 0.5d &&
                Math.Abs(node.PositionY - position.Y) < 0.5d)
            {
                continue;
            }

            node.PositionX = position.X;
            node.PositionY = position.Y;
            node.UpdatedAtUtc = updatedAtUtc;
            updatedNodeIds.Add(node.NodeKey);
        }

        if (updatedNodeIds.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return updatedNodeIds;
    }

    public async Task<ProjectStructureSubtreeRecompositionResult?> RecomposeSubtreeAsync(
        Guid projectId,
        string rootNodeKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootNodeKey))
        {
            return null;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        await SyncGraphAsync(dbContext, projectId, cancellationToken);

        var records = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var links = await dbContext.Set<ProjectObjectLinkRecord>()
            .Where(item => item.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var plan = ProjectStructureSubtreeRecompositionEngine.Recompose(MapStructureNodes(records, links), rootNodeKey);
        if (plan is null)
        {
            return null;
        }

        if (plan.DescendantCount == 0)
        {
            return new ProjectStructureSubtreeRecompositionResult(rootNodeKey, 0, 0);
        }

        var targetPositions = plan.Positions.ToDictionary(position => position.NodeId, StringComparer.Ordinal);
        var updatedAtUtc = clock.GetUtcNow();
        var repositionedNodeCount = 0;

        foreach (var record in records)
        {
            if (!targetPositions.TryGetValue(record.NodeKey, out var targetPosition))
            {
                continue;
            }

            if (Math.Abs(record.PositionX - targetPosition.X) < 0.5d &&
                Math.Abs(record.PositionY - targetPosition.Y) < 0.5d)
            {
                continue;
            }

            record.PositionX = targetPosition.X;
            record.PositionY = targetPosition.Y;
            record.UpdatedAtUtc = updatedAtUtc;
            repositionedNodeCount++;
        }

        if (repositionedNodeCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new ProjectStructureSubtreeRecompositionResult(rootNodeKey, plan.DescendantCount, repositionedNodeCount);
    }

    public async Task<ProjectStructureNode?> UpdateObjectAsync(
        Guid projectId,
        string nodeKey,
        string title,
        string subtitle,
        string notes,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var node = await dbContext.Set<ProjectObjectRecord>()
            .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.NodeKey == nodeKey, cancellationToken);
        if (node is null)
        {
            return null;
        }

        node.Title = string.IsNullOrWhiteSpace(title) ? node.Title : title.Trim();
        node.Subtitle = subtitle?.Trim() ?? string.Empty;
        node.Notes = notes?.Trim() ?? string.Empty;
        node.UpdatedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapStructureNode(node);
    }

    public async Task<ProjectStructureNode?> UpdateObjectAsync(
        Guid projectId,
        string nodeKey,
        ProjectObjectEditRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var node = await dbContext.Set<ProjectObjectRecord>()
            .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.NodeKey == nodeKey && !item.IsSystemManaged, cancellationToken);
        if (node is null)
        {
            return null;
        }

        node.Title = string.IsNullOrWhiteSpace(request.Title) ? node.Title : request.Title.Trim();
        node.Subtitle = request.Subtitle?.Trim() ?? string.Empty;
        node.Notes = request.Notes?.Trim() ?? string.Empty;
        node.StartUtc = request.StartUtc;
        node.EndUtc = request.EndUtc;
        node.MetadataJson = ResolveMetadataJson(node.ObjectType, node.ObjectSubtype, request.MetadataJson, null);
        node.UpdatedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapStructureNode(node);
    }

    public async Task<ProjectStructureNode?> ReclassifyObjectAsync(
        Guid projectId,
        string nodeKey,
        ProjectObjectReclassificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var node = await dbContext.Set<ProjectObjectRecord>()
            .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.NodeKey == nodeKey && !item.IsSystemManaged, cancellationToken);
        if (node is null || !IsSupportedReclassification(node.ObjectType, request.TargetObjectType))
        {
            return null;
        }

        node.ObjectType = request.TargetObjectType;
        node.ObjectSubtype = request.TargetObjectSubtype?.Trim() ?? string.Empty;
        node.Title = string.IsNullOrWhiteSpace(request.Title) ? node.Title : request.Title.Trim();
        node.Subtitle = request.Subtitle?.Trim() ?? string.Empty;
        node.Notes = request.Notes?.Trim() ?? string.Empty;
        node.MetadataJson = ResolveMetadataJson(node.ObjectType, node.ObjectSubtype, request.MetadataJson, null);
        node.ExternalArtifactKind = node.ObjectType.ToString();
        if (string.IsNullOrWhiteSpace(node.Route))
        {
            node.Route = $"/projects/{projectId}/structure";
        }

        node.UpdatedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapStructureNode(node);
    }

    public async Task<ProjectStructureSubprojectTransferResult?> MoveDescendantsToProjectAsync(
        Guid sourceProjectId,
        string sourceNodeKey,
        Guid targetProjectId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceNodeKey) || sourceProjectId == targetProjectId)
        {
            return null;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var sourceRecords = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == sourceProjectId)
            .ToListAsync(cancellationToken);
        if (sourceRecords.Count == 0)
        {
            return null;
        }

        var editableChildrenByParent = sourceRecords
            .Where(item => !item.IsSystemManaged && !string.IsNullOrWhiteSpace(item.ParentNodeKey))
            .GroupBy(item => item.ParentNodeKey!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var movedNodeKeys = new HashSet<string>(StringComparer.Ordinal);
        var movedRootKeys = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(sourceNodeKey);

        while (queue.Count > 0)
        {
            var currentNodeKey = queue.Dequeue();
            if (!editableChildrenByParent.TryGetValue(currentNodeKey, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                if (!movedNodeKeys.Add(child.NodeKey))
                {
                    continue;
                }

                if (string.Equals(child.ParentNodeKey, sourceNodeKey, StringComparison.Ordinal))
                {
                    movedRootKeys.Add(child.NodeKey);
                }

                queue.Enqueue(child.NodeKey);
            }
        }

        if (movedNodeKeys.Count == 0)
        {
            return new ProjectStructureSubprojectTransferResult(targetProjectId, 0, 0);
        }

        var targetNodeKeys = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == targetProjectId)
            .Select(item => item.NodeKey)
            .ToListAsync(cancellationToken);
        if (targetNodeKeys.Any(movedNodeKeys.Contains))
        {
            return null;
        }

        var targetRootNodeKey = BuildProjectRootNodeKey(targetProjectId);
        var movedRecords = sourceRecords
            .Where(item => movedNodeKeys.Contains(item.NodeKey))
            .ToList();
        var updatedAtUtc = clock.GetUtcNow();

        foreach (var record in movedRecords)
        {
            var originalParentNodeKey = record.ParentNodeKey;
            record.ProjectId = targetProjectId;
            record.ParentNodeKey = movedNodeKeys.Contains(originalParentNodeKey ?? string.Empty)
                ? originalParentNodeKey
                : targetRootNodeKey;
            record.Route = RewriteProjectScopedRoute(record.Route, sourceProjectId, targetProjectId);
            record.UpdatedAtUtc = updatedAtUtc;
        }

        var linksToProcess = await dbContext.Set<ProjectObjectLinkRecord>()
            .Where(item => item.ProjectId == sourceProjectId &&
                (movedNodeKeys.Contains(item.SourceNodeKey) || movedNodeKeys.Contains(item.TargetNodeKey)))
            .ToListAsync(cancellationToken);

        foreach (var link in linksToProcess)
        {
            var hasMovedSource = movedNodeKeys.Contains(link.SourceNodeKey);
            var hasMovedTarget = movedNodeKeys.Contains(link.TargetNodeKey);
            if (hasMovedSource && hasMovedTarget)
            {
                link.ProjectId = targetProjectId;
                continue;
            }

            dbContext.Remove(link);
        }

        foreach (var movedRootKey in movedRootKeys)
        {
            await UpsertLinkAsync(
                dbContext,
                targetProjectId,
                targetRootNodeKey,
                movedRootKey,
                ResolveHierarchyLinkKind(targetProjectId, targetRootNodeKey),
                isSystemManaged: false,
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new ProjectStructureSubprojectTransferResult(targetProjectId, movedNodeKeys.Count, movedRootKeys.Count);
    }

    public async Task<ProjectStructureNode?> UpdateObjectMetadataAsync(
        Guid projectId,
        string nodeKey,
        string metadataJson,
        string? notes = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var node = await dbContext.Set<ProjectObjectRecord>()
            .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.NodeKey == nodeKey && !item.IsSystemManaged, cancellationToken);
        if (node is null)
        {
            return null;
        }

        node.MetadataJson = ResolveMetadataJson(node.ObjectType, node.ObjectSubtype, metadataJson, null);
        if (notes is not null)
        {
            node.Notes = notes.Trim();
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            node.Status = status.Trim();
            var progress = ResolveStatusBackedProgress(node.Status);
            node.ProgressMode = progress.Mode;
            node.ProgressPercent = progress.Percent;
        }

        node.UpdatedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapStructureNode(node);
    }

    public async Task<IReadOnlyList<ProjectStructureNode>> UpdateObjectStatusesDetailedAsync(
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys,
        string status,
        CancellationToken cancellationToken = default)
    {
        if (nodeKeys.Count == 0 || string.IsNullOrWhiteSpace(status))
        {
            return [];
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var normalizedKeys = nodeKeys.Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.Ordinal).ToList();
        if (normalizedKeys.Count == 0)
        {
            return [];
        }

        var nodes = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId &&
                !item.IsSystemManaged &&
                normalizedKeys.Contains(item.NodeKey))
            .ToListAsync(cancellationToken);
        var normalizedStatus = status.Trim();
        var updatedAtUtc = clock.GetUtcNow();

        foreach (var node in nodes)
        {
            node.Status = normalizedStatus;
            var progress = ResolveStatusBackedProgress(normalizedStatus);
            node.ProgressMode = progress.Mode;
            node.ProgressPercent = progress.Percent;
            node.UpdatedAtUtc = updatedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return nodes.Select(node => MapStructureNode(node)).ToList();
    }

    public async Task<int> UpdateObjectStatusesAsync(
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys,
        string status,
        CancellationToken cancellationToken = default)
        => (await UpdateObjectStatusesDetailedAsync(projectId, nodeKeys, status, cancellationToken)).Count;

    public async Task<IReadOnlyList<ProjectStructureNode>> UpdateObjectProgressDetailedAsync(
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys,
        string progressMode,
        int progressPercent,
        CancellationToken cancellationToken = default)
    {
        if (nodeKeys.Count == 0)
        {
            return [];
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var normalizedKeys = nodeKeys.Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.Ordinal).ToList();
        if (normalizedKeys.Count == 0)
        {
            return [];
        }

        var nodes = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId && normalizedKeys.Contains(item.NodeKey))
            .ToListAsync(cancellationToken);

        var normalizedMode = NormalizeProgressMode(progressMode);
        var normalizedPercent = Math.Clamp(progressPercent, 0, 100);
        var updatedAtUtc = clock.GetUtcNow();
        foreach (var node in nodes)
        {
            node.ProgressMode = normalizedMode;
            node.ProgressPercent = normalizedPercent;
            node.UpdatedAtUtc = updatedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return nodes.Select(node => MapStructureNode(node)).ToList();
    }

    public async Task<int> UpdateObjectProgressAsync(
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys,
        string progressMode,
        int progressPercent,
        CancellationToken cancellationToken = default)
        => (await UpdateObjectProgressDetailedAsync(projectId, nodeKeys, progressMode, progressPercent, cancellationToken)).Count;

    public async Task<IReadOnlyList<ProjectStructureNode>> UpdateObjectMarkerDetailedAsync(
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys,
        string markerIcon,
        string markerTone,
        string markerLabel,
        CancellationToken cancellationToken = default)
        => await UpdateObjectMarkersDetailedAsync(
            projectId,
            nodeKeys,
            markerIcon,
            markerTone,
            markerLabel,
            ProjectMarkerMutationMode.ReplaceAll,
            cancellationToken);

    public async Task<IReadOnlyList<ProjectStructureNode>> AddObjectMarkerDetailedAsync(
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys,
        string markerIcon,
        string markerTone,
        string markerLabel,
        CancellationToken cancellationToken = default)
        => await UpdateObjectMarkersDetailedAsync(
            projectId,
            nodeKeys,
            markerIcon,
            markerTone,
            markerLabel,
            ProjectMarkerMutationMode.Add,
            cancellationToken);

    public async Task<IReadOnlyList<ProjectStructureNode>> ToggleObjectMarkerDetailedAsync(
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys,
        string markerIcon,
        string markerTone,
        string markerLabel,
        CancellationToken cancellationToken = default)
        => await UpdateObjectMarkersDetailedAsync(
            projectId,
            nodeKeys,
            markerIcon,
            markerTone,
            markerLabel,
            ProjectMarkerMutationMode.Toggle,
            cancellationToken);

    public async Task<IReadOnlyList<ProjectStructureNode>> RemoveObjectMarkerDetailedAsync(
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys,
        string markerIcon,
        string markerTone,
        string markerLabel,
        CancellationToken cancellationToken = default)
        => await UpdateObjectMarkersDetailedAsync(
            projectId,
            nodeKeys,
            markerIcon,
            markerTone,
            markerLabel,
            ProjectMarkerMutationMode.Remove,
            cancellationToken);

    public async Task<IReadOnlyList<ProjectStructureNode>> ClearObjectMarkersDetailedAsync(
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys,
        CancellationToken cancellationToken = default)
        => await UpdateObjectMarkersDetailedAsync(
            projectId,
            nodeKeys,
            string.Empty,
            string.Empty,
            string.Empty,
            ProjectMarkerMutationMode.ClearAll,
            cancellationToken);

    private async Task<IReadOnlyList<ProjectStructureNode>> UpdateObjectMarkersDetailedAsync(
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys,
        string markerIcon,
        string markerTone,
        string markerLabel,
        ProjectMarkerMutationMode mutationMode,
        CancellationToken cancellationToken = default)
    {
        if (nodeKeys.Count == 0)
        {
            return [];
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var normalizedKeys = nodeKeys.Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.Ordinal).ToList();
        if (normalizedKeys.Count == 0)
        {
            return [];
        }

        var nodes = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId && normalizedKeys.Contains(item.NodeKey))
            .ToListAsync(cancellationToken);
        var normalizedMarker = ProjectObjectMetadataSerializer.NormalizeMarker(markerIcon, markerTone, markerLabel);
        var updatedAtUtc = clock.GetUtcNow();

        foreach (var node in nodes)
        {
            var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
            var existingMarkers = ProjectObjectMetadataSerializer.ResolveMarkers(metadata, node.MarkerIcon, node.MarkerTone, node.MarkerLabel);
            var updatedMarkers = mutationMode switch
            {
                ProjectMarkerMutationMode.ReplaceAll => normalizedMarker is null ? [] : [normalizedMarker],
                ProjectMarkerMutationMode.Add => AddMarker(existingMarkers, normalizedMarker),
                ProjectMarkerMutationMode.Remove => RemoveMarker(existingMarkers, normalizedMarker),
                ProjectMarkerMutationMode.Toggle => ToggleMarker(existingMarkers, normalizedMarker),
                ProjectMarkerMutationMode.ClearAll => [],
                _ => existingMarkers
            };
            ProjectObjectMetadataSerializer.SetMarkers(metadata, updatedMarkers);
            node.MetadataJson = ProjectObjectMetadataSerializer.Serialize(metadata);
            ApplyPrimaryMarker(node, updatedMarkers);
            node.UpdatedAtUtc = updatedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return nodes.Select(node => MapStructureNode(node)).ToList();
    }

    public async Task<int> UpdateObjectMarkerAsync(
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys,
        string markerIcon,
        string markerTone,
        string markerLabel,
        CancellationToken cancellationToken = default)
        => (await UpdateObjectMarkerDetailedAsync(projectId, nodeKeys, markerIcon, markerTone, markerLabel, cancellationToken)).Count;

    public async Task<int> AddObjectMarkerAsync(
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys,
        string markerIcon,
        string markerTone,
        string markerLabel,
        CancellationToken cancellationToken = default)
        => (await AddObjectMarkerDetailedAsync(projectId, nodeKeys, markerIcon, markerTone, markerLabel, cancellationToken)).Count;

    public async Task<int> ToggleObjectMarkerAsync(
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys,
        string markerIcon,
        string markerTone,
        string markerLabel,
        CancellationToken cancellationToken = default)
        => (await ToggleObjectMarkerDetailedAsync(projectId, nodeKeys, markerIcon, markerTone, markerLabel, cancellationToken)).Count;

    public async Task<int> RemoveObjectMarkerAsync(
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys,
        string markerIcon,
        string markerTone,
        string markerLabel,
        CancellationToken cancellationToken = default)
        => (await RemoveObjectMarkerDetailedAsync(projectId, nodeKeys, markerIcon, markerTone, markerLabel, cancellationToken)).Count;

    public async Task<int> ClearObjectMarkersAsync(
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys,
        CancellationToken cancellationToken = default)
        => (await ClearObjectMarkersDetailedAsync(projectId, nodeKeys, cancellationToken)).Count;

    private static IReadOnlyList<ProjectNodeMarker> AddMarker(
        IReadOnlyList<ProjectNodeMarker> existingMarkers,
        ProjectNodeMarker? marker)
    {
        if (marker is null)
        {
            return existingMarkers;
        }

        var updated = existingMarkers
            .Where(existing => !string.Equals(existing.Icon, marker.Icon, StringComparison.OrdinalIgnoreCase))
            .ToList();
        updated.Add(marker);
        return updated;
    }

    private static IReadOnlyList<ProjectNodeMarker> ToggleMarker(
        IReadOnlyList<ProjectNodeMarker> existingMarkers,
        ProjectNodeMarker? marker)
    {
        if (marker is null)
        {
            return existingMarkers;
        }

        var hasMarker = existingMarkers.Any(existing => string.Equals(existing.Icon, marker.Icon, StringComparison.OrdinalIgnoreCase));
        if (hasMarker)
        {
            return existingMarkers
                .Where(existing => !string.Equals(existing.Icon, marker.Icon, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return AddMarker(existingMarkers, marker);
    }

    private static IReadOnlyList<ProjectNodeMarker> RemoveMarker(
        IReadOnlyList<ProjectNodeMarker> existingMarkers,
        ProjectNodeMarker? marker)
    {
        if (marker is null)
        {
            return existingMarkers;
        }

        return existingMarkers
            .Where(existing => !string.Equals(existing.Icon, marker.Icon, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static void ApplyPrimaryMarker(ProjectObjectRecord node, IReadOnlyList<ProjectNodeMarker> markers)
    {
        var primaryMarker = ProjectObjectMetadataSerializer.ResolvePrimaryMarker(markers);
        node.MarkerIcon = primaryMarker?.Icon ?? string.Empty;
        node.MarkerTone = primaryMarker?.Tone ?? string.Empty;
        node.MarkerLabel = primaryMarker?.Label ?? string.Empty;
    }

    public async Task<IReadOnlyList<ProjectStructureNode>> UpdateObjectPriorityDetailedAsync(
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys,
        int priority,
        CancellationToken cancellationToken = default)
    {
        if (nodeKeys.Count == 0)
        {
            return [];
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var normalizedKeys = nodeKeys.Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.Ordinal).ToList();
        if (normalizedKeys.Count == 0)
        {
            return [];
        }

        var nodes = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId && normalizedKeys.Contains(item.NodeKey))
            .ToListAsync(cancellationToken);

        var normalizedPriority = Math.Clamp(priority, 0, 6);
        var updatedAtUtc = clock.GetUtcNow();
        foreach (var node in nodes)
        {
            node.Priority = normalizedPriority;
            node.UpdatedAtUtc = updatedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return nodes.Select(node => MapStructureNode(node)).ToList();
    }

    public async Task<int> UpdateObjectPriorityAsync(
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys,
        int priority,
        CancellationToken cancellationToken = default)
        => (await UpdateObjectPriorityDetailedAsync(projectId, nodeKeys, priority, cancellationToken)).Count;

    public async Task SaveViewStateAsync(Guid projectId, string surfaceKind, string stateJson, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var record = await dbContext.Set<ProjectWorkbenchViewStateRecord>()
            .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.SurfaceKind == surfaceKind, cancellationToken);
        if (record is null)
        {
            record = new ProjectWorkbenchViewStateRecord
            {
                ProjectId = projectId,
                SurfaceKind = surfaceKind
            };

            await dbContext.Set<ProjectWorkbenchViewStateRecord>().AddAsync(record, cancellationToken);
        }

        record.StateJson = string.IsNullOrWhiteSpace(stateJson) ? "{}" : stateJson;
        record.UpdatedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ArtifactReference?> ExecuteNodeCommandAsync(Guid projectId, string nodeKey, ProjectStructureCommandKind commandKind, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        await SyncGraphAsync(dbContext, projectId, cancellationToken);

        var node = await dbContext.Set<ProjectObjectRecord>()
            .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.NodeKey == nodeKey, cancellationToken);
        if (node is null)
        {
            return null;
        }

        if (node.ObjectType == ProjectObjectType.PromptFlow &&
            commandKind is ProjectStructureCommandKind.Open or ProjectStructureCommandKind.Wizard)
        {
            var artifact = await EnsurePromptFlowWizardAsync(dbContext, projectId, node, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return artifact;
        }

        if (string.Equals(node.ExternalArtifactKind, "prompt-node", StringComparison.OrdinalIgnoreCase) &&
            node.ExternalArtifactId.HasValue)
        {
            var promptNode = await dbContext.Set<PromptRunNode>().FirstOrDefaultAsync(item => item.Id == node.ExternalArtifactId.Value, cancellationToken);
            if (promptNode is null)
            {
                return null;
            }

            switch (commandKind)
            {
                case ProjectStructureCommandKind.Branch:
                    var branchNode = new PromptRunNode
                    {
                        PromptRunId = promptNode.PromptRunId,
                        PromptBlockDefinitionId = promptNode.PromptBlockDefinitionId,
                        ParentPromptRunNodeId = promptNode.Id,
                        Title = $"{promptNode.Title} follow-up",
                        BranchKey = $"branch-{clock.GetUtcNow():yyyyMMddHHmmss}",
                        BranchLabel = "Workbench follow-up",
                        Sequence = promptNode.Sequence + 1,
                        State = PromptRunNodeState.Pending,
                        Notes = "Created from the structure canvas branch action."
                    };
                    await dbContext.Set<PromptRunNode>().AddAsync(branchNode, cancellationToken);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await SyncGraphAsync(dbContext, projectId, cancellationToken);
                    return new ArtifactReference(
                        "prompt-session",
                        branchNode.PromptRunId,
                        "Prompt Session",
                        $"/prompt-factory?runId={branchNode.PromptRunId}",
                        "Prompt branch session",
                        projectId,
                        $"prompt-session:{branchNode.PromptRunId:N}",
                        TabKind: WorkbenchTabKinds.PromptWizardSession);
                case ProjectStructureCommandKind.Skip:
                    promptNode.State = PromptRunNodeState.Skipped;
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await SyncGraphAsync(dbContext, projectId, cancellationToken);
                    return null;
                case ProjectStructureCommandKind.MarkUsed:
                    promptNode.State = PromptRunNodeState.Used;
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await SyncGraphAsync(dbContext, projectId, cancellationToken);
                    return null;
            }
        }

        return commandKind switch
        {
            ProjectStructureCommandKind.Validate => new ArtifactReference("validation", null, "Validation Center", $"/validation?projectId={projectId}", "Project validation workspace", projectId),
            ProjectStructureCommandKind.Test => new ArtifactReference("test-plan", null, "Test Lab", $"/test-lab?projectId={projectId}", "Project test planning workspace", projectId),
            ProjectStructureCommandKind.Open => BuildArtifactReference(node, projectId),
            _ => BuildArtifactReference(node, projectId)
        };
    }

    private async Task<ArtifactReference?> EnsurePromptFlowWizardAsync(
        AppDbContext dbContext,
        Guid projectId,
        ProjectObjectRecord node,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(node.Route) &&
            node.Route.StartsWith("/prompt-factory", StringComparison.OrdinalIgnoreCase))
        {
            node.ExternalArtifactKind = "prompt-session";
            if (!node.ExternalArtifactId.HasValue &&
                TryResolvePromptFactorySessionId(node.Route, out var existingSessionId))
            {
                node.ExternalArtifactId = existingSessionId;
            }

            node.UpdatedAtUtc = clock.GetUtcNow();
            return BuildArtifactReference(node, projectId);
        }

        var phase = await ResolvePromptFlowPhaseAsync(dbContext, projectId, node, cancellationToken);
        var sessionId = await promptFactoryService.CreateBlankProjectSessionAsync(projectId, node.Title, phase, cancellationToken);
        node.Route = $"/prompt-factory?sessionId={sessionId}";
        node.ExternalArtifactKind = "prompt-session";
        node.ExternalArtifactId = sessionId;
        node.UpdatedAtUtc = clock.GetUtcNow();
        return BuildArtifactReference(node, projectId);
    }

    private static ArtifactReference? BuildArtifactReference(ProjectObjectRecord node, Guid projectId)
    {
        if (string.IsNullOrWhiteSpace(node.Route))
        {
            return null;
        }

        var tabKind = node.ObjectType switch
        {
            ProjectObjectType.ProjectRoot => WorkbenchTabKinds.ProjectOverview,
            ProjectObjectType.PromptFlow or ProjectObjectType.PromptSession or ProjectObjectType.PromptStep => WorkbenchTabKinds.PromptWizardSession,
            ProjectObjectType.ValidationRun => WorkbenchTabKinds.ValidationRun,
            ProjectObjectType.TestPlan or ProjectObjectType.TestEvidence => WorkbenchTabKinds.TestPlan,
            _ when node.Route.EndsWith("/structure", StringComparison.OrdinalIgnoreCase) => WorkbenchTabKinds.ProjectStructure,
            _ when node.Route.EndsWith("/calendar", StringComparison.OrdinalIgnoreCase) => WorkbenchTabKinds.ProjectCalendar,
            _ => WorkbenchTabKinds.Page
        };

        return new ArtifactReference(
            node.ExternalArtifactKind,
            node.ExternalArtifactId,
            node.Title,
            node.Route,
            node.Notes,
            projectId,
            node.NodeKey,
            TabKind: tabKind);
    }

    private async Task<SavedMediaDescriptor?> SaveMediaAsync(
        Guid projectId,
        ProjectObjectType objectType,
        ProjectObjectMediaPayload? media,
        CancellationToken cancellationToken)
    {
        if (media is null || string.IsNullOrWhiteSpace(media.Base64Data) || string.IsNullOrWhiteSpace(media.FileName))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(media.Base64Data);
        }
        catch
        {
            return null;
        }

        var extension = Path.GetExtension(media.FileName);
        var safeExtension = string.IsNullOrWhiteSpace(extension)
            ? objectType == ProjectObjectType.ImageAsset ? ".png" : ".bin"
            : extension;
        var safeFileName = $"{SanitizeSlug(Path.GetFileNameWithoutExtension(media.FileName))}-{Guid.NewGuid():N}{safeExtension}";
        var category = objectType switch
        {
            ProjectObjectType.ImageAsset => "project-media/images",
            ProjectObjectType.VideoAsset => "project-media/videos",
            _ => "project-media/files"
        };
        var relativePath = Path.Combine("managed-files", category, projectId.ToString("N"), safeFileName)
            .Replace('\\', '/');
        await fileStore.SaveBytesAsync(relativePath, bytes, cancellationToken);

        return new SavedMediaDescriptor(
            relativePath,
            $"/{relativePath}",
            media.ContentType,
            media.FileName,
            objectType.ToString());
    }

    private async Task SyncGraphAsync(AppDbContext dbContext, Guid projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Set<Project>().FirstAsync(item => item.Id == projectId, cancellationToken);
        var phases = await dbContext.Set<ProjectPhase>()
            .Where(item => item.ProjectId == projectId)
            .OrderBy(item => item.OrderIndex)
            .ToListAsync(cancellationToken);
        var resources = await dbContext.Set<ProjectResource>()
            .Where(item => item.ProjectId == projectId)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
        var runs = (await dbContext.Set<PromptRun>()
            .Where(item => item.ProjectId == projectId)
            .ToListAsync(cancellationToken))
            .OrderBy(item => item.CreatedAtUtc)
            .ToList();
        var runIds = runs.Select(item => item.Id).ToArray();
        var runNodes = runIds.Length == 0
            ? []
            : await dbContext.Set<PromptRunNode>()
                .Where(item => runIds.Contains(item.PromptRunId))
                .OrderBy(item => item.Sequence)
                .ToListAsync(cancellationToken);
        var validations = (await dbContext.Set<ValidationRun>()
            .Where(item => item.ProjectId == projectId)
            .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();
        var testPlans = (await dbContext.Set<TestPlan>()
            .Where(item => item.ProjectId == projectId)
            .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();
        var allProjects = await dbContext.Set<Project>().ToListAsync(cancellationToken);
        var allHierarchyLinks = await dbContext.Set<ProjectHierarchyLink>().ToListAsync(cancellationToken);
        var hierarchyProjection = BuildProjectHierarchyProjection(project, allProjects, allHierarchyLinks, clock.GetUtcNow());

        var expectedNodes = new List<ProjectObjectRecord>
        {
            new()
            {
                ProjectId = projectId,
                NodeKey = BuildProjectRootNodeKey(project.Id),
                ObjectType = ProjectObjectType.ProjectRoot,
                Title = project.Name,
                Subtitle = project.Objective,
                Status = project.Status.ToString(),
                Notes = project.Description,
                Route = $"/projects?projectId={project.Id}",
                ExternalArtifactKind = "project",
                ExternalArtifactId = project.Id,
                PositionX = 140,
                PositionY = 240,
                IsSystemManaged = true,
                CreatedAtUtc = project.CreatedAtUtc,
                UpdatedAtUtc = clock.GetUtcNow()
            }
        };

        expectedNodes.AddRange(hierarchyProjection.ParentNodes);
        expectedNodes.AddRange(hierarchyProjection.DescendantNodes);

        expectedNodes.AddRange(phases.Select((phase, index) => new ProjectObjectRecord
        {
            ProjectId = projectId,
            NodeKey = $"phase:{phase.Id}",
            ObjectType = ProjectObjectType.Phase,
            Title = phase.Name,
            Subtitle = phase.Goal,
            Status = phase.Status.ToString(),
            Notes = phase.Goal,
            Route = $"/projects?projectId={projectId}",
            ExternalArtifactKind = "phase",
            ExternalArtifactId = phase.Id,
            ParentNodeKey = BuildProjectRootNodeKey(project.Id),
            PositionX = 420,
            PositionY = 120 + (index * 180),
            StartUtc = phase.StartDateUtc.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(phase.StartDateUtc.Value, DateTimeKind.Utc)) : null,
            EndUtc = phase.EndDateUtc.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(phase.EndDateUtc.Value, DateTimeKind.Utc)) : null,
            IsSystemManaged = true,
            CreatedAtUtc = project.CreatedAtUtc,
            UpdatedAtUtc = clock.GetUtcNow()
        }));

        expectedNodes.AddRange(resources.Select((resource, index) => new ProjectObjectRecord
        {
            ProjectId = projectId,
            NodeKey = $"resource:{resource.Id}",
            ObjectType = MapResourceKind(resource.ResourceKind),
            Title = resource.Name,
            Subtitle = resource.LocationOrIdentifier,
            Status = resource.ValidationStatus.ToString(),
            Notes = resource.Description,
            Route = $"/resources?resourceId={resource.Id}",
            ExternalArtifactKind = "resource",
            ExternalArtifactId = resource.Id,
            ParentNodeKey = BuildProjectRootNodeKey(project.Id),
            PositionX = 760,
            PositionY = 100 + (index * 120),
            IsSystemManaged = true,
            CreatedAtUtc = resource.CreatedAtUtc,
            UpdatedAtUtc = clock.GetUtcNow()
        }));

        expectedNodes.AddRange(runs.Select((run, index) => new ProjectObjectRecord
        {
            ProjectId = projectId,
            NodeKey = $"prompt-run:{run.Id}",
            ObjectType = ProjectObjectType.PromptSession,
            Title = run.Name,
            Subtitle = run.Phase,
            Status = "Active",
            Notes = run.Phase,
            Route = $"/prompt-factory?runId={run.Id}",
            ExternalArtifactKind = "prompt-run",
            ExternalArtifactId = run.Id,
            ParentNodeKey = phases.FirstOrDefault(phase => string.Equals(phase.Name, run.Phase, StringComparison.OrdinalIgnoreCase)) is { } phase
                ? $"phase:{phase.Id}"
                : BuildProjectRootNodeKey(project.Id),
            PositionX = 1080,
            PositionY = 100 + (index * 160),
            IsSystemManaged = true,
            CreatedAtUtc = run.CreatedAtUtc,
            UpdatedAtUtc = clock.GetUtcNow()
        }));

        expectedNodes.AddRange(runNodes.Select((node, index) => new ProjectObjectRecord
        {
            ProjectId = projectId,
            NodeKey = $"prompt-node:{node.Id}",
            ObjectType = ProjectObjectType.PromptStep,
            Title = node.Title,
            Subtitle = node.BranchLabel,
            Status = node.State.ToString(),
            Notes = node.Notes,
            Route = node.PromptArtifactId.HasValue ? $"/prompt-gallery?promptId={node.PromptArtifactId}" : $"/prompt-factory?runId={node.PromptRunId}",
            ExternalArtifactKind = "prompt-node",
            ExternalArtifactId = node.Id,
            ParentNodeKey = node.ParentPromptRunNodeId.HasValue ? $"prompt-node:{node.ParentPromptRunNodeId.Value}" : $"prompt-run:{node.PromptRunId}",
            PositionX = 1400,
            PositionY = 100 + (index * 120),
            IsSystemManaged = true,
            CreatedAtUtc = clock.GetUtcNow(),
            UpdatedAtUtc = clock.GetUtcNow()
        }));

        expectedNodes.AddRange(validations.Select((validation, index) => new ProjectObjectRecord
        {
            ProjectId = projectId,
            NodeKey = $"validation:{validation.Id}",
            ObjectType = ProjectObjectType.ValidationRun,
            Title = validation.ArtifactTitle,
            Subtitle = validation.ValidationType.ToString(),
            Status = validation.Decision.ToString(),
            Notes = validation.Summary,
            Route = $"/validation?runId={validation.Id}",
            ExternalArtifactKind = "validation-run",
            ExternalArtifactId = validation.Id,
            ParentNodeKey = BuildProjectRootNodeKey(project.Id),
            PositionX = 780,
            PositionY = 580 + (index * 120),
            StartUtc = validation.UpdatedAtUtc,
            EndUtc = validation.UpdatedAtUtc.AddHours(1),
            IsSystemManaged = true,
            CreatedAtUtc = validation.CreatedAtUtc,
            UpdatedAtUtc = clock.GetUtcNow()
        }));

        expectedNodes.AddRange(testPlans.Select((testPlan, index) => new ProjectObjectRecord
        {
            ProjectId = projectId,
            NodeKey = $"test-plan:{testPlan.Id}",
            ObjectType = ProjectObjectType.TestPlan,
            Title = testPlan.Title,
            Subtitle = testPlan.Phase,
            Status = "Planned",
            Notes = testPlan.CoverageGoal,
            Route = $"/test-lab?planId={testPlan.Id}",
            ExternalArtifactKind = "test-plan",
            ExternalArtifactId = testPlan.Id,
            ParentNodeKey = BuildProjectRootNodeKey(project.Id),
            PositionX = 1100,
            PositionY = 620 + (index * 140),
            StartUtc = testPlan.UpdatedAtUtc,
            EndUtc = testPlan.UpdatedAtUtc.AddHours(1),
            IsSystemManaged = true,
            CreatedAtUtc = testPlan.CreatedAtUtc,
            UpdatedAtUtc = clock.GetUtcNow()
        }));

        var expectedLinks = new List<(string Source, string Target, ProjectObjectLinkKind Kind)>();
        expectedLinks.AddRange(hierarchyProjection.Links);
        expectedLinks.AddRange(phases.Select(phase => (BuildProjectRootNodeKey(project.Id), $"phase:{phase.Id}", ProjectObjectLinkKind.Contains)));
        expectedLinks.AddRange(resources.Select(resource => (BuildProjectRootNodeKey(project.Id), $"resource:{resource.Id}", ProjectObjectLinkKind.Uses)));
        expectedLinks.AddRange(validations.Select(validation => (BuildProjectRootNodeKey(project.Id), $"validation:{validation.Id}", ProjectObjectLinkKind.Validates)));
        expectedLinks.AddRange(testPlans.Select(testPlan => (BuildProjectRootNodeKey(project.Id), $"test-plan:{testPlan.Id}", ProjectObjectLinkKind.Tests)));

        foreach (var run in runs)
        {
            var phaseNodeKey = phases.FirstOrDefault(phase => string.Equals(phase.Name, run.Phase, StringComparison.OrdinalIgnoreCase)) is { } phase
                ? $"phase:{phase.Id}"
                : BuildProjectRootNodeKey(project.Id);
            expectedLinks.Add((phaseNodeKey, $"prompt-run:{run.Id}", ProjectObjectLinkKind.BelongsTo));
        }

        expectedLinks.AddRange(runNodes.Select(node => (
            node.ParentPromptRunNodeId.HasValue ? $"prompt-node:{node.ParentPromptRunNodeId.Value}" : $"prompt-run:{node.PromptRunId}",
            $"prompt-node:{node.Id}",
            node.ParentPromptRunNodeId.HasValue ? ProjectObjectLinkKind.DerivedFrom : ProjectObjectLinkKind.Contains)));

        var existingNodes = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId)
            .ToDictionaryAsync(item => item.NodeKey, cancellationToken);
        var expectedNodeKeys = expectedNodes.Select(item => item.NodeKey).ToHashSet(StringComparer.Ordinal);

        foreach (var expectedNode in expectedNodes)
        {
            if (existingNodes.TryGetValue(expectedNode.NodeKey, out var existing))
            {
                existing.ObjectType = expectedNode.ObjectType;
                existing.Title = expectedNode.Title;
                existing.Subtitle = expectedNode.Subtitle;
                existing.Status = expectedNode.Status;
                existing.Notes = expectedNode.Notes;
                existing.Route = expectedNode.Route;
                existing.ExternalArtifactKind = expectedNode.ExternalArtifactKind;
                existing.ExternalArtifactId = expectedNode.ExternalArtifactId;
                existing.ParentNodeKey = expectedNode.ParentNodeKey;
                existing.StartUtc = expectedNode.StartUtc;
                existing.EndUtc = expectedNode.EndUtc;
                existing.IsSystemManaged = true;
                existing.UpdatedAtUtc = clock.GetUtcNow();
                if (existing.PositionX == default && existing.PositionY == default)
                {
                    existing.PositionX = expectedNode.PositionX;
                    existing.PositionY = expectedNode.PositionY;
                }
            }
            else
            {
                await dbContext.Set<ProjectObjectRecord>().AddAsync(expectedNode, cancellationToken);
            }
        }

        var staleSystemNodes = existingNodes.Values.Where(item => item.IsSystemManaged && !expectedNodeKeys.Contains(item.NodeKey)).ToList();
        if (staleSystemNodes.Count > 0)
        {
            dbContext.RemoveRange(staleSystemNodes);
        }

        var existingLinks = await dbContext.Set<ProjectObjectLinkRecord>()
            .Where(item => item.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var expectedLinkKeys = expectedLinks
            .Select(link => $"{link.Source}|{link.Target}|{link.Kind}")
            .ToHashSet(StringComparer.Ordinal);

        foreach (var expectedLink in expectedLinks)
        {
            await UpsertLinkAsync(dbContext, projectId, expectedLink.Source, expectedLink.Target, expectedLink.Kind, isSystemManaged: true, cancellationToken);
        }

        var staleLinks = existingLinks
            .Where(item => item.IsSystemManaged && !expectedLinkKeys.Contains($"{item.SourceNodeKey}|{item.TargetNodeKey}|{item.LinkKind}"))
            .ToList();
        if (staleLinks.Count > 0)
        {
            dbContext.RemoveRange(staleLinks);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string BuildProjectRootNodeKey(Guid projectId)
        => $"{ProjectRootNodePrefix}{projectId}";

    private static string NormalizeEditableParentNodeKey(Guid projectId, string? parentNodeKey)
        => string.IsNullOrWhiteSpace(parentNodeKey)
            ? BuildProjectRootNodeKey(projectId)
            : parentNodeKey.Trim();

    private static ProjectObjectLinkKind ResolveHierarchyLinkKind(Guid projectId, string parentNodeKey)
        => string.Equals(parentNodeKey, BuildProjectRootNodeKey(projectId), StringComparison.Ordinal)
            ? ProjectObjectLinkKind.Contains
            : ProjectObjectLinkKind.BelongsTo;

    private static string BuildProjectChildNodeKey(Guid projectId)
        => $"{ProjectChildNodePrefix}{projectId}";

    private static string BuildRelatedParentNodeKey(Guid projectId)
        => $"{ProjectRelatedParentNodePrefix}{projectId}";

    private static string ResolveRelatedProjectSubtitle(Project project, string fallbackLabel)
        => string.IsNullOrWhiteSpace(project.CurrentPhase)
            ? fallbackLabel
            : project.CurrentPhase.Trim();

    private static string ResolveRelatedProjectNotes(Project project, string fallbackLabel)
    {
        if (!string.IsNullOrWhiteSpace(project.Description))
        {
            return project.Description;
        }

        if (!string.IsNullOrWhiteSpace(project.Objective))
        {
            return project.Objective;
        }

        return fallbackLabel;
    }

    private static ProjectHierarchyProjection BuildProjectHierarchyProjection(
        Project project,
        IReadOnlyList<Project> allProjects,
        IReadOnlyList<ProjectHierarchyLink> allHierarchyLinks,
        DateTimeOffset updatedAtUtc)
    {
        var projectMap = allProjects.ToDictionary(item => item.Id);
        var childProjectIdsByParent = allHierarchyLinks
            .GroupBy(link => link.ParentProjectId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(link => link.ChildProjectId)
                    .Distinct()
                    .ToList());
        var parentProjectIdsByChild = allHierarchyLinks
            .GroupBy(link => link.ChildProjectId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(link => link.ParentProjectId)
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
                    Route = $"/projects/{descendantProject.Id}/structure",
                    ExternalArtifactKind = "project",
                    ExternalArtifactId = descendantProject.Id,
                    ParentNodeKey = parentProjectId == project.Id
                        ? BuildProjectRootNodeKey(project.Id)
                        : BuildProjectChildNodeKey(parentProjectId),
                    PositionX = position.X,
                    PositionY = position.Y,
                    IsSystemManaged = true,
                    CreatedAtUtc = descendantProject.CreatedAtUtc,
                    UpdatedAtUtc = updatedAtUtc
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
            var sourceNodeKey = ResolveProjectHierarchyLinkSourceNodeKey(
                link.ParentProjectId,
                project.Id,
                visibleDescendantProjectIds);
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

    private static HashSet<Guid> CollectDescendantProjectIds(
        Guid projectId,
        IReadOnlyDictionary<Guid, List<Guid>> childProjectIdsByParent)
    {
        var descendants = new HashSet<Guid>();
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
                if (!descendants.Add(childProjectId))
                {
                    continue;
                }

                queue.Enqueue(childProjectId);
            }
        }

        return descendants;
    }

    private static ProjectObjectRecord CreateRelatedParentNode(
        Guid projectId,
        Project parentProject,
        string fallbackLabel,
        double x,
        double y,
        DateTimeOffset updatedAtUtc)
        => new()
        {
            ProjectId = projectId,
            NodeKey = BuildRelatedParentNodeKey(parentProject.Id),
            ObjectType = ProjectObjectType.ProjectRoot,
            Title = parentProject.Name,
            Subtitle = ResolveRelatedProjectSubtitle(parentProject, fallbackLabel),
            Status = parentProject.Status.ToString(),
            Notes = ResolveRelatedProjectNotes(parentProject, fallbackLabel),
            Route = $"/projects/{parentProject.Id}/structure",
            ExternalArtifactKind = "project",
            ExternalArtifactId = parentProject.Id,
            PositionX = x,
            PositionY = y,
            IsSystemManaged = true,
            CreatedAtUtc = parentProject.CreatedAtUtc,
            UpdatedAtUtc = updatedAtUtc
        };

    private static string ResolveProjectHierarchyLinkSourceNodeKey(
        Guid parentProjectId,
        Guid rootProjectId,
        IReadOnlySet<Guid> visibleDescendantProjectIds)
        => parentProjectId == rootProjectId
            ? BuildProjectRootNodeKey(rootProjectId)
            : visibleDescendantProjectIds.Contains(parentProjectId)
                ? BuildProjectChildNodeKey(parentProjectId)
                : BuildRelatedParentNodeKey(parentProjectId);

    private static string ResolveProjectSortKey(Project project)
        => string.IsNullOrWhiteSpace(project.Name)
            ? project.Id.ToString("N")
            : project.Name.Trim();

    private sealed record ProjectHierarchyProjection(
        IReadOnlyList<ProjectObjectRecord> ParentNodes,
        IReadOnlyList<ProjectObjectRecord> DescendantNodes,
        IReadOnlyList<(string Source, string Target, ProjectObjectLinkKind Kind)> Links);

    private static async Task UpsertLinkAsync(
        AppDbContext dbContext,
        Guid projectId,
        string sourceNodeKey,
        string targetNodeKey,
        ProjectObjectLinkKind linkKind,
        bool isSystemManaged,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Set<ProjectObjectLinkRecord>()
            .FirstOrDefaultAsync(item =>
                item.ProjectId == projectId &&
                item.SourceNodeKey == sourceNodeKey &&
                item.TargetNodeKey == targetNodeKey &&
                item.LinkKind == linkKind &&
                item.IsSystemManaged == isSystemManaged,
                cancellationToken);

        if (existing is not null)
        {
            return;
        }

        await dbContext.Set<ProjectObjectLinkRecord>().AddAsync(new ProjectObjectLinkRecord
        {
            ProjectId = projectId,
            SourceNodeKey = sourceNodeKey,
            TargetNodeKey = targetNodeKey,
            LinkKind = linkKind,
            IsSystemManaged = isSystemManaged,
            CreatedAtUtc = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private static async Task<string?> LoadViewStateAsync(AppDbContext dbContext, Guid projectId, string surfaceKind, CancellationToken cancellationToken)
        => await dbContext.Set<ProjectWorkbenchViewStateRecord>()
            .Where(item => item.ProjectId == projectId && item.SurfaceKind == surfaceKind)
            .Select(item => item.StateJson)
            .FirstOrDefaultAsync(cancellationToken);

    private static string ResolvePreferredCalendarView(string? viewStateJson)
    {
        if (string.IsNullOrWhiteSpace(viewStateJson))
        {
            return "month";
        }

        var marker = "\"preferredView\":\"";
        var start = viewStateJson.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return "month";
        }

        start += marker.Length;
        var end = viewStateJson.IndexOf('"', start);
        return end > start ? viewStateJson[start..end] : "month";
    }

    private static bool IsSupportedReclassification(ProjectObjectType currentType, ProjectObjectType targetType)
        => currentType switch
        {
            ProjectObjectType.ProjectBlock => targetType == ProjectObjectType.ProjectBlock,
            ProjectObjectType.Note => targetType == ProjectObjectType.ProjectBlock,
            _ => false
        };

    private static string RewriteProjectScopedRoute(string route, Guid sourceProjectId, Guid targetProjectId)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return route;
        }

        var sourceStructureRoute = $"/projects/{sourceProjectId}/structure";
        if (string.Equals(route, sourceStructureRoute, StringComparison.OrdinalIgnoreCase))
        {
            return $"/projects/{targetProjectId}/structure";
        }

        var sourceCalendarRoute = $"/projects/{sourceProjectId}/calendar";
        if (string.Equals(route, sourceCalendarRoute, StringComparison.OrdinalIgnoreCase))
        {
            return $"/projects/{targetProjectId}/calendar";
        }

        var sourceProjectQueryRoute = $"/projects?projectId={sourceProjectId}";
        if (string.Equals(route, sourceProjectQueryRoute, StringComparison.OrdinalIgnoreCase))
        {
            return $"/projects?projectId={targetProjectId}";
        }

        return route;
    }

    private static string ResolveMetadataJson(
        ProjectObjectType objectType,
        string? objectSubtype,
        string? metadataJson,
        SavedMediaDescriptor? media)
    {
        var metadata = string.IsNullOrWhiteSpace(metadataJson)
            ? new ProjectObjectMetadataEnvelope()
            : ProjectObjectMetadataSerializer.Parse(metadataJson);

        if (objectType is ProjectObjectType.File or ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset)
        {
            metadata.File ??= new ProjectFileMetadata();
            metadata.File.FileSubtype = objectType switch
            {
                ProjectObjectType.ImageAsset => ProjectFileSubtype.Image,
                ProjectObjectType.VideoAsset => ProjectFileSubtype.Video,
                _ when metadata.File.FileSubtype == ProjectFileSubtype.Unknown =>
                    ProjectObjectMetadataSerializer.InferFileSubtype(
                        objectSubtype ?? string.Empty,
                        media?.OriginalFileName ?? string.Empty,
                        media?.ContentType ?? string.Empty),
                _ => metadata.File.FileSubtype
            };
        }

        if (objectType == ProjectObjectType.Link && metadata.Link is null)
        {
            metadata.Link = new ProjectLinkMetadata();
        }

        ProjectObjectMetadataSerializer.Validate(objectType, objectSubtype ?? string.Empty, metadata);
        return ProjectObjectMetadataSerializer.Serialize(metadata);
    }

    private static IReadOnlyList<ProjectStructureNode> MapStructureNodes(
        IReadOnlyList<ProjectObjectRecord> records,
        IReadOnlyList<ProjectObjectLinkRecord> links)
    {
        var projectNodeKeys = records
            .Where(record => TryResolveProjectHierarchyNode(record.NodeKey, out _, out _))
            .Select(record => record.NodeKey)
            .ToHashSet(StringComparer.Ordinal);

        return records
            .Select(record =>
            {
                var projectRole = ProjectStructureProjectRole.None;
                Guid? relatedProjectId = null;
                var parentProjectCount = 0;

                if (TryResolveProjectHierarchyNode(record.NodeKey, out var nodeKind, out var projectId))
                {
                    relatedProjectId = projectId;
                    projectRole = nodeKind switch
                    {
                        ProjectHierarchyNodeKind.ActiveProject => ProjectStructureProjectRole.ActiveProject,
                        ProjectHierarchyNodeKind.Subproject => ProjectStructureProjectRole.Subproject,
                        ProjectHierarchyNodeKind.RelatedParent when links.Any(link =>
                            string.Equals(link.SourceNodeKey, record.NodeKey, StringComparison.Ordinal) &&
                            TryResolveProjectHierarchyNode(link.TargetNodeKey, out var targetKind, out _) &&
                            targetKind == ProjectHierarchyNodeKind.ActiveProject)
                            => ProjectStructureProjectRole.ParentProject,
                        ProjectHierarchyNodeKind.RelatedParent => ProjectStructureProjectRole.AdditionalParentProject,
                        _ => ProjectStructureProjectRole.None
                    };

                    if (projectRole == ProjectStructureProjectRole.Subproject)
                    {
                        parentProjectCount = links.Count(link =>
                            string.Equals(link.TargetNodeKey, record.NodeKey, StringComparison.Ordinal) &&
                            projectNodeKeys.Contains(link.SourceNodeKey));
                    }
                }

                return MapStructureNode(record, projectRole, relatedProjectId, parentProjectCount);
            })
            .ToList();
    }

    private static bool TryResolveProjectHierarchyNode(
        string nodeKey,
        out ProjectHierarchyNodeKind nodeKind,
        out Guid relatedProjectId)
    {
        nodeKind = ProjectHierarchyNodeKind.None;
        relatedProjectId = Guid.Empty;

        if (nodeKey.StartsWith(ProjectRootNodePrefix, StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(nodeKey[ProjectRootNodePrefix.Length..], out relatedProjectId))
        {
            nodeKind = ProjectHierarchyNodeKind.ActiveProject;
            return true;
        }

        if (nodeKey.StartsWith(ProjectChildNodePrefix, StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(nodeKey[ProjectChildNodePrefix.Length..], out relatedProjectId))
        {
            nodeKind = ProjectHierarchyNodeKind.Subproject;
            return true;
        }

        if (nodeKey.StartsWith(ProjectRelatedParentNodePrefix, StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(nodeKey[ProjectRelatedParentNodePrefix.Length..], out relatedProjectId))
        {
            nodeKind = ProjectHierarchyNodeKind.RelatedParent;
            return true;
        }

        return false;
    }

    private static ProjectStructureNode MapStructureNode(
        ProjectObjectRecord record,
        ProjectStructureProjectRole projectRole = ProjectStructureProjectRole.None,
        Guid? relatedProjectId = null,
        int parentProjectCount = 0)
    {
        var profile = projectRole switch
        {
            ProjectStructureProjectRole.Subproject => new ProjectObjectVisualProfile("hex", "#1d4ed8", "PR", "Project"),
            ProjectStructureProjectRole.ParentProject => new ProjectObjectVisualProfile("hex", "#334155", "PR", "Parent"),
            ProjectStructureProjectRole.AdditionalParentProject => new ProjectObjectVisualProfile("hex", "#94a3b8", "PR", "Parent"),
            _ => ResolveVisualProfile(record.ObjectType, record.ObjectSubtype, record.Status)
        };
        var badges = new List<string>();
        if (record.IsSystemManaged)
        {
            badges.Add("Synced");
        }

        if (record.StartUtc.HasValue)
        {
            badges.Add("Scheduled");
        }

        if (!string.IsNullOrWhiteSpace(record.ObjectSubtype))
        {
            badges.Add(ResolveSubtypeBadge(record.ObjectType, record.ObjectSubtype));
        }

        if (!string.IsNullOrWhiteSpace(record.MediaOriginalFileName))
        {
            badges.Add("Uploaded");
        }

        switch (projectRole)
        {
            case ProjectStructureProjectRole.Subproject:
                badges.Add("Subproject");
                if (parentProjectCount > 1)
                {
                    badges.Add($"{parentProjectCount} parents");
                }

                break;
            case ProjectStructureProjectRole.ParentProject:
                badges.Add("Parent");
                break;
            case ProjectStructureProjectRole.AdditionalParentProject:
                badges.Add("Shared parent");
                break;
        }

        var metadataJson = string.IsNullOrWhiteSpace(record.MetadataJson) ? "{}" : record.MetadataJson;
        var metadata = ProjectObjectMetadataSerializer.Parse(metadataJson);
        var markers = ProjectObjectMetadataSerializer.ResolveMarkers(metadata, record.MarkerIcon, record.MarkerTone, record.MarkerLabel);
        var primaryMarker = ProjectObjectMetadataSerializer.ResolvePrimaryMarker(markers);

        return new ProjectStructureNode(
            record.NodeKey,
            record.ParentNodeKey,
            record.ObjectType,
            record.ObjectSubtype,
            record.Title,
            record.Subtitle,
            record.Status,
            record.Notes,
            record.Route,
            record.ExternalArtifactKind,
            record.ExternalArtifactId,
            record.MediaRelativePath,
            record.MediaContentType,
            record.MediaOriginalFileName,
            record.PositionX,
            record.PositionY,
            profile,
            badges,
            string.IsNullOrWhiteSpace(record.ProgressMode) ? string.Empty : NormalizeProgressMode(record.ProgressMode),
            record.ProgressPercent,
            primaryMarker?.Icon ?? string.Empty,
            primaryMarker?.Tone ?? string.Empty,
            primaryMarker?.Label ?? string.Empty,
            markers,
            record.Priority,
            record.StartUtc,
            record.EndUtc,
            metadataJson,
            projectRole,
            relatedProjectId,
            parentProjectCount);
    }

    private static string ResolveSubtypeBadge(ProjectObjectType objectType, string objectSubtype) => objectType switch
    {
        ProjectObjectType.ProjectBlock => ResolveBlockSubtypeLabel(objectSubtype),
        ProjectObjectType.Meeting => ProjectStructureCanvasCatalog.ResolveNodeLabel(new ProjectStructureNode(string.Empty, null, ProjectObjectType.Meeting, objectSubtype, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, null, string.Empty, string.Empty, string.Empty, 0, 0, new ProjectObjectVisualProfile("rect", "#0f172a", "NT", "Note"), [], string.Empty, 0, string.Empty, string.Empty, string.Empty, [], 0)),
        ProjectObjectType.Participant => objectSubtype switch
        {
            "team-block" => "Team block",
            "team-section" => "Team section",
            "ai-agent" => "AI agent",
            _ => ProjectStructureCanvasCatalog.ResolveNodeLabel(new ProjectStructureNode(string.Empty, null, ProjectObjectType.Participant, objectSubtype, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, null, string.Empty, string.Empty, string.Empty, 0, 0, new ProjectObjectVisualProfile("rect", "#0f172a", "NT", "Note"), [], string.Empty, 0, string.Empty, string.Empty, string.Empty, [], 0))
        },
        ProjectObjectType.WorkItem or ProjectObjectType.Repository or ProjectObjectType.File or ProjectObjectType.Script or ProjectObjectType.Environment or ProjectObjectType.Infrastructure
            => ProjectStructureCanvasCatalog.ResolveNodeLabel(new ProjectStructureNode(string.Empty, null, objectType, objectSubtype, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, null, string.Empty, string.Empty, string.Empty, 0, 0, new ProjectObjectVisualProfile("rect", "#0f172a", "NT", "Note"), [], string.Empty, 0, string.Empty, string.Empty, string.Empty, [], 0)),
        _ => objectSubtype
    };

    private static ProjectObjectVisualProfile ResolveVisualProfile(ProjectObjectType objectType, string objectSubtype, string status) => objectType switch
    {
        ProjectObjectType.ProjectRoot => Profile("hex", "#0f172a", "PR", "Project", ProjectObjectPaletteKeys.Primary),
        ProjectObjectType.Phase => Profile("pill", "#2563eb", "PH", "Phase", ProjectObjectPaletteKeys.Info),
        ProjectObjectType.Milestone => Profile("diamond", "#d97706", "MS", "Milestone", ProjectObjectPaletteKeys.Warning),
        ProjectObjectType.ProjectBlock => ResolveProjectBlockVisualProfile(objectSubtype),
        ProjectObjectType.Meeting => objectSubtype switch
        {
            "onsite" => Profile("diamond", "#d97706", "ME", "Onsite", ProjectObjectPaletteKeys.Warning),
            _ => Profile("diamond", "#0ea5e9", "ME", "Meeting", ProjectObjectPaletteKeys.Info)
        },
        ProjectObjectType.Recording => Profile("pill", "#8b5cf6", "RC", "Recording", ProjectObjectPaletteKeys.Secondary),
        ProjectObjectType.Transcript => Profile("rect", "#14b8a6", "TR", "Transcript", ProjectObjectPaletteKeys.Success),
        ProjectObjectType.Participant => objectSubtype switch
        {
            "hr" => Profile("hex", "#38bdf8", "HR", "HR", ProjectObjectPaletteKeys.Info),
            "team-block" => Profile("hex", "#2563eb", "TB", "Team", ProjectObjectPaletteKeys.Info),
            "team-section" => Profile("hex", "#1d4ed8", "TS", "Section", ProjectObjectPaletteKeys.Info),
            "freelancer" => Profile("hex", "#a855f7", "FR", "Freelancer", ProjectObjectPaletteKeys.Secondary),
            "partner" => Profile("hex", "#16a34a", "PA", "Partner", ProjectObjectPaletteKeys.Success),
            "ai-agent" => Profile("hex", "#0f766e", "AI", "AI", ProjectObjectPaletteKeys.Success),
            _ => Profile("hex", "#475569", "PT", "Participant", ProjectObjectPaletteKeys.Primary)
        },
        ProjectObjectType.WorkItem => ResolveWorkItemVisualProfile(objectSubtype),
        ProjectObjectType.Repository => ResolveRepositoryVisualProfile(objectSubtype),
        ProjectObjectType.File => ResolveFileVisualProfile(objectSubtype),
        ProjectObjectType.ImageAsset => Profile("rect", "#ec4899", "IM", "Image", ProjectObjectPaletteKeys.Danger),
        ProjectObjectType.VideoAsset => Profile("rect", "#7c3aed", "VD", "Video", ProjectObjectPaletteKeys.Secondary),
        ProjectObjectType.Link => Profile("circle", "#38bdf8", "LN", "Link", ProjectObjectPaletteKeys.Info),
        ProjectObjectType.Connector => Profile("circle", "#8b5cf6", "CN", "Connector", ProjectObjectPaletteKeys.Secondary),
        ProjectObjectType.Script => ResolveScriptVisualProfile(objectSubtype),
        ProjectObjectType.Environment => ResolveEnvironmentVisualProfile(objectSubtype),
        ProjectObjectType.Infrastructure => ResolveInfrastructureVisualProfile(objectSubtype),
        ProjectObjectType.PromptFlow or ProjectObjectType.PromptSession => Profile("hex", "#0f766e", "PF", "Prompt", ProjectObjectPaletteKeys.Success),
        ProjectObjectType.PromptStep => Profile("pill", "#14b8a6", "ST", "Step", ProjectObjectPaletteKeys.Success),
        ProjectObjectType.ValidationRun => status.Contains("Approved", StringComparison.OrdinalIgnoreCase)
            ? Profile("diamond", "#16a34a", "VL", "Validate", ProjectObjectPaletteKeys.Success)
            : Profile("diamond", "#dc2626", "VL", "Validate", ProjectObjectPaletteKeys.Danger),
        ProjectObjectType.TestPlan or ProjectObjectType.TestEvidence => Profile("diamond", "#7c3aed", "TS", "Test", ProjectObjectPaletteKeys.Secondary),
        ProjectObjectType.Decision => Profile("hex", "#ea580c", "DC", "Decision", ProjectObjectPaletteKeys.Warning),
        ProjectObjectType.SecretReference => Profile("shield", "#be123c", "SC", "Secret", ProjectObjectPaletteKeys.Danger),
        _ => Profile("rect", "#d97706", "NT", "Note", ProjectObjectPaletteKeys.Warning)
    };

    private static (double X, double Y) GetDefaultPosition(ProjectObjectType objectType, int index)
        => objectType switch
        {
            ProjectObjectType.ProjectRoot => (140, 240),
            ProjectObjectType.Phase => (420, 120 + (index * 150)),
            ProjectObjectType.ProjectBlock => (760, 420 + (index * 110)),
            ProjectObjectType.Meeting or ProjectObjectType.Participant or ProjectObjectType.WorkItem => (760, 100 + (index * 120)),
            ProjectObjectType.Repository or ProjectObjectType.File or ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset or ProjectObjectType.Link or ProjectObjectType.Connector or ProjectObjectType.SecretReference or ProjectObjectType.Script or ProjectObjectType.Environment => (1040, 100 + (index * 120)),
            ProjectObjectType.Recording or ProjectObjectType.Transcript or ProjectObjectType.Infrastructure => (1320, 100 + (index * 120)),
            ProjectObjectType.PromptFlow or ProjectObjectType.PromptSession => (1080, 100 + (index * 150)),
            ProjectObjectType.PromptStep => (1400, 100 + (index * 120)),
            ProjectObjectType.ValidationRun => (780, 580 + (index * 120)),
            ProjectObjectType.TestPlan or ProjectObjectType.TestEvidence => (1100, 620 + (index * 140)),
            _ => (420 + ((index % 3) * 220), 820 + ((index / 3) * 140))
        };

    private static string NormalizeProgressMode(string? progressMode)
        => (progressMode?.Trim() ?? string.Empty).ToLowerInvariant() switch
        {
            "complete" => "complete",
            "started" => "started",
            "progress" => "progress",
            "na" => "na",
            _ => "progress"
        };

    private static (string Mode, int Percent) ResolveStatusBackedProgress(string? status)
    {
        var normalizedStatus = status?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedStatus) ||
            normalizedStatus.Contains("n/a", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("not applicable", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("skip", StringComparison.OrdinalIgnoreCase))
        {
            return ("na", 0);
        }

        if (normalizedStatus.Contains("done", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("complete", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("approved", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("used", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("ready", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("final", StringComparison.OrdinalIgnoreCase))
        {
            return ("complete", 100);
        }

        if (normalizedStatus.Contains("review", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("validation", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("testing", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("qa", StringComparison.OrdinalIgnoreCase))
        {
            return ("progress", 78);
        }

        if (normalizedStatus.Contains("active", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("in progress", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("running", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("blocked", StringComparison.OrdinalIgnoreCase))
        {
            return ("progress", 62);
        }

        if (normalizedStatus.Contains("planned", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("draft", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("pending", StringComparison.OrdinalIgnoreCase) ||
            normalizedStatus.Contains("queued", StringComparison.OrdinalIgnoreCase))
        {
            return ("progress", 28);
        }

        return ("progress", 48);
    }

    private static ProjectObjectVisualProfile ResolveProjectBlockVisualProfile(string objectSubtype)
        => objectSubtype switch
        {
            "feature" => Profile("hex", "#2563eb", "FB", "Feature", ProjectObjectPaletteKeys.Info),
            "architecture" => Profile("hex", "#4f46e5", "AR", "Architecture", ProjectObjectPaletteKeys.Secondary),
            "implementation" => Profile("hex", "#0f766e", "IM", "Implementation", ProjectObjectPaletteKeys.Success),
            "revision" => Profile("hex", "#f97316", "RB", "Revision", ProjectObjectPaletteKeys.Warning),
            "testing" => Profile("hex", "#7c3aed", "TB", "Testing", ProjectObjectPaletteKeys.Secondary),
            "prompting" => Profile("hex", "#0f766e", "PB", "Prompting", ProjectObjectPaletteKeys.Success),
            "financial" => Profile("hex", "#16a34a", "FN", "Financial", ProjectObjectPaletteKeys.Success),
            "marketing" => Profile("hex", "#db2777", "MK", "Marketing", ProjectObjectPaletteKeys.Danger),
            "research" => Profile("hex", "#0891b2", "RS", "Research", ProjectObjectPaletteKeys.Info),
            "delivery" => Profile("hex", "#d97706", "DL", "Delivery", ProjectObjectPaletteKeys.Warning),
            "operations" => Profile("hex", "#475569", "OP", "Operations", ProjectObjectPaletteKeys.Neutral),
            "deployment" => Profile("hex", "#2563eb", "DP", "Deployment", ProjectObjectPaletteKeys.Info),
            "repos" => Profile("hex", "#0284c7", "RP", "Repos", ProjectObjectPaletteKeys.Info),
            "dockers" => Profile("hex", "#2563eb", "DK", "Dockers", ProjectObjectPaletteKeys.Info),
            "task-flow" => Profile("hex", "#2563eb", "TF", "Task flow", ProjectObjectPaletteKeys.Info),
            "backlog" => Profile("hex", "#7c3aed", "BG", "Backlog", ProjectObjectPaletteKeys.Secondary),
            "server" => Profile("hex", "#b91c1c", "SV", "Server", ProjectObjectPaletteKeys.Danger),
            "computer" => Profile("hex", "#334155", "PC", "Computer", ProjectObjectPaletteKeys.Neutral),
            "router" => Profile("hex", "#2563eb", "RT", "Router", ProjectObjectPaletteKeys.Info),
            "wifi" => Profile("hex", "#0ea5e9", "WF", "WiFi", ProjectObjectPaletteKeys.Info),
            "risk" => Profile("hex", "#dc2626", "RK", "Risk", ProjectObjectPaletteKeys.Danger),
            "compliance" => Profile("hex", "#7c2d12", "CP", "Compliance", ProjectObjectPaletteKeys.Warning),
            "support" => Profile("hex", "#0284c7", "SP", "Support", ProjectObjectPaletteKeys.Info),
            _ => Profile("hex", "#334155", "BL", "Block", ProjectObjectPaletteKeys.Primary)
        };

    private static ProjectObjectVisualProfile ResolveWorkItemVisualProfile(string objectSubtype) => objectSubtype switch
    {
        "task" => Profile("pill", "#d97706", "TK", "Task", ProjectObjectPaletteKeys.Warning),
        "issue" => Profile("pill", "#dc2626", "IS", "Issue", ProjectObjectPaletteKeys.Danger),
        "revision" => Profile("pill", "#8b5cf6", "RV", "Revision", ProjectObjectPaletteKeys.Secondary),
        "feedback" => Profile("pill", "#0284c7", "FB", "Feedback", ProjectObjectPaletteKeys.Info),
        "payment" => Profile("pill", "#16a34a", "PM", "Payment", ProjectObjectPaletteKeys.Success),
        "send" => Profile("pill", "#2563eb", "SD", "Send", ProjectObjectPaletteKeys.Primary),
        _ => Profile("pill", "#475569", "WK", "Work", ProjectObjectPaletteKeys.Neutral)
    };

    private static ProjectObjectVisualProfile ResolveRepositoryVisualProfile(string objectSubtype) => objectSubtype switch
    {
        "remote" => Profile("rect", "#0f766e", "GH", "Remote", ProjectObjectPaletteKeys.Success),
        "local" => Profile("rect", "#0891b2", "RE", "Local", ProjectObjectPaletteKeys.Info),
        "folder" => Profile("rect", "#2563eb", "FD", "Folder", ProjectObjectPaletteKeys.Primary),
        _ => Profile("rect", "#0891b2", "RE", "Repo", ProjectObjectPaletteKeys.Info)
    };

    private static ProjectObjectVisualProfile ResolveFileVisualProfile(string objectSubtype) => objectSubtype switch
    {
        "pdf" => Profile("rect", "#dc2626", "PDF", "PDF", ProjectObjectPaletteKeys.Danger),
        "excel" => Profile("rect", "#16a34a", "XLS", "Excel", ProjectObjectPaletteKeys.Success),
        "docx" => Profile("rect", "#2563eb", "DOC", "Docx", ProjectObjectPaletteKeys.Info),
        "markdown" => Profile("rect", "#0284c7", "MD", "Markdown", ProjectObjectPaletteKeys.Info),
        "mermaid" => Profile("rect", "#7c3aed", "MMD", "Mermaid", ProjectObjectPaletteKeys.Secondary),
        "screenshot" => Profile("rect", "#db2777", "SS", "Screenshot", ProjectObjectPaletteKeys.Danger),
        "log" => Profile("rect", "#475569", "LOG", "Log", ProjectObjectPaletteKeys.Neutral),
        "archive" => Profile("rect", "#4338ca", "ZIP", "Archive", ProjectObjectPaletteKeys.Primary),
        "audio" => Profile("rect", "#0f766e", "AUD", "Audio", ProjectObjectPaletteKeys.Success),
        "json" => Profile("rect", "#64748b", "JS", "JSON", ProjectObjectPaletteKeys.Neutral),
        "text" => Profile("rect", "#64748b", "TXT", "Text", ProjectObjectPaletteKeys.Neutral),
        _ => Profile("rect", "#14b8a6", "FI", "File", ProjectObjectPaletteKeys.Info)
    };

    private static ProjectObjectVisualProfile ResolveScriptVisualProfile(string objectSubtype) => objectSubtype switch
    {
        "powershell" => Profile("diamond", "#2563eb", "PS", "PowerShell", ProjectObjectPaletteKeys.Info),
        "console" => Profile("diamond", "#0f766e", "CS", "Console", ProjectObjectPaletteKeys.Success),
        "ef-migration" => Profile("diamond", "#d97706", "EF", "Migration", ProjectObjectPaletteKeys.Warning),
        "tailwind-watch" => Profile("diamond", "#0ea5e9", "TW", "Tailwind", ProjectObjectPaletteKeys.Info),
        _ => Profile("diamond", "#475569", "SC", "Script", ProjectObjectPaletteKeys.Neutral)
    };

    private static ProjectObjectVisualProfile ResolveEnvironmentVisualProfile(string objectSubtype) => objectSubtype switch
    {
        "python" => Profile("hex", "#16a34a", "PY", "Python", ProjectObjectPaletteKeys.Success),
        "dotnet-runtime" => Profile("hex", "#2563eb", ".NET", "Runtime", ProjectObjectPaletteKeys.Info),
        "dotnet-watch" => Profile("hex", "#0ea5e9", "DW", "Watch", ProjectObjectPaletteKeys.Info),
        "dotnet-release" => Profile("hex", "#d97706", "REL", "Release", ProjectObjectPaletteKeys.Warning),
        _ => Profile("hex", "#475569", "ENV", "Environment", ProjectObjectPaletteKeys.Neutral)
    };

    private static ProjectObjectVisualProfile ResolveInfrastructureVisualProfile(string objectSubtype) => objectSubtype switch
    {
        "remote-server" => Profile("hex", "#b91c1c", "SV", "Server", ProjectObjectPaletteKeys.Danger),
        "domain" => Profile("hex", "#0284c7", "DNS", "Domain", ProjectObjectPaletteKeys.Info),
        "dns-record" => Profile("hex", "#0ea5e9", "DNS", "DNS", ProjectObjectPaletteKeys.Info),
        "docker-mode" => Profile("hex", "#2563eb", "DK", "Docker", ProjectObjectPaletteKeys.Info),
        "database" => Profile("hex", "#7c3aed", "DB", "Database", ProjectObjectPaletteKeys.Secondary),
        "deployment-folder" => Profile("hex", "#2563eb", "FD", "Folder", ProjectObjectPaletteKeys.Info),
        "key-reference" => Profile("hex", "#be123c", "KEY", "Key", ProjectObjectPaletteKeys.Danger),
        "ai-link" => Profile("hex", "#0f766e", "AI", "AI", ProjectObjectPaletteKeys.Success),
        _ => Profile("hex", "#475569", "INF", "Infrastructure", ProjectObjectPaletteKeys.Neutral)
    };

    private static ProjectObjectVisualProfile Profile(string shape, string accentColor, string icon, string accentBadge, string paletteKey)
        => new(shape, accentColor, icon, accentBadge, paletteKey);

    private static string ResolveBlockSubtypeLabel(string objectSubtype) => objectSubtype switch
    {
        "feature" => "Feature block",
        "architecture" => "Architecture block",
        "implementation" => "Implementation block",
        "revision" => "Revision block",
        "testing" => "Testing block",
        "prompting" => "Prompting block",
        "financial" => "Financial block",
        "marketing" => "Marketing block",
        "research" => "Research block",
        "delivery" => "Delivery block",
        "operations" => "Operations block",
        "risk" => "Risk block",
        "compliance" => "Compliance block",
        "support" => "Support block",
        _ => "Project block"
    };

    private static string SanitizeSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "asset";
        }

        var builder = new string(value
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());

        while (builder.Contains("--", StringComparison.Ordinal))
        {
            builder = builder.Replace("--", "-", StringComparison.Ordinal);
        }

        return builder.Trim('-');
    }

    private static bool TryResolvePromptFactorySessionId(string route, out Guid sessionId)
    {
        sessionId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(route))
        {
            return false;
        }

        const string marker = "sessionId=";
        var start = route.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return false;
        }

        start += marker.Length;
        var end = route.IndexOf('&', start);
        var rawValue = end >= start ? route[start..end] : route[start..];
        return Guid.TryParse(rawValue, out sessionId);
    }

    private static async Task<string> ResolvePromptFlowPhaseAsync(
        AppDbContext dbContext,
        Guid projectId,
        ProjectObjectRecord node,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(node.ParentNodeKey))
        {
            var records = await dbContext.Set<ProjectObjectRecord>()
                .Where(item => item.ProjectId == projectId)
                .ToDictionaryAsync(item => item.NodeKey, cancellationToken);
            var currentParentKey = node.ParentNodeKey;
            var visited = new HashSet<string>(StringComparer.Ordinal);

            while (!string.IsNullOrWhiteSpace(currentParentKey) &&
                   visited.Add(currentParentKey) &&
                   records.TryGetValue(currentParentKey, out var parentNode))
            {
                if (parentNode.ObjectType == ProjectObjectType.Phase &&
                    !string.IsNullOrWhiteSpace(parentNode.Title))
                {
                    return parentNode.Title.Trim();
                }

                currentParentKey = parentNode.ParentNodeKey;
            }
        }

        return (await dbContext.Set<Project>()
            .Where(item => item.Id == projectId)
            .Select(item => item.CurrentPhase)
            .FirstOrDefaultAsync(cancellationToken))?.Trim() ?? string.Empty;
    }

    private static ProjectObjectType MapResourceKind(ResourceKind resourceKind) => resourceKind switch
    {
        ResourceKind.Repository => ProjectObjectType.Repository,
        ResourceKind.File => ProjectObjectType.File,
        ResourceKind.WebLink or ResourceKind.PromptLink => ProjectObjectType.Link,
        ResourceKind.SecretLink => ProjectObjectType.SecretReference,
        _ => ProjectObjectType.Connector
    };
}


