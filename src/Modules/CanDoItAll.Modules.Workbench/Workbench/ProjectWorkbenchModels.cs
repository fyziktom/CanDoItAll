using System.ComponentModel.DataAnnotations.Schema;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.TestLab;
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
    Test,
    Skip,
    MarkUsed
}

public enum ProjectObjectPlacementIntent
{
    CallerControlled,
    AutomaticAroundParent
}

public static class ProjectProgressPolicy
{
    public const int UntrackedPercent = -1;

    public static bool IsTrackedPercent(int value)
    {
        return value is >= 0 and <= 100;
    }
}

public static class ProjectObjectSubtypePolicy
{
    public const string Task = "task";

    public static string Normalize(ProjectObjectType objectType, string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return objectType == ProjectObjectType.WorkItem &&
            string.Equals(normalized, Task, StringComparison.OrdinalIgnoreCase)
                ? Task
                : normalized;
    }
}

public sealed partial class ProjectObjectRecord : IProjectObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string NodeKey { get; set; } = string.Empty;
    public ProjectObjectType ObjectType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string ObjectSubtype { get; set; } = string.Empty;
    public string ProgressMode { get; set; } = string.Empty;
    public int ProgressPercent { get; set; } = ProjectProgressPolicy.UntrackedPercent;
    public string MarkersJson { get; set; } = "[]";
    public int Priority { get; set; }
    public string MetadataJson { get; set; } = "{}";
    public string? ParentNodeKey { get; set; }
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public DateTimeOffset? StartUtc { get; set; }
    public DateTimeOffset? EndUtc { get; set; }
    public int? DurationSeconds { get; set; }
    public bool IsSystemManaged { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    [NotMapped]
    internal ProjectNodeBindingState Binding { get; set; } = ProjectNodeBindingState.Empty;

    [NotMapped]
    internal ProjectNodeReferenceCollection NodeReferences { get; set; } = ProjectNodeReferenceCollection.Empty;
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
        builder.Property(item => item.ObjectSubtype).HasMaxLength(120);
        builder.Property(item => item.ProgressMode).HasMaxLength(32);
        builder.Property(item => item.MarkersJson).HasColumnType("TEXT");
        builder.Property(item => item.MetadataJson).HasColumnType("TEXT");
        builder.Property(item => item.ParentNodeKey).HasMaxLength(160);
        builder.Ignore(item => item.Binding);
        builder.Ignore(item => item.NodeReferences);
        builder.HasIndex(item => new { item.ProjectId, item.NodeKey }).IsUnique();
        builder.HasIndex(item => new { item.ProjectId, item.ObjectType, item.ObjectSubtype, item.IsSystemManaged });
        builder.HasIndex(item => new { item.ProjectId, item.ParentNodeKey, item.ObjectType, item.IsSystemManaged });
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
        builder.HasIndex(item => new { item.ProjectId, item.LinkKind, item.IsSystemManaged });
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
    string StorageObjectReferenceJson = "",
    ProjectStructureProjectRole ProjectRole = ProjectStructureProjectRole.None,
    Guid? RelatedProjectId = null,
    int ParentProjectCount = 0,
    int? DurationSeconds = null,
    ProjectNodeReferenceCollection? NodeReferences = null,
    bool IsSystemManaged = false);

public sealed record ProjectStructureLink(
    string SourceId,
    string TargetId,
    ProjectObjectLinkKind Kind,
    bool IsUserAuthored,
    Guid? RecordId = null);

public sealed record ProjectStructureSurface(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<ProjectStructureNode> Nodes,
    IReadOnlyList<ProjectStructureLink> Links,
    string? ViewStateJson);

public sealed record ProjectCalendarEvent(
    Guid Id,
    string NodeKey,
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
    string? MetadataJson = null,
    int? DurationSeconds = null,
    ProjectNodeReferenceCollection? NodeReferences = null,
    ProjectObjectExternalBindingRequest? ExternalBinding = null,
    string? Status = null,
    ProjectObjectPlacementIntent PlacementIntent = ProjectObjectPlacementIntent.CallerControlled);

public sealed record ProjectObjectExternalBindingRequest(
    string Route,
    string ArtifactKind,
    Guid? ArtifactId);

public sealed record ProjectObjectEditRequest(
    string Title,
    string Subtitle,
    string Notes,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    string MetadataJson,
    int? DurationSeconds = null,
    ProjectNodeReferenceCollection? NodeReferences = null);

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
    string? MetadataJson = null,
    int? DurationSeconds = null);

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

public sealed record ProjectStructureClipboardCopyResult(
    IReadOnlyList<string> RootNodeIds,
    IReadOnlyDictionary<string, string> NodeIdMap);

internal sealed record SavedMediaDescriptor(
    string RelativePath,
    string Route,
    string ContentType,
    string OriginalFileName,
    string ArtifactKind,
    string StorageObjectReferenceJson);

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
public sealed partial class ProjectWorkbenchService(
IDbContextFactory<AppDbContext> dbContextFactory,
IClock clock,
IStoragePlacementService storagePlacementService,
ProjectStructureAssemblyService projectStructureAssemblyService,
ProjectWorkbenchRelationService relationService,
ProjectWorkbenchLifecycleService lifecycleService,
ProjectWorkbenchCommandService commandService,
ProjectWorkbenchCrossModuleMutationService crossModuleMutationService) : IProjectWorkbenchSeedService
{
    private const string GanttTaskSubtype = "task";
    private const string GanttViewStateSurfaceKind = "gantt";
    private static readonly ProjectStructureInvariantService InvariantService = new();

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

        var assembly = await projectStructureAssemblyService.LoadAsync(dbContext, projectId, cancellationToken);
        var viewState = await LoadViewStateAsync(dbContext, projectId, "structure", cancellationToken);
        var mappedNodes = ProjectWorkbenchNodeMapper.MapStructureNodes(assembly.Nodes, assembly.Links);

        return new ProjectStructureLoadResult(
            new ProjectStructureSurface(
                project.Id,
                project.Name,
                mappedNodes,
                assembly.Links.Select(link => new ProjectStructureLink(link.SourceNodeKey, link.TargetNodeKey, link.LinkKind, !link.IsSystemManaged, link.Id)).ToList(),
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

        var assembly = await projectStructureAssemblyService.LoadAsync(dbContext, projectId, cancellationToken);
        var viewState = await LoadViewStateAsync(dbContext, projectId, "calendar", cancellationToken);
        var preferredView = ResolvePreferredCalendarView(viewState);
        var events = assembly.Nodes
            .Where(item => item.StartUtc.HasValue && item.EndUtc.HasValue)
            .OrderBy(item => item.StartUtc)
            .ThenBy(item => item.EndUtc)
            .ThenBy(item => item.Title)
            .Select(item => new ProjectCalendarEvent(
                item.Id,
                item.NodeKey,
                item.Title,
                item.StartUtc!.Value,
                item.EndUtc!.Value,
                item.Status,
                item.Binding.Route,
                item.Binding.ExternalArtifactKind,
                item.Binding.ExternalArtifactId,
                item.ObjectType,
                ProjectNodeKindRegistry.ResolveVisualProfile(item.ObjectType, item.ObjectSubtype, item.Status).AccentColor))
            .ToList();

        return new ProjectCalendarLoadResult(
            new ProjectCalendarSurface(project.Id, project.Name, events, preferredView, viewState),
            null);
    }

    public async Task<ProjectStructureNode> CreateObjectAsync(Guid projectId, ProjectObjectCreateRequest request, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var normalizedParentNodeKey = ProjectWorkbenchGraphConventions.NormalizeEditableParentNodeKey(projectId, request.ParentNodeKey);
        var existingNodes = (await projectStructureAssemblyService.LoadAsync(dbContext, projectId, cancellationToken)).Nodes;
        InvariantService.ValidateParentAssignment(projectId, $"pending:{Guid.NewGuid():N}", normalizedParentNodeKey, existingNodes);

        var hasRequestedPosition = request.X.HasValue && request.Y.HasValue;
        var existingCount = existingNodes.Count(item => !item.IsSystemManaged);
        var position = request.PlacementIntent == ProjectObjectPlacementIntent.AutomaticAroundParent
            ? ProjectStructureAutomaticPlacementPolicy.Resolve(
                existingNodes,
                new ProjectStructureAutomaticPlacementRequest(
                    normalizedParentNodeKey,
                    request.ObjectType,
                    request.Title,
                    request.Subtitle,
                    request.Notes,
                    hasRequestedPosition ? (request.X!.Value, request.Y!.Value) : null))
            : hasRequestedPosition
                ? (request.X!.Value, request.Y!.Value)
                : ProjectWorkbenchGraphConventions.GetDefaultPosition(request.ObjectType, existingCount + 1);
        var media = await SaveMediaAsync(projectId, request.ObjectType, request.Media, cancellationToken);
        var binding = ResolveCreateBinding(projectId, request.ObjectType, media, request.ExternalBinding);
        var normalizedObjectSubtype = ProjectObjectSubtypePolicy.Normalize(request.ObjectType, request.ObjectSubtype);
        var metadataJson = ProjectWorkbenchObjectModeling.ResolveMetadataJson(request.ObjectType, normalizedObjectSubtype, request.MetadataJson, null, request.Notes, media);
        var resolvedEndUtc = ProjectWorkbenchObjectModeling.ResolveEndUtc(request.StartUtc, request.EndUtc, request.DurationSeconds);
        var normalizedDurationSeconds = ProjectWorkbenchObjectModeling.NormalizeDurationSeconds(request.DurationSeconds, request.StartUtc, resolvedEndUtc);
        var normalizedStatus = string.IsNullOrWhiteSpace(request.Status) ? "Draft" : request.Status.Trim();
        var progress = ProjectWorkbenchObjectModeling.ResolveStatusBackedProgress(normalizedStatus);

        var record = new ProjectObjectRecord
        {
            ProjectId = projectId,
            NodeKey = $"custom:{Guid.NewGuid():N}",
            ObjectType = request.ObjectType,
            Title = string.IsNullOrWhiteSpace(request.Title) ? request.ObjectType.ToString() : request.Title.Trim(),
            Subtitle = request.Subtitle?.Trim() ?? string.Empty,
            Status = normalizedStatus,
            Notes = request.Notes?.Trim() ?? string.Empty,
            ObjectSubtype = normalizedObjectSubtype,
            ProgressMode = progress.Mode,
            ProgressPercent = progress.Percent,
            MetadataJson = metadataJson,
            ParentNodeKey = normalizedParentNodeKey,
            PositionX = position.Item1,
            PositionY = position.Item2,
            StartUtc = request.StartUtc,
            EndUtc = resolvedEndUtc,
            DurationSeconds = normalizedDurationSeconds,
            CreatedAtUtc = clock.GetUtcNow(),
            UpdatedAtUtc = clock.GetUtcNow(),
            Binding = new ProjectNodeBindingState(
                binding.Route,
                binding.ExternalArtifactKind,
                binding.ExternalArtifactId,
                binding.MediaRelativePath,
                binding.MediaContentType,
                binding.MediaOriginalFileName,
                binding.StorageObjectReferenceJson),
            NodeReferences = request.NodeReferences ?? ProjectNodeReferenceCollection.Empty,
            MarkersJson = "[]"
        };

        await dbContext.Set<ProjectObjectRecord>().AddAsync(record, cancellationToken);

        if (request.ObjectType is ProjectObjectType.PromptFlow or ProjectObjectType.PromptSession or ProjectObjectType.PromptStep)
        {
            await commandService.EnsurePromptGalleryArtifactAsync(dbContext, projectId, record, cancellationToken);
        }

        var bindingPlan = await ProjectNodeBindingStorage.PersistAsync(dbContext, record, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        ProjectNodeBindingStorage.Apply(record, bindingPlan);
        return ProjectWorkbenchNodeMapper.MapStructureNode(record);
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
        var projectRootNodeKey = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId);

        foreach (var seed in seeds.Where(seed => !string.IsNullOrWhiteSpace(seed.Title)))
        {
            index++;
            var nodeKey = $"custom:{Guid.NewGuid():N}";
            var normalizedObjectSubtype = ProjectObjectSubtypePolicy.Normalize(seed.ObjectType, seed.ObjectSubtype);
            var position = ProjectWorkbenchGraphConventions.GetDefaultPosition(seed.ObjectType, existingCount + index);
            var resolvedEndUtc = ProjectWorkbenchObjectModeling.ResolveEndUtc(seed.StartUtc, seed.EndUtc, seed.DurationSeconds);
            var normalizedDurationSeconds = ProjectWorkbenchObjectModeling.NormalizeDurationSeconds(seed.DurationSeconds, seed.StartUtc, resolvedEndUtc);
            var record = new ProjectObjectRecord
            {
                ProjectId = projectId,
                NodeKey = nodeKey,
                ObjectType = seed.ObjectType,
                Title = seed.Title.Trim(),
                Subtitle = seed.Subtitle?.Trim() ?? string.Empty,
                Status = "Planned",
                Notes = seed.Notes?.Trim() ?? string.Empty,
                ObjectSubtype = normalizedObjectSubtype,
                ProgressMode = "progress",
                ProgressPercent = 0,
                MetadataJson = ProjectWorkbenchObjectModeling.ResolveMetadataJson(seed.ObjectType, normalizedObjectSubtype, seed.MetadataJson, null, seed.Notes, null),
                ParentNodeKey = projectRootNodeKey,
                PositionX = position.Item1,
                PositionY = position.Item2,
                StartUtc = seed.StartUtc,
                EndUtc = resolvedEndUtc,
                DurationSeconds = normalizedDurationSeconds,
                CreatedAtUtc = clock.GetUtcNow(),
                UpdatedAtUtc = clock.GetUtcNow(),
                Binding = new ProjectNodeBindingState(
                    $"/projects/{projectId}/structure",
                    seed.ObjectType.ToString(),
                    null,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty),
                MarkersJson = "[]"
            };
            await dbContext.Set<ProjectObjectRecord>().AddAsync(record, cancellationToken);
            await ProjectNodeBindingStorage.PersistAsync(dbContext, record, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    async Task IProjectWorkbenchSeedService.SeedProjectObjectsAsync(Guid projectId, IReadOnlyCollection<ProjectObjectSeedDraft> seeds, CancellationToken cancellationToken)
        => await SeedProjectObjectsAsync(
            projectId,
            seeds.Select(seed => new ProjectObjectSeedRequest(seed.ObjectType, seed.Title, seed.Subtitle, seed.Notes, seed.StartUtc, seed.EndUtc, null, null, seed.DurationSeconds)).ToList(),
            cancellationToken);

    public async Task LinkObjectsAsync(Guid projectId, string sourceNodeKey, string targetNodeKey, ProjectObjectLinkKind linkKind, CancellationToken cancellationToken = default)
    {
        await relationService.LinkObjectsAsync(projectId, sourceNodeKey, targetNodeKey, linkKind, cancellationToken);
    }

    public async Task<bool> UnlinkObjectsAsync(
        Guid projectId,
        string sourceNodeKey,
        string targetNodeKey,
        ProjectObjectLinkKind linkKind,
        CancellationToken cancellationToken = default)
    {
        return await relationService.UnlinkObjectsAsync(projectId, sourceNodeKey, targetNodeKey, linkKind, cancellationToken);
    }

    public async Task<ProjectStructureNode?> ReparentObjectAsync(
        Guid projectId,
        string nodeKey,
        string? parentNodeKey,
        CancellationToken cancellationToken = default)
    {
        return await relationService.ReparentObjectAsync(projectId, nodeKey, parentNodeKey, cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectStructureNode>> ReparentSubtreesAsync(
        Guid projectId,
        IReadOnlyCollection<string> sourceRootNodeKeys,
        string targetParentNodeKey,
        CancellationToken cancellationToken = default)
    {
        return await relationService.ReparentSubtreesAsync(
            projectId,
            sourceRootNodeKeys,
            targetParentNodeKey,
            cancellationToken);
    }

    public async Task<int> DeleteObjectAsync(Guid projectId, string nodeKey, CancellationToken cancellationToken = default)
    {
        return await crossModuleMutationService.DeleteObjectAsync(projectId, nodeKey, cancellationToken);
    }

    public async Task MoveObjectAsync(Guid projectId, string nodeKey, double x, double y, CancellationToken cancellationToken = default)
    {
        await relationService.MoveObjectAsync(projectId, nodeKey, x, y, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> MoveObjectsAsync(
        Guid projectId,
        IReadOnlyCollection<ProjectNodeMoveRequest> positions,
        CancellationToken cancellationToken = default)
    {
        return await relationService.MoveObjectsAsync(projectId, positions, cancellationToken);
    }

    public async Task<ProjectStructureSubtreeRecompositionResult?> RecomposeSubtreeAsync(
        Guid projectId,
        string rootNodeKey,
        CancellationToken cancellationToken = default)
    {
        return await relationService.RecomposeSubtreeAsync(projectId, rootNodeKey, cancellationToken);
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
        await ProjectNodeBindingStorage.LoadAsync(dbContext, [node], cancellationToken);

        node.Title = string.IsNullOrWhiteSpace(title) ? node.Title : title.Trim();
        node.Subtitle = subtitle?.Trim() ?? string.Empty;
        node.Notes = notes?.Trim() ?? string.Empty;
        node.UpdatedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return ProjectWorkbenchNodeMapper.MapStructureNode(node);
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
        await ProjectNodeBindingStorage.LoadAsync(dbContext, [node], cancellationToken);

        node.Title = string.IsNullOrWhiteSpace(request.Title) ? node.Title : request.Title.Trim();
        node.Subtitle = request.Subtitle?.Trim() ?? string.Empty;
        node.Notes = request.Notes?.Trim() ?? string.Empty;
        node.StartUtc = request.StartUtc;
        node.EndUtc = ProjectWorkbenchObjectModeling.ResolveEndUtc(request.StartUtc, request.EndUtc, request.DurationSeconds);
        node.DurationSeconds = ProjectWorkbenchObjectModeling.NormalizeDurationSeconds(request.DurationSeconds, node.StartUtc, node.EndUtc);
        node.MetadataJson = ProjectWorkbenchObjectModeling.ResolveMetadataJson(node.ObjectType, node.ObjectSubtype, request.MetadataJson, node.MetadataJson, node.Notes, null);
        node.NodeReferences = request.NodeReferences ?? node.NodeReferences;
        node.UpdatedAtUtc = clock.GetUtcNow();
        var bindingPlan = await ProjectNodeBindingStorage.PersistAsync(dbContext, node, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        ProjectNodeBindingStorage.Apply(node, bindingPlan);
        return ProjectWorkbenchNodeMapper.MapStructureNode(node);
    }

    public async Task<ProjectStructureNode?> ReclassifyObjectAsync(
        Guid projectId,
        string nodeKey,
        ProjectObjectReclassificationRequest request,
        CancellationToken cancellationToken = default)
    {
        return await lifecycleService.ReclassifyObjectAsync(projectId, nodeKey, request, cancellationToken);
    }

    public async Task<ProjectStructureSubprojectTransferResult?> MoveDescendantsToProjectAsync(
        Guid sourceProjectId,
        string sourceNodeKey,
        Guid targetProjectId,
        CancellationToken cancellationToken = default)
    {
        return await crossModuleMutationService.MoveDescendantsToProjectAsync(
            sourceProjectId,
            sourceNodeKey,
            targetProjectId,
            cancellationToken);
    }

    public async Task<ProjectStructureSubprojectTransferResult?> MoveNodesToProjectAsync(
        Guid sourceProjectId,
        IReadOnlyCollection<string> sourceNodeKeys,
        Guid targetProjectId,
        bool includeDescendants = true,
        CancellationToken cancellationToken = default)
    {
        return await crossModuleMutationService.MoveNodesToProjectAsync(
            sourceProjectId,
            sourceNodeKeys,
            targetProjectId,
            includeDescendants,
            cancellationToken);
    }

    public Task<ProjectStructureNode?> UpdateObjectMetadataAsync(
        Guid projectId,
        string nodeKey,
        string metadataJson,
        string? notes = null,
        string? status = null,
        ProjectNodeReferenceCollection? nodeReferences = null,
        CancellationToken cancellationToken = default)
        => UpdateObjectMetadataCoreAsync(
            projectId,
            nodeKey,
            metadataJson: metadataJson,
            metadataMutation: null,
            notes: notes,
            status: status,
            nodeReferences: nodeReferences,
            cancellationToken: cancellationToken);

    public Task<ProjectStructureNode?> MutateObjectMetadataAsync(
        Guid projectId,
        string nodeKey,
        Action<ProjectObjectMetadataEnvelope> metadataMutation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadataMutation);
        return UpdateObjectMetadataCoreAsync(
            projectId,
            nodeKey,
            metadataJson: null,
            metadataMutation: metadataMutation,
            notes: null,
            status: null,
            nodeReferences: null,
            cancellationToken: cancellationToken);
    }

    private async Task<ProjectStructureNode?> UpdateObjectMetadataCoreAsync(
        Guid projectId,
        string nodeKey,
        string? metadataJson,
        Action<ProjectObjectMetadataEnvelope>? metadataMutation,
        string? notes,
        string? status,
        ProjectNodeReferenceCollection? nodeReferences,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var node = await dbContext.Set<ProjectObjectRecord>()
            .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.NodeKey == nodeKey && !item.IsSystemManaged, cancellationToken);
        if (node is null)
        {
            return null;
        }
        await ProjectNodeBindingStorage.LoadAsync(dbContext, [node], cancellationToken);

        if (notes is not null)
        {
            node.Notes = notes.Trim();
        }

        if (metadataMutation is not null)
        {
            var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
            metadataMutation(metadata);
            metadataJson = ProjectObjectMetadataSerializer.Serialize(metadata);
        }

        node.MetadataJson = ProjectWorkbenchObjectModeling.ResolveMetadataJson(node.ObjectType, node.ObjectSubtype, metadataJson, node.MetadataJson, node.Notes, null);
        node.NodeReferences = nodeReferences ?? node.NodeReferences;

        if (!string.IsNullOrWhiteSpace(status))
        {
            node.Status = status.Trim();
            var progress = ProjectWorkbenchObjectModeling.ResolveStatusBackedProgress(node.Status);
            node.ProgressMode = progress.Mode;
            node.ProgressPercent = progress.Percent;
        }

        node.UpdatedAtUtc = clock.GetUtcNow();
        var bindingPlan = await ProjectNodeBindingStorage.PersistAsync(dbContext, node, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        ProjectNodeBindingStorage.Apply(node, bindingPlan);
        return ProjectWorkbenchNodeMapper.MapStructureNode(node);
    }

    public async Task<ProjectStructureNode?> ReplaceObjectMediaAsync(
        Guid projectId,
        string nodeKey,
        ProjectObjectMediaPayload media,
        string? metadataJson = null,
        string? notes = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(media);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var node = await dbContext.Set<ProjectObjectRecord>()
            .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.NodeKey == nodeKey && !item.IsSystemManaged, cancellationToken);
        if (node is null)
        {
            return null;
        }

        if (node.ObjectType is not (ProjectObjectType.File or ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset))
        {
            throw new InvalidOperationException($"Project object '{nodeKey}' does not support media replacement.");
        }

        await ProjectNodeBindingStorage.LoadAsync(dbContext, [node], cancellationToken);
        var savedMedia = await SaveMediaAsync(projectId, node.ObjectType, media, cancellationToken)
            ?? throw new InvalidOperationException($"Replacement media for project object '{nodeKey}' could not be saved.");
        node.Binding = ResolveCreateBinding(projectId, node.ObjectType, savedMedia, null);

        if (notes is not null)
        {
            node.Notes = notes.Trim();
        }

        node.MetadataJson = ProjectWorkbenchObjectModeling.ResolveMetadataJson(
            node.ObjectType,
            node.ObjectSubtype,
            metadataJson,
            node.MetadataJson,
            node.Notes,
            savedMedia);

        if (!string.IsNullOrWhiteSpace(status))
        {
            node.Status = status.Trim();
            var progress = ProjectWorkbenchObjectModeling.ResolveStatusBackedProgress(node.Status);
            node.ProgressMode = progress.Mode;
            node.ProgressPercent = progress.Percent;
        }

        node.UpdatedAtUtc = clock.GetUtcNow();
        var bindingPlan = await ProjectNodeBindingStorage.PersistAsync(dbContext, node, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        ProjectNodeBindingStorage.Apply(node, bindingPlan);
        return ProjectWorkbenchNodeMapper.MapStructureNode(node);
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
            var progress = ProjectWorkbenchObjectModeling.ResolveStatusBackedProgress(normalizedStatus);
            node.ProgressMode = progress.Mode;
            node.ProgressPercent = progress.Percent;
            node.UpdatedAtUtc = updatedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await ProjectNodeBindingStorage.LoadAsync(dbContext, nodes, cancellationToken);
        return nodes.Select(node => ProjectWorkbenchNodeMapper.MapStructureNode(node)).ToList();
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

        var normalizedMode = ProjectWorkbenchObjectModeling.NormalizeProgressMode(progressMode);
        var normalizedPercent = Math.Clamp(progressPercent, 0, 100);
        var updatedAtUtc = clock.GetUtcNow();
        foreach (var node in nodes)
        {
            node.ProgressMode = normalizedMode;
            node.ProgressPercent = normalizedPercent;
            node.UpdatedAtUtc = updatedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await ProjectNodeBindingStorage.LoadAsync(dbContext, nodes, cancellationToken);
        return nodes.Select(node => ProjectWorkbenchNodeMapper.MapStructureNode(node)).ToList();
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
            var existingMarkers = ProjectNodeMarkerState.Parse(node.MarkersJson);
            var updatedMarkers = mutationMode switch
            {
                ProjectMarkerMutationMode.ReplaceAll => normalizedMarker is null ? [] : [normalizedMarker],
                ProjectMarkerMutationMode.Add => ProjectWorkbenchObjectModeling.AddMarker(existingMarkers, normalizedMarker),
                ProjectMarkerMutationMode.Remove => ProjectWorkbenchObjectModeling.RemoveMarker(existingMarkers, normalizedMarker),
                ProjectMarkerMutationMode.Toggle => ProjectWorkbenchObjectModeling.ToggleMarker(existingMarkers, normalizedMarker),
                ProjectMarkerMutationMode.ClearAll => [],
                _ => existingMarkers
            };
            node.MarkersJson = ProjectNodeMarkerState.Serialize(updatedMarkers);
            node.UpdatedAtUtc = updatedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await ProjectNodeBindingStorage.LoadAsync(dbContext, nodes, cancellationToken);
        return nodes.Select(node => ProjectWorkbenchNodeMapper.MapStructureNode(node)).ToList();
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
        await ProjectNodeBindingStorage.LoadAsync(dbContext, nodes, cancellationToken);
        return nodes.Select(node => ProjectWorkbenchNodeMapper.MapStructureNode(node)).ToList();
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
        await UpsertViewStateAsync(dbContext, projectId, surfaceKind, stateJson, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProjectStructureGanttViewState> LoadGanttViewStateAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var state = ProjectStructureGanttViewState.Parse(await LoadViewStateAsync(
            dbContext,
            projectId,
            GanttViewStateSurfaceKind,
            cancellationToken));
        var taskNodeIds = await LoadCanonicalGanttTaskNodeIdsAsync(dbContext, projectId, cancellationToken);
        return new ProjectStructureGanttViewState(state.ResolveOrderedTaskNodeIds(taskNodeIds));
    }

    internal async Task SaveGanttViewStateAsync(
        Guid projectId,
        ProjectStructureGanttViewState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var taskNodeIds = await LoadCanonicalGanttTaskNodeIdsAsync(dbContext, projectId, cancellationToken);
        var normalizedState = new ProjectStructureGanttViewState(state.ResolveOrderedTaskNodeIds(taskNodeIds));
        await PersistGanttViewStateAsync(dbContext, projectId, normalizedState, cancellationToken);
    }

    internal async Task<ProjectStructureGanttViewState> InsertGanttTaskIntoRowOrderAsync(
        Guid projectId,
        string taskNodeId,
        string? afterTaskNodeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskNodeId);
        if (string.Equals(taskNodeId, afterTaskNodeId, StringComparison.Ordinal))
        {
            throw new ArgumentException("A Gantt task cannot be inserted after itself.", nameof(afterTaskNodeId));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var taskNodeIds = await LoadCanonicalGanttTaskNodeIdsAsync(dbContext, projectId, cancellationToken);
        EnsureCanonicalGanttTask(taskNodeIds, taskNodeId);
        var state = await LoadNormalizedGanttViewStateAsync(dbContext, projectId, taskNodeIds, cancellationToken);
        var orderedTaskNodeIds = state.OrderedTaskNodeIds.ToList();
        orderedTaskNodeIds.Remove(taskNodeId);

        if (string.IsNullOrWhiteSpace(afterTaskNodeId))
        {
            orderedTaskNodeIds.Add(taskNodeId);
        }
        else
        {
            var anchorIndex = orderedTaskNodeIds.IndexOf(afterTaskNodeId);
            if (anchorIndex < 0)
            {
                throw new InvalidOperationException($"Gantt row anchor '{afterTaskNodeId}' is not a canonical task in project '{projectId}'.");
            }

            orderedTaskNodeIds.Insert(anchorIndex + 1, taskNodeId);
        }

        var updatedState = new ProjectStructureGanttViewState(orderedTaskNodeIds);
        await PersistGanttViewStateAsync(dbContext, projectId, updatedState, cancellationToken);
        return updatedState;
    }

    internal async Task<ProjectStructureGanttViewState> MoveGanttTaskInRowOrderAsync(
        Guid projectId,
        ProjectStructureGanttRowMoveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TaskNodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AnchorTaskNodeId);
        if (string.Equals(request.TaskNodeId, request.AnchorTaskNodeId, StringComparison.Ordinal))
        {
            throw new ArgumentException("A Gantt task cannot be moved relative to itself.", nameof(request));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var taskNodeIds = await LoadCanonicalGanttTaskNodeIdsAsync(dbContext, projectId, cancellationToken);
        EnsureCanonicalGanttTask(taskNodeIds, request.TaskNodeId);
        EnsureCanonicalGanttTask(taskNodeIds, request.AnchorTaskNodeId);
        var state = await LoadNormalizedGanttViewStateAsync(dbContext, projectId, taskNodeIds, cancellationToken);
        var orderedTaskNodeIds = state.OrderedTaskNodeIds.ToList();
        var taskIndex = orderedTaskNodeIds.IndexOf(request.TaskNodeId);
        var anchorIndex = orderedTaskNodeIds.IndexOf(request.AnchorTaskNodeId);
        var hasExpectedAdjacency = request.Placement switch
        {
            ProjectStructureGanttRowPlacement.Before => anchorIndex == taskIndex - 1,
            ProjectStructureGanttRowPlacement.After => anchorIndex == taskIndex + 1,
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Placement, "Unsupported Gantt row placement.")
        };
        if (!hasExpectedAdjacency)
        {
            throw new ProjectStructureGanttRowOrderConflictException(
                request.TaskNodeId,
                request.AnchorTaskNodeId,
                request.Placement);
        }

        (orderedTaskNodeIds[taskIndex], orderedTaskNodeIds[anchorIndex]) =
            (orderedTaskNodeIds[anchorIndex], orderedTaskNodeIds[taskIndex]);

        var updatedState = new ProjectStructureGanttViewState(orderedTaskNodeIds);
        await PersistGanttViewStateAsync(dbContext, projectId, updatedState, cancellationToken);
        return updatedState;
    }

    public async Task<ArtifactReference?> ExecuteNodeCommandAsync(Guid projectId, string nodeKey, ProjectStructureCommandKind commandKind, CancellationToken cancellationToken = default)
    {
        return await commandService.ExecuteNodeCommandAsync(projectId, nodeKey, commandKind, cancellationToken);
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
        var contentKind = StorageContentClassifier.Resolve(media.ContentType, media.FileName);
        var placement = await storagePlacementService.PlaceAsync(
            new StoragePlacementRequest(
                media.FileName,
                media.ContentType,
                bytes,
                StorageUsagePurpose.ProjectAsset,
                contentKind,
                projectId,
                RelativePathHint: relativePath,
                PreviewRequired: StorageContentClassifier.SupportsInlinePreview(contentKind)),
            cancellationToken);
        var storageObjectReference = placement.WriteResult.Reference;
        var storageReference = StorageJson.SerializeReference(storageObjectReference);

        return new SavedMediaDescriptor(
            placement.RelativePath,
            placement.Route,
            storageObjectReference.ContentType,
            media.FileName,
            objectType.ToString(),
            storageReference);
    }

    private static ProjectNodeBindingState ResolveCreateBinding(
        Guid projectId,
        ProjectObjectType objectType,
        SavedMediaDescriptor? media,
        ProjectObjectExternalBindingRequest? externalBinding)
    {
        if (media is not null && externalBinding is not null)
        {
            throw new InvalidOperationException("A project object cannot use both uploaded media and an explicit external binding.");
        }

        if (media is not null)
        {
            return new ProjectNodeBindingState(
                media.Route,
                media.ArtifactKind,
                null,
                media.RelativePath,
                media.ContentType,
                media.OriginalFileName,
                media.StorageObjectReferenceJson);
        }

        if (externalBinding is not null)
        {
            if (string.IsNullOrWhiteSpace(externalBinding.Route))
            {
                throw new InvalidOperationException("External binding route is required.");
            }

            if (string.IsNullOrWhiteSpace(externalBinding.ArtifactKind))
            {
                throw new InvalidOperationException("External binding artifact kind is required.");
            }

            return new ProjectNodeBindingState(
                externalBinding.Route.Trim(),
                externalBinding.ArtifactKind.Trim(),
                externalBinding.ArtifactId,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        return new ProjectNodeBindingState(
            $"/projects/{projectId}/structure",
            objectType.ToString(),
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);
    }

    private static async Task<IReadOnlyList<string>> LoadCanonicalGanttTaskNodeIdsAsync(
        AppDbContext dbContext,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var workItems = await dbContext.Set<ProjectObjectRecord>()
            .Where(item =>
                item.ProjectId == projectId &&
                item.ObjectType == ProjectObjectType.WorkItem &&
                !item.IsSystemManaged)
            .OrderBy(item => item.PositionY)
            .ThenBy(item => item.PositionX)
            .ThenBy(item => item.NodeKey)
            .Select(item => new { item.NodeKey, item.ObjectSubtype })
            .ToListAsync(cancellationToken);

        return workItems
            .Where(item => string.Equals(item.ObjectSubtype, GanttTaskSubtype, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.NodeKey)
            .ToArray();
    }

    private static void EnsureCanonicalGanttTask(IReadOnlyList<string> taskNodeIds, string taskNodeId)
    {
        if (!taskNodeIds.Contains(taskNodeId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Project structure node '{taskNodeId}' is not a canonical Gantt task.");
        }
    }

    private static async Task<ProjectStructureGanttViewState> LoadNormalizedGanttViewStateAsync(
        AppDbContext dbContext,
        Guid projectId,
        IReadOnlyList<string> taskNodeIds,
        CancellationToken cancellationToken)
    {
        var state = ProjectStructureGanttViewState.Parse(await LoadViewStateAsync(
            dbContext,
            projectId,
            GanttViewStateSurfaceKind,
            cancellationToken));
        return new ProjectStructureGanttViewState(state.ResolveOrderedTaskNodeIds(taskNodeIds));
    }

    private async Task PersistGanttViewStateAsync(
        AppDbContext dbContext,
        Guid projectId,
        ProjectStructureGanttViewState state,
        CancellationToken cancellationToken)
    {
        await UpsertViewStateAsync(
            dbContext,
            projectId,
            GanttViewStateSurfaceKind,
            state.ToJson(),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertViewStateAsync(
        AppDbContext dbContext,
        Guid projectId,
        string surfaceKind,
        string stateJson,
        CancellationToken cancellationToken)
    {
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
}


