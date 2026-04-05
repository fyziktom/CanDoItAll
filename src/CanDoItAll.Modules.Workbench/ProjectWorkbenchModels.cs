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
    public string StorageObjectReferenceJson { get; set; } = string.Empty;
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
    public int? DurationSeconds { get; set; }
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
        builder.Property(item => item.StorageObjectReferenceJson).HasColumnType("TEXT");
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
    string StorageObjectReferenceJson = "",
    ProjectStructureProjectRole ProjectRole = ProjectStructureProjectRole.None,
    Guid? RelatedProjectId = null,
    int ParentProjectCount = 0,
    int? DurationSeconds = null);

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
    string? MetadataJson = null,
    int? DurationSeconds = null);

public sealed record ProjectObjectEditRequest(
    string Title,
    string Subtitle,
    string Notes,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    string MetadataJson,
    int? DurationSeconds = null);

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
public sealed class ProjectWorkbenchService(
IDbContextFactory<AppDbContext> dbContextFactory,
IClock clock,
IStoragePlacementService storagePlacementService,
ProjectStructureAssemblyService projectStructureAssemblyService,
ProjectWorkbenchRelationService relationService,
ProjectWorkbenchLifecycleService lifecycleService,
ProjectWorkbenchCommandService commandService,
ProjectWorkbenchCrossModuleMutationService crossModuleMutationService) : IProjectWorkbenchSeedService
{
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
                assembly.Links.Select(link => new ProjectStructureLink(link.SourceNodeKey, link.TargetNodeKey, link.LinkKind, !link.IsSystemManaged)).ToList(),
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
                item.Title,
                item.StartUtc!.Value,
                item.EndUtc!.Value,
                item.Status,
                item.Route,
                item.ExternalArtifactKind,
                item.ExternalArtifactId,
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

        var existingCount = existingNodes.Count(item => !item.IsSystemManaged);
        var position = request.X.HasValue && request.Y.HasValue
            ? (request.X.Value, request.Y.Value)
            : ProjectWorkbenchGraphConventions.GetDefaultPosition(request.ObjectType, existingCount + 1);
        var media = await SaveMediaAsync(projectId, request.ObjectType, request.Media, cancellationToken);
        var route = media?.Route ?? $"/projects/{projectId}/structure";
        var artifactKind = media?.ArtifactKind ?? request.ObjectType.ToString();
        var metadataJson = ProjectWorkbenchObjectModeling.ResolveMetadataJson(request.ObjectType, request.ObjectSubtype, request.MetadataJson, null, request.Notes, media);
        var resolvedEndUtc = ProjectWorkbenchObjectModeling.ResolveEndUtc(request.StartUtc, request.EndUtc, request.DurationSeconds);
        var normalizedDurationSeconds = ProjectWorkbenchObjectModeling.NormalizeDurationSeconds(request.DurationSeconds, request.StartUtc, resolvedEndUtc);

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
            StorageObjectReferenceJson = media?.StorageObjectReferenceJson ?? string.Empty,
            ProgressMode = "progress",
            ProgressPercent = 0,
            MetadataJson = metadataJson,
            ParentNodeKey = normalizedParentNodeKey,
            PositionX = position.Item1,
            PositionY = position.Item2,
            StartUtc = request.StartUtc,
            EndUtc = resolvedEndUtc,
            DurationSeconds = normalizedDurationSeconds,
            CreatedAtUtc = clock.GetUtcNow(),
            UpdatedAtUtc = clock.GetUtcNow()
        };

        await dbContext.Set<ProjectObjectRecord>().AddAsync(record, cancellationToken);

        if (request.ObjectType == ProjectObjectType.PromptFlow)
        {
            await commandService.EnsurePromptFlowWizardAsync(dbContext, projectId, record, cancellationToken);
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
                Route = $"/projects/{projectId}/structure",
                ExternalArtifactKind = seed.ObjectType.ToString(),
                ObjectSubtype = seed.ObjectSubtype?.Trim() ?? string.Empty,
                ProgressMode = "progress",
                ProgressPercent = 0,
                MetadataJson = ProjectWorkbenchObjectModeling.ResolveMetadataJson(seed.ObjectType, seed.ObjectSubtype, seed.MetadataJson, null, seed.Notes, null),
                ParentNodeKey = projectRootNodeKey,
                PositionX = position.Item1,
                PositionY = position.Item2,
                StartUtc = seed.StartUtc,
                EndUtc = resolvedEndUtc,
                DurationSeconds = normalizedDurationSeconds,
                CreatedAtUtc = clock.GetUtcNow(),
                UpdatedAtUtc = clock.GetUtcNow()
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
        await ProjectNodeBindingStorage.NormalizeAndHydrateAsync(dbContext, [node], cancellationToken);

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
        await ProjectNodeBindingStorage.NormalizeAndHydrateAsync(dbContext, [node], cancellationToken);

        node.Title = string.IsNullOrWhiteSpace(request.Title) ? node.Title : request.Title.Trim();
        node.Subtitle = request.Subtitle?.Trim() ?? string.Empty;
        node.Notes = request.Notes?.Trim() ?? string.Empty;
        node.StartUtc = request.StartUtc;
        node.EndUtc = ProjectWorkbenchObjectModeling.ResolveEndUtc(request.StartUtc, request.EndUtc, request.DurationSeconds);
        node.DurationSeconds = ProjectWorkbenchObjectModeling.NormalizeDurationSeconds(request.DurationSeconds, node.StartUtc, node.EndUtc);
        node.MetadataJson = ProjectWorkbenchObjectModeling.ResolveMetadataJson(node.ObjectType, node.ObjectSubtype, request.MetadataJson, node.MetadataJson, node.Notes, null);
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
        await ProjectNodeBindingStorage.NormalizeAndHydrateAsync(dbContext, [node], cancellationToken);

        if (notes is not null)
        {
            node.Notes = notes.Trim();
        }

        node.MetadataJson = ProjectWorkbenchObjectModeling.ResolveMetadataJson(node.ObjectType, node.ObjectSubtype, metadataJson, node.MetadataJson, node.Notes, null);

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
        await ProjectNodeBindingStorage.NormalizeAndHydrateAsync(dbContext, nodes, cancellationToken);
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
        await ProjectNodeBindingStorage.NormalizeAndHydrateAsync(dbContext, nodes, cancellationToken);
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
            var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
            var existingMarkers = ProjectObjectMetadataSerializer.ResolveMarkers(metadata, node.MarkerIcon, node.MarkerTone, node.MarkerLabel);
            var updatedMarkers = mutationMode switch
            {
                ProjectMarkerMutationMode.ReplaceAll => normalizedMarker is null ? [] : [normalizedMarker],
                ProjectMarkerMutationMode.Add => ProjectWorkbenchObjectModeling.AddMarker(existingMarkers, normalizedMarker),
                ProjectMarkerMutationMode.Remove => ProjectWorkbenchObjectModeling.RemoveMarker(existingMarkers, normalizedMarker),
                ProjectMarkerMutationMode.Toggle => ProjectWorkbenchObjectModeling.ToggleMarker(existingMarkers, normalizedMarker),
                ProjectMarkerMutationMode.ClearAll => [],
                _ => existingMarkers
            };
            ProjectObjectMetadataSerializer.SetMarkers(metadata, updatedMarkers);
            node.MetadataJson = ProjectObjectMetadataSerializer.Serialize(metadata);
            ProjectWorkbenchObjectModeling.ApplyPrimaryMarker(node, updatedMarkers);
            node.UpdatedAtUtc = updatedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await ProjectNodeBindingStorage.NormalizeAndHydrateAsync(dbContext, nodes, cancellationToken);
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
        await ProjectNodeBindingStorage.NormalizeAndHydrateAsync(dbContext, nodes, cancellationToken);
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


