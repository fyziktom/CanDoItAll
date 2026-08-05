using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectWorkbenchLifecycleService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    ProjectStructureRuntimeNodeMetadataBoundary runtimeMetadataBoundary)
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
        await using var mutationScope =
            await ProjectStructureSerializableMutationScope.BeginBindingWriteAsync(
                dbContext,
                ProjectStructureSerializableMutationScope.ForProject(projectId),
            cancellationToken);
        var node = await dbContext.Set<ProjectObjectRecord>()
            .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.NodeKey == nodeKey && !item.IsSystemManaged, cancellationToken);
        if (node is null)
        {
            return null;
        }

        await ProjectNodeBindingStorage.LoadAsync(dbContext, [node], cancellationToken);

        var sourceDescriptor = ProjectNodeKindRegistry.ResolveDescriptor(node.ObjectType, node.ObjectSubtype);
        var targetDescriptor = ProjectNodeKindRegistry.ResolveDescriptor(request.TargetObjectType, request.TargetObjectSubtype);
        if (!ProjectNodeKindRegistry.CanReclassify(node.ObjectType, node.ObjectSubtype, request.TargetObjectType, request.TargetObjectSubtype))
        {
            return null;
        }

        if (ProjectStructureTaskResourceGraphPolicy.IsResourceChildType(
                request.TargetObjectType) &&
            !string.IsNullOrWhiteSpace(node.ParentNodeKey))
        {
            var parent = await dbContext.Set<ProjectObjectRecord>()
                .FirstOrDefaultAsync(
                    candidate =>
                        candidate.ProjectId == projectId &&
                        candidate.NodeKey == node.ParentNodeKey,
                    cancellationToken);
            if (parent is not null)
            {
                ProjectStructureCanonicalTaskMutationPolicy
                    .EnsureGenericResourceAttachmentAllowed(
                        parent.ObjectType,
                        parent.ObjectSubtype);
            }
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
            ProgressMode = node.ProgressMode,
            ProgressPercent = node.ProgressPercent,
            MarkersJson = node.MarkersJson,
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
            UpdatedAtUtc = node.UpdatedAtUtc,
            Binding = node.Binding,
            NodeReferences = node.NodeReferences.Clone()
        };

        var targetObjectSubtype = ProjectObjectSubtypePolicy.Normalize(
            request.TargetObjectType,
            request.TargetObjectSubtype);
        var runtimeMetadataInput = ProjectWorkbenchObjectModeling.HasMeaningfulMetadata(request.MetadataJson)
            ? request.MetadataJson
            : sourceSnapshot.MetadataJson;
        var runtimeMetadataJson = runtimeMetadataBoundary.ValidateAndCanonicalize(
            request.TargetObjectType,
            targetObjectSubtype,
            request.Notes,
            runtimeMetadataInput);

        node.ObjectType = request.TargetObjectType;
        node.ObjectSubtype = targetObjectSubtype;
        node.Title = string.IsNullOrWhiteSpace(request.Title) ? node.Title : request.Title.Trim();
        node.Subtitle = request.Subtitle?.Trim() ?? string.Empty;
        node.Notes = request.Notes?.Trim() ?? string.Empty;
        if (request.UpdateTiming)
        {
            node.StartUtc = request.StartUtc;
            node.EndUtc = ProjectWorkbenchObjectModeling.ResolveEndUtc(
                request.StartUtc,
                request.EndUtc,
                request.DurationSeconds);
            node.DurationSeconds = ProjectWorkbenchObjectModeling.NormalizeDurationSeconds(
                request.DurationSeconds,
                node.StartUtc,
                node.EndUtc);
        }

        node.MetadataJson = ProjectWorkbenchObjectModeling.ResolveMetadataJson(
            node.ObjectType,
            node.ObjectSubtype,
            runtimeMetadataJson,
            null,
            node.Notes,
            null);
        node.MetadataJson =
            ProjectStructureCanonicalTaskCreationPolicy.NormalizeMetadataJson(
                node.ObjectType,
                node.ObjectSubtype,
                node.MetadataJson,
                ProjectObjectTaskPricingInitialization
                    .ClearAuthoritativePricing);
        node.Binding = node.Binding with
        {
            Route = string.IsNullOrWhiteSpace(node.Binding.Route)
                ? $"/projects/{projectId}/structure"
                : node.Binding.Route,
            ExternalArtifactKind = node.ObjectType.ToString()
        };

        var now = clock.GetUtcNow();
        node.UpdatedAtUtc = now;
        var bindingPlan = await ProjectNodeBindingStorage.PersistAsync(dbContext, node, cancellationToken);
        ProjectNodeBindingStorage.Apply(node, bindingPlan);
        await dbContext.Set<ProjectNodeLifecycleEventRecord>()
            .AddAsync(
                ProjectNodeTransitionHistory.CaptureReclassification(
                    projectId,
                    sourceSnapshot,
                    sourceDescriptor,
                    node,
                    targetDescriptor,
                    now),
                cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await ProjectStructureTaskResourceGraphPolicy
            .ReconcileAfterStructureMutationAsync(
                dbContext,
                projectId,
                string.IsNullOrWhiteSpace(sourceSnapshot.ParentNodeKey)
                    ? []
                    : [sourceSnapshot.ParentNodeKey],
                now,
                cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await mutationScope.CommitAsync(cancellationToken);
        return ProjectWorkbenchNodeMapper.MapStructureNode(node);
    }
}
