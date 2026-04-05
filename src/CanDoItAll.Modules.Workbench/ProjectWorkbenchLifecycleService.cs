using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectWorkbenchLifecycleService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock)
{
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
        if (node is null)
        {
            return null;
        }

        await ProjectNodeBindingStorage.NormalizeAndHydrateAsync(dbContext, [node], cancellationToken);

        var sourceDescriptor = ProjectNodeKindRegistry.ResolveDescriptor(node.ObjectType, node.ObjectSubtype);
        var targetDescriptor = ProjectNodeKindRegistry.ResolveDescriptor(request.TargetObjectType, request.TargetObjectSubtype);
        if (!ProjectNodeKindRegistry.CanReclassify(node.ObjectType, node.ObjectSubtype, request.TargetObjectType, request.TargetObjectSubtype))
        {
            return null;
        }

        var sourceSnapshot = new ProjectObjectRecord
        {
            Id = node.Id,
            ProjectId = node.ProjectId,
            NodeKey = node.NodeKey,
            ObjectType = node.ObjectType,
            ObjectSubtype = node.ObjectSubtype,
            Title = node.Title,
            Subtitle = node.Subtitle,
            Status = node.Status,
            Notes = node.Notes,
            Route = node.Route,
            ExternalArtifactKind = node.ExternalArtifactKind,
            ExternalArtifactId = node.ExternalArtifactId,
            MediaRelativePath = node.MediaRelativePath,
            MediaContentType = node.MediaContentType,
            MediaOriginalFileName = node.MediaOriginalFileName,
            StorageObjectReferenceJson = node.StorageObjectReferenceJson,
            ProgressMode = node.ProgressMode,
            ProgressPercent = node.ProgressPercent,
            MarkerIcon = node.MarkerIcon,
            MarkerTone = node.MarkerTone,
            MarkerLabel = node.MarkerLabel,
            Priority = node.Priority,
            MetadataJson = node.MetadataJson,
            ParentNodeKey = node.ParentNodeKey,
            PositionX = node.PositionX,
            PositionY = node.PositionY,
            StartUtc = node.StartUtc,
            EndUtc = node.EndUtc,
            DurationSeconds = node.DurationSeconds,
            IsSystemManaged = node.IsSystemManaged,
            CreatedAtUtc = node.CreatedAtUtc,
            UpdatedAtUtc = node.UpdatedAtUtc
        };

        node.ObjectType = request.TargetObjectType;
        node.ObjectSubtype = request.TargetObjectSubtype?.Trim() ?? string.Empty;
        node.Title = string.IsNullOrWhiteSpace(request.Title) ? node.Title : request.Title.Trim();
        node.Subtitle = request.Subtitle?.Trim() ?? string.Empty;
        node.Notes = request.Notes?.Trim() ?? string.Empty;
        node.MetadataJson = ProjectWorkbenchObjectModeling.ResolveMetadataJson(
            node.ObjectType,
            node.ObjectSubtype,
            request.MetadataJson,
            sourceSnapshot.MetadataJson,
            node.Notes,
            null);
        node.ExternalArtifactKind = node.ObjectType.ToString();
        if (string.IsNullOrWhiteSpace(node.Route))
        {
            node.Route = $"/projects/{projectId}/structure";
        }

        var now = clock.GetUtcNow();
        node.UpdatedAtUtc = now;
        var bindingPlan = await ProjectNodeBindingStorage.PersistAsync(dbContext, node, cancellationToken);
        ProjectNodeBindingStorage.Apply(node, bindingPlan);
        await dbContext.Set<ProjectNodeLifecycleEventRecord>()
            .AddAsync(
                ProjectNodeLifecycleHistory.CaptureReclassification(
                    projectId,
                    sourceSnapshot,
                    sourceDescriptor,
                    node,
                    targetDescriptor,
                    now),
                cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ProjectWorkbenchNodeMapper.MapStructureNode(node);
    }
}
