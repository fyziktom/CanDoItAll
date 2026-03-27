using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Validation;
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
    int Priority);

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
    ProjectObjectMediaPayload? Media = null);

public sealed record ProjectObjectSeedRequest(
    ProjectObjectType ObjectType,
    string Title,
    string Subtitle,
    string Notes,
    DateTimeOffset? StartUtc = null,
    DateTimeOffset? EndUtc = null,
    string? ObjectSubtype = null);

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
    public async Task<ProjectStructureSurface> GetStructureAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var project = await dbContext.Set<Project>().FirstAsync(item => item.Id == projectId, cancellationToken);
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

        return new ProjectStructureSurface(
            project.Id,
            project.Name,
            nodes.Select(MapStructureNode).ToList(),
            links.Select(link => new ProjectStructureLink(link.SourceNodeKey, link.TargetNodeKey, link.LinkKind, !link.IsSystemManaged)).ToList(),
            viewState);
    }

    public async Task<ProjectCalendarSurface> GetCalendarAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var project = await dbContext.Set<Project>().FirstAsync(item => item.Id == projectId, cancellationToken);
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

        return new ProjectCalendarSurface(project.Id, project.Name, events, preferredView, viewState);
    }

    public async Task<ProjectStructureNode> CreateObjectAsync(Guid projectId, ProjectObjectCreateRequest request, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var existingCount = await dbContext.Set<ProjectObjectRecord>().CountAsync(item => item.ProjectId == projectId && !item.IsSystemManaged, cancellationToken);
        var position = request.X.HasValue && request.Y.HasValue
            ? (request.X.Value, request.Y.Value)
            : GetDefaultPosition(request.ObjectType, existingCount + 1);
        var media = await SaveMediaAsync(projectId, request.ObjectType, request.Media, cancellationToken);
        var route = media?.Route ?? $"/projects/{projectId}/structure";
        var artifactKind = media?.ArtifactKind ?? request.ObjectType.ToString();

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
            ParentNodeKey = request.ParentNodeKey,
            PositionX = position.Item1,
            PositionY = position.Item2,
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc ?? request.StartUtc?.AddHours(1),
            CreatedAtUtc = clock.GetUtcNow(),
            UpdatedAtUtc = clock.GetUtcNow()
        };

        await dbContext.Set<ProjectObjectRecord>().AddAsync(record, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.ParentNodeKey))
        {
            await UpsertLinkAsync(dbContext, projectId, request.ParentNodeKey, record.NodeKey, ProjectObjectLinkKind.BelongsTo, isSystemManaged: false, cancellationToken);
        }

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
                PositionX = position.Item1,
                PositionY = position.Item2,
                StartUtc = seed.StartUtc,
                EndUtc = seed.EndUtc ?? seed.StartUtc?.AddHours(1),
                CreatedAtUtc = clock.GetUtcNow(),
                UpdatedAtUtc = clock.GetUtcNow()
            }, cancellationToken);

            await UpsertLinkAsync(dbContext, projectId, $"project:{projectId}", nodeKey, ProjectObjectLinkKind.Contains, isSystemManaged: false, cancellationToken);
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

    public async Task MoveObjectAsync(Guid projectId, string nodeKey, double x, double y, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var node = await dbContext.Set<ProjectObjectRecord>()
            .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.NodeKey == nodeKey, cancellationToken);
        if (node is null)
        {
            return;
        }

        node.PositionX = x;
        node.PositionY = y;
        node.UpdatedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
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

    public async Task<int> UpdateObjectStatusesAsync(
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys,
        string status,
        CancellationToken cancellationToken = default)
    {
        if (nodeKeys.Count == 0 || string.IsNullOrWhiteSpace(status))
        {
            return 0;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var normalizedKeys = nodeKeys.Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.Ordinal).ToList();
        if (normalizedKeys.Count == 0)
        {
            return 0;
        }

        var nodes = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId &&
                !item.IsSystemManaged &&
                normalizedKeys.Contains(item.NodeKey))
            .ToListAsync(cancellationToken);

        foreach (var node in nodes)
        {
            node.Status = status.Trim();
            var progress = ResolveStatusBackedProgress(status);
            node.ProgressMode = progress.Mode;
            node.ProgressPercent = progress.Percent;
            node.UpdatedAtUtc = clock.GetUtcNow();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return nodes.Count;
    }

    public async Task<int> UpdateObjectProgressAsync(
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys,
        string progressMode,
        int progressPercent,
        CancellationToken cancellationToken = default)
    {
        if (nodeKeys.Count == 0)
        {
            return 0;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var normalizedKeys = nodeKeys.Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.Ordinal).ToList();
        if (normalizedKeys.Count == 0)
        {
            return 0;
        }

        var nodes = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId && normalizedKeys.Contains(item.NodeKey))
            .ToListAsync(cancellationToken);

        var normalizedMode = NormalizeProgressMode(progressMode);
        var normalizedPercent = Math.Clamp(progressPercent, 0, 100);
        foreach (var node in nodes)
        {
            node.ProgressMode = normalizedMode;
            node.ProgressPercent = normalizedPercent;
            node.UpdatedAtUtc = clock.GetUtcNow();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return nodes.Count;
    }

    public async Task<int> UpdateObjectMarkerAsync(
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys,
        string markerIcon,
        string markerTone,
        string markerLabel,
        CancellationToken cancellationToken = default)
    {
        if (nodeKeys.Count == 0)
        {
            return 0;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var normalizedKeys = nodeKeys.Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.Ordinal).ToList();
        if (normalizedKeys.Count == 0)
        {
            return 0;
        }

        var nodes = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId && normalizedKeys.Contains(item.NodeKey))
            .ToListAsync(cancellationToken);

        foreach (var node in nodes)
        {
            node.MarkerIcon = markerIcon?.Trim() ?? string.Empty;
            node.MarkerTone = markerTone?.Trim() ?? string.Empty;
            node.MarkerLabel = markerLabel?.Trim() ?? string.Empty;
            node.UpdatedAtUtc = clock.GetUtcNow();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return nodes.Count;
    }

    public async Task<int> UpdateObjectPriorityAsync(
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys,
        int priority,
        CancellationToken cancellationToken = default)
    {
        if (nodeKeys.Count == 0)
        {
            return 0;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var normalizedKeys = nodeKeys.Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.Ordinal).ToList();
        if (normalizedKeys.Count == 0)
        {
            return 0;
        }

        var nodes = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId && normalizedKeys.Contains(item.NodeKey))
            .ToListAsync(cancellationToken);

        var normalizedPriority = Math.Clamp(priority, 0, 6);
        foreach (var node in nodes)
        {
            node.Priority = normalizedPriority;
            node.UpdatedAtUtc = clock.GetUtcNow();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return nodes.Count;
    }

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

        var expectedNodes = new List<ProjectObjectRecord>
        {
            new()
            {
                ProjectId = projectId,
                NodeKey = $"project:{project.Id}",
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
            ParentNodeKey = $"project:{project.Id}",
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
            ParentNodeKey = $"project:{project.Id}",
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
                : $"project:{project.Id}",
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
            ParentNodeKey = $"project:{project.Id}",
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
            ParentNodeKey = $"project:{project.Id}",
            PositionX = 1100,
            PositionY = 620 + (index * 140),
            StartUtc = testPlan.UpdatedAtUtc,
            EndUtc = testPlan.UpdatedAtUtc.AddHours(1),
            IsSystemManaged = true,
            CreatedAtUtc = testPlan.CreatedAtUtc,
            UpdatedAtUtc = clock.GetUtcNow()
        }));

        var expectedLinks = new List<(string Source, string Target, ProjectObjectLinkKind Kind)>();
        expectedLinks.AddRange(phases.Select(phase => ($"project:{project.Id}", $"phase:{phase.Id}", ProjectObjectLinkKind.Contains)));
        expectedLinks.AddRange(resources.Select(resource => ($"project:{project.Id}", $"resource:{resource.Id}", ProjectObjectLinkKind.Uses)));
        expectedLinks.AddRange(validations.Select(validation => ($"project:{project.Id}", $"validation:{validation.Id}", ProjectObjectLinkKind.Validates)));
        expectedLinks.AddRange(testPlans.Select(testPlan => ($"project:{project.Id}", $"test-plan:{testPlan.Id}", ProjectObjectLinkKind.Tests)));

        foreach (var run in runs)
        {
            var phaseNodeKey = phases.FirstOrDefault(phase => string.Equals(phase.Name, run.Phase, StringComparison.OrdinalIgnoreCase)) is { } phase
                ? $"phase:{phase.Id}"
                : $"project:{project.Id}";
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

    private static ProjectStructureNode MapStructureNode(ProjectObjectRecord record)
    {
        var profile = ResolveVisualProfile(record.ObjectType, record.ObjectSubtype, record.Status);
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
            record.MarkerIcon,
            record.MarkerTone,
            record.MarkerLabel,
            record.Priority);
    }

    private static string ResolveSubtypeBadge(ProjectObjectType objectType, string objectSubtype) => objectType switch
    {
        ProjectObjectType.ProjectBlock => ResolveBlockSubtypeLabel(objectSubtype),
        _ => objectSubtype
    };

    private static ProjectObjectVisualProfile ResolveVisualProfile(ProjectObjectType objectType, string objectSubtype, string status) => objectType switch
    {
        ProjectObjectType.ProjectRoot => new("hex", "#0f172a", "PR", "Project"),
        ProjectObjectType.Phase => new("pill", "#2563eb", "PH", "Phase"),
        ProjectObjectType.Milestone => new("diamond", "#d97706", "MS", "Milestone"),
        ProjectObjectType.ProjectBlock => ResolveProjectBlockVisualProfile(objectSubtype),
        ProjectObjectType.Repository => new("rect", "#0891b2", "RE", "Repo"),
        ProjectObjectType.File => new("rect", "#14b8a6", "FI", "File"),
        ProjectObjectType.ImageAsset => new("rect", "#ec4899", "IM", "Image"),
        ProjectObjectType.VideoAsset => new("rect", "#7c3aed", "VD", "Video"),
        ProjectObjectType.Link => new("circle", "#38bdf8", "LN", "Link"),
        ProjectObjectType.Connector => new("circle", "#8b5cf6", "CN", "Connector"),
        ProjectObjectType.PromptFlow or ProjectObjectType.PromptSession => new("hex", "#0f766e", "PF", "Prompt"),
        ProjectObjectType.PromptStep => new("pill", "#14b8a6", "ST", "Step"),
        ProjectObjectType.ValidationRun => new("diamond", status.Contains("Approved", StringComparison.OrdinalIgnoreCase) ? "#16a34a" : "#dc2626", "VL", "Validate"),
        ProjectObjectType.TestPlan or ProjectObjectType.TestEvidence => new("diamond", "#7c3aed", "TS", "Test"),
        ProjectObjectType.Decision => new("hex", "#ea580c", "DC", "Decision"),
        ProjectObjectType.SecretReference => new("shield", "#be123c", "SC", "Secret"),
        _ => new("rect", "#475569", "NT", "Note")
    };

    private static (double X, double Y) GetDefaultPosition(ProjectObjectType objectType, int index)
        => objectType switch
        {
            ProjectObjectType.ProjectRoot => (140, 240),
            ProjectObjectType.Phase => (420, 120 + (index * 150)),
            ProjectObjectType.ProjectBlock => (760, 420 + (index * 110)),
            ProjectObjectType.Repository or ProjectObjectType.File or ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset or ProjectObjectType.Link or ProjectObjectType.Connector or ProjectObjectType.SecretReference => (760, 100 + (index * 120)),
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
            "feature" => new("hex", "#2563eb", "FB", "Feature"),
            "architecture" => new("hex", "#4f46e5", "AR", "Architecture"),
            "implementation" => new("hex", "#0f766e", "IM", "Implementation"),
            "revision" => new("hex", "#f97316", "RB", "Revision"),
            "testing" => new("hex", "#7c3aed", "TB", "Testing"),
            "prompting" => new("hex", "#0f766e", "PB", "Prompting"),
            "financial" => new("hex", "#16a34a", "FN", "Financial"),
            "marketing" => new("hex", "#db2777", "MK", "Marketing"),
            "research" => new("hex", "#0891b2", "RS", "Research"),
            "delivery" => new("hex", "#d97706", "DL", "Delivery"),
            "operations" => new("hex", "#475569", "OP", "Operations"),
            "risk" => new("hex", "#dc2626", "RK", "Risk"),
            "compliance" => new("hex", "#7c2d12", "CP", "Compliance"),
            "support" => new("hex", "#0284c7", "SP", "Support"),
            _ => new("hex", "#334155", "BL", "Block")
        };

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


