using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Workbench;

public enum ProjectNodeReferenceKind
{
    MeetingParticipant,
    RecordingMeetingNode,
    RecordingTranscriptNode,
    TranscriptRecordingNode,
    TranscriptProviderProfile,
    ParticipantParentParticipant,
    WorkItemAssigneeParticipant,
    WorkItemRepositoryResource,
    RepositoryResource,
    EnvironmentRepositoryResource,
    InfrastructureSecretReference,
    InfrastructureStorageCatalog
}

public sealed class ProjectNodeBindingRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectObjectId { get; set; }
    public string Route { get; set; } = string.Empty;
    public string ExternalArtifactKind { get; set; } = string.Empty;
    public Guid? ExternalArtifactId { get; set; }
    public string MediaRelativePath { get; set; } = string.Empty;
    public string MediaContentType { get; set; } = string.Empty;
    public string MediaOriginalFileName { get; set; } = string.Empty;
    public string StorageObjectReferenceJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class ProjectNodeBindingRecordConfiguration : IEntityTypeConfiguration<ProjectNodeBindingRecord>
{
    public void Configure(EntityTypeBuilder<ProjectNodeBindingRecord> builder)
    {
        builder.ToTable("Workbench_ProjectNodeBindings");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Route).HasMaxLength(800);
        builder.Property(item => item.ExternalArtifactKind).HasMaxLength(120);
        builder.Property(item => item.MediaRelativePath).HasMaxLength(800);
        builder.Property(item => item.MediaContentType).HasMaxLength(160);
        builder.Property(item => item.MediaOriginalFileName).HasMaxLength(260);
        builder.Property(item => item.StorageObjectReferenceJson).HasColumnType("TEXT");
        builder.HasIndex(item => item.ProjectObjectId).IsUnique();
        builder.HasOne<ProjectObjectRecord>()
            .WithOne()
            .HasForeignKey<ProjectNodeBindingRecord>(item => item.ProjectObjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProjectNodeReferenceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectObjectId { get; set; }
    public ProjectNodeReferenceKind ReferenceKind { get; set; }
    public Guid ReferenceId { get; set; }
    public int OrderIndex { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class ProjectNodeReferenceRecordConfiguration : IEntityTypeConfiguration<ProjectNodeReferenceRecord>
{
    public void Configure(EntityTypeBuilder<ProjectNodeReferenceRecord> builder)
    {
        builder.ToTable("Workbench_ProjectNodeReferences");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ProjectObjectId, item.ReferenceKind, item.ReferenceId }).IsUnique();
        builder.HasIndex(item => new { item.ProjectObjectId, item.ReferenceKind, item.OrderIndex });
        builder.HasOne<ProjectObjectRecord>()
            .WithMany()
            .HasForeignKey(item => item.ProjectObjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed record ProjectNodeBindingReferencePayload(
    ProjectNodeReferenceKind ReferenceKind,
    Guid ReferenceId,
    int OrderIndex);

internal sealed record ProjectNodeBindingSnapshot(
    string Route,
    string ExternalArtifactKind,
    Guid? ExternalArtifactId,
    string MediaRelativePath,
    string MediaContentType,
    string MediaOriginalFileName,
    string StorageObjectReferenceJson);

internal sealed record ProjectNodeBindingPersistencePlan(
    string SanitizedMetadataJson,
    ProjectNodeBindingSnapshot Binding,
    IReadOnlyList<ProjectNodeBindingReferencePayload> References);

internal static class ProjectNodeBindingStorage
{
    public static async Task NormalizeAndHydrateAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<ProjectObjectRecord> nodes,
        CancellationToken cancellationToken = default)
    {
        if (nodes.Count == 0)
        {
            return;
        }

        var nodeIds = nodes
            .Select(item => item.Id)
            .Distinct()
            .ToArray();
        var bindingByNodeId = await dbContext.Set<ProjectNodeBindingRecord>()
            .Where(item => nodeIds.Contains(item.ProjectObjectId))
            .ToDictionaryAsync(item => item.ProjectObjectId, cancellationToken);
        var referenceRows = await dbContext.Set<ProjectNodeReferenceRecord>()
            .Where(item => nodeIds.Contains(item.ProjectObjectId))
            .OrderBy(item => item.ReferenceKind)
            .ThenBy(item => item.OrderIndex)
            .ToListAsync(cancellationToken);
        var referencesByNodeId = referenceRows
            .GroupBy(item => item.ProjectObjectId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ProjectNodeReferenceRecord>)group.ToList());

        var changed = false;
        foreach (var node in nodes)
        {
            var binding = bindingByNodeId.GetValueOrDefault(node.Id);
            var references = referencesByNodeId.GetValueOrDefault(node.Id) ?? [];
            if (!RequiresNormalization(node, binding, references))
            {
                continue;
            }

            var plan = CreatePersistencePlan(node);
            UpsertBindingRecord(dbContext, bindingByNodeId, node, plan);
            ReplaceReferenceRecords(dbContext, referencesByNodeId, node, plan, referenceRows);
            StripCarrierPayload(node, plan.SanitizedMetadataJson);
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            bindingByNodeId = await dbContext.Set<ProjectNodeBindingRecord>()
                .Where(item => nodeIds.Contains(item.ProjectObjectId))
                .ToDictionaryAsync(item => item.ProjectObjectId, cancellationToken);
            referenceRows = await dbContext.Set<ProjectNodeReferenceRecord>()
                .Where(item => nodeIds.Contains(item.ProjectObjectId))
                .OrderBy(item => item.ReferenceKind)
                .ThenBy(item => item.OrderIndex)
                .ToListAsync(cancellationToken);
            referencesByNodeId = referenceRows
                .GroupBy(item => item.ProjectObjectId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<ProjectNodeReferenceRecord>)group.ToList());
        }

        foreach (var node in nodes)
        {
            Apply(
                node,
                bindingByNodeId.GetValueOrDefault(node.Id),
                referencesByNodeId.GetValueOrDefault(node.Id) ?? []);
        }
    }

    public static async Task<ProjectNodeBindingPersistencePlan> PersistAsync(
        AppDbContext dbContext,
        ProjectObjectRecord node,
        CancellationToken cancellationToken = default)
    {
        var plan = CreatePersistencePlan(node);
        var existingBinding = await dbContext.Set<ProjectNodeBindingRecord>()
            .FirstOrDefaultAsync(item => item.ProjectObjectId == node.Id, cancellationToken);
        UpsertBindingRecord(dbContext, existingBinding, node, plan);

        var existingReferences = await dbContext.Set<ProjectNodeReferenceRecord>()
            .Where(item => item.ProjectObjectId == node.Id)
            .ToListAsync(cancellationToken);
        ReplaceReferenceRecords(dbContext, node, plan, existingReferences);
        StripCarrierPayload(node, plan.SanitizedMetadataJson);
        return plan;
    }

    public static void Apply(ProjectObjectRecord node, ProjectNodeBindingPersistencePlan plan)
    {
        Apply(node, plan.Binding, plan.References);
    }

    public static void Apply(
        ProjectObjectRecord node,
        ProjectNodeBindingRecord? binding,
        IReadOnlyList<ProjectNodeReferenceRecord> references)
    {
        Apply(
            node,
            binding is null ? null : new ProjectNodeBindingSnapshot(
                binding.Route,
                binding.ExternalArtifactKind,
                binding.ExternalArtifactId,
                binding.MediaRelativePath,
                binding.MediaContentType,
                binding.MediaOriginalFileName,
                binding.StorageObjectReferenceJson),
            references
                .Select(item => new ProjectNodeBindingReferencePayload(item.ReferenceKind, item.ReferenceId, item.OrderIndex))
                .ToList());
    }

    public static void Apply(
        ProjectObjectRecord node,
        ProjectNodeBindingSnapshot? binding,
        IReadOnlyList<ProjectNodeBindingReferencePayload> references)
    {
        var effectiveBinding = binding ?? BuildFallbackBinding(node);
        node.Route = effectiveBinding.Route;
        node.ExternalArtifactKind = effectiveBinding.ExternalArtifactKind;
        node.ExternalArtifactId = effectiveBinding.ExternalArtifactId;
        node.MediaRelativePath = effectiveBinding.MediaRelativePath;
        node.MediaContentType = effectiveBinding.MediaContentType;
        node.MediaOriginalFileName = effectiveBinding.MediaOriginalFileName;
        node.StorageObjectReferenceJson = effectiveBinding.StorageObjectReferenceJson;

        var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
        ApplyReferences(metadata, references);
        node.MetadataJson = ProjectObjectMetadataSerializer.Serialize(metadata);
    }

    public static bool HasForeignReferencePayload(ProjectObjectMetadataEnvelope metadata)
    {
        return (metadata.Meeting?.ParticipantIds.Count ?? 0) > 0 ||
               metadata.Recording?.MeetingNodeArtifactId.HasValue == true ||
               metadata.Recording?.TranscriptNodeArtifactId.HasValue == true ||
               metadata.Transcript?.RecordingNodeArtifactId.HasValue == true ||
               metadata.Transcript?.LastProviderProfileId.HasValue == true ||
               metadata.Participant?.ParentParticipantArtifactId.HasValue == true ||
               metadata.WorkItem?.AssigneeParticipantArtifactId.HasValue == true ||
               metadata.WorkItem?.RepositoryResourceId.HasValue == true ||
               metadata.Repository?.ResourceId.HasValue == true ||
               metadata.Environment?.RepositoryResourceId.HasValue == true ||
               metadata.Infrastructure?.SecretReferenceArtifactId.HasValue == true ||
               metadata.Infrastructure?.StorageCatalogId.HasValue == true;
    }

    private static bool RequiresNormalization(
        ProjectObjectRecord node,
        ProjectNodeBindingRecord? binding,
        IReadOnlyList<ProjectNodeReferenceRecord> references)
    {
        if (binding is null)
        {
            return true;
        }

        if (references.Count > 0)
        {
            return HasForeignReferencePayload(ProjectObjectMetadataSerializer.Parse(node.MetadataJson));
        }

        return !string.IsNullOrWhiteSpace(node.Route) ||
               !string.IsNullOrWhiteSpace(node.ExternalArtifactKind) ||
               node.ExternalArtifactId.HasValue ||
               !string.IsNullOrWhiteSpace(node.MediaRelativePath) ||
               !string.IsNullOrWhiteSpace(node.MediaContentType) ||
               !string.IsNullOrWhiteSpace(node.MediaOriginalFileName) ||
               !string.IsNullOrWhiteSpace(node.StorageObjectReferenceJson) ||
               HasForeignReferencePayload(ProjectObjectMetadataSerializer.Parse(node.MetadataJson));
    }

    private static ProjectNodeBindingPersistencePlan CreatePersistencePlan(ProjectObjectRecord node)
    {
        var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
        var references = ExtractReferences(metadata);
        ClearReferences(metadata);
        ValidateSanitizedMetadata(node.ObjectType, node.ObjectSubtype, metadata);
        return new ProjectNodeBindingPersistencePlan(
            ProjectObjectMetadataSerializer.Serialize(metadata),
            new ProjectNodeBindingSnapshot(
                ResolveRoute(node),
                ResolveArtifactKind(node),
                node.ExternalArtifactId,
                node.MediaRelativePath ?? string.Empty,
                node.MediaContentType ?? string.Empty,
                node.MediaOriginalFileName ?? string.Empty,
                node.StorageObjectReferenceJson ?? string.Empty),
            references);
    }

    private static void ValidateSanitizedMetadata(
        ProjectObjectType objectType,
        string objectSubtype,
        ProjectObjectMetadataEnvelope metadata)
    {
        ProjectObjectMetadataSerializer.Validate(objectType, objectSubtype, metadata);
        if (HasForeignReferencePayload(metadata))
        {
            throw new InvalidOperationException("Persisted metadata cannot retain foreign-owner references.");
        }
    }

    private static string ResolveRoute(ProjectObjectRecord node)
    {
        return string.IsNullOrWhiteSpace(node.Route)
            ? $"/projects/{node.ProjectId}/structure"
            : node.Route.Trim();
    }

    private static string ResolveArtifactKind(ProjectObjectRecord node)
    {
        return string.IsNullOrWhiteSpace(node.ExternalArtifactKind)
            ? node.ObjectType.ToString()
            : node.ExternalArtifactKind.Trim();
    }

    private static ProjectNodeBindingSnapshot BuildFallbackBinding(ProjectObjectRecord node)
    {
        return new ProjectNodeBindingSnapshot(
            ResolveRoute(node),
            ResolveArtifactKind(node),
            node.ExternalArtifactId,
            node.MediaRelativePath ?? string.Empty,
            node.MediaContentType ?? string.Empty,
            node.MediaOriginalFileName ?? string.Empty,
            node.StorageObjectReferenceJson ?? string.Empty);
    }

    private static void UpsertBindingRecord(
        AppDbContext dbContext,
        Dictionary<Guid, ProjectNodeBindingRecord> bindingByNodeId,
        ProjectObjectRecord node,
        ProjectNodeBindingPersistencePlan plan)
    {
        if (bindingByNodeId.TryGetValue(node.Id, out var existingBinding))
        {
            ApplyBinding(existingBinding, plan.Binding);
            existingBinding.UpdatedAtUtc = node.UpdatedAtUtc;
            return;
        }

        var binding = new ProjectNodeBindingRecord
        {
            ProjectObjectId = node.Id,
            Route = plan.Binding.Route,
            ExternalArtifactKind = plan.Binding.ExternalArtifactKind,
            ExternalArtifactId = plan.Binding.ExternalArtifactId,
            MediaRelativePath = plan.Binding.MediaRelativePath,
            MediaContentType = plan.Binding.MediaContentType,
            MediaOriginalFileName = plan.Binding.MediaOriginalFileName,
            StorageObjectReferenceJson = plan.Binding.StorageObjectReferenceJson,
            CreatedAtUtc = node.CreatedAtUtc,
            UpdatedAtUtc = node.UpdatedAtUtc
        };
        bindingByNodeId[node.Id] = binding;
        dbContext.Set<ProjectNodeBindingRecord>().Add(binding);
    }

    private static void UpsertBindingRecord(
        AppDbContext dbContext,
        ProjectNodeBindingRecord? existingBinding,
        ProjectObjectRecord node,
        ProjectNodeBindingPersistencePlan plan)
    {
        if (existingBinding is not null)
        {
            ApplyBinding(existingBinding, plan.Binding);
            existingBinding.UpdatedAtUtc = node.UpdatedAtUtc;
            return;
        }

        dbContext.Set<ProjectNodeBindingRecord>().Add(new ProjectNodeBindingRecord
        {
            ProjectObjectId = node.Id,
            Route = plan.Binding.Route,
            ExternalArtifactKind = plan.Binding.ExternalArtifactKind,
            ExternalArtifactId = plan.Binding.ExternalArtifactId,
            MediaRelativePath = plan.Binding.MediaRelativePath,
            MediaContentType = plan.Binding.MediaContentType,
            MediaOriginalFileName = plan.Binding.MediaOriginalFileName,
            StorageObjectReferenceJson = plan.Binding.StorageObjectReferenceJson,
            CreatedAtUtc = node.CreatedAtUtc,
            UpdatedAtUtc = node.UpdatedAtUtc
        });
    }

    private static void ReplaceReferenceRecords(
        AppDbContext dbContext,
        Dictionary<Guid, IReadOnlyList<ProjectNodeReferenceRecord>> referencesByNodeId,
        ProjectObjectRecord node,
        ProjectNodeBindingPersistencePlan plan,
        List<ProjectNodeReferenceRecord> referenceRows)
    {
        var existingReferences = referencesByNodeId.GetValueOrDefault(node.Id) ?? [];
        ReplaceReferenceRecords(dbContext, node, plan, existingReferences);

        referencesByNodeId[node.Id] = plan.References
            .Select(reference => new ProjectNodeReferenceRecord
            {
                ProjectObjectId = node.Id,
                ReferenceKind = reference.ReferenceKind,
                ReferenceId = reference.ReferenceId,
                OrderIndex = reference.OrderIndex,
                CreatedAtUtc = node.UpdatedAtUtc
            })
            .ToList();
        referenceRows.RemoveAll(item => item.ProjectObjectId == node.Id);
        referenceRows.AddRange(referencesByNodeId[node.Id]);
    }

    private static void ReplaceReferenceRecords(
        AppDbContext dbContext,
        ProjectObjectRecord node,
        ProjectNodeBindingPersistencePlan plan,
        IReadOnlyList<ProjectNodeReferenceRecord> existingReferences)
    {
        if (existingReferences.Count > 0)
        {
            dbContext.RemoveRange(existingReferences);
        }

        foreach (var reference in plan.References)
        {
            dbContext.Set<ProjectNodeReferenceRecord>().Add(new ProjectNodeReferenceRecord
            {
                ProjectObjectId = node.Id,
                ReferenceKind = reference.ReferenceKind,
                ReferenceId = reference.ReferenceId,
                OrderIndex = reference.OrderIndex,
                CreatedAtUtc = node.UpdatedAtUtc
            });
        }
    }

    private static void ApplyBinding(ProjectNodeBindingRecord binding, ProjectNodeBindingSnapshot snapshot)
    {
        binding.Route = snapshot.Route;
        binding.ExternalArtifactKind = snapshot.ExternalArtifactKind;
        binding.ExternalArtifactId = snapshot.ExternalArtifactId;
        binding.MediaRelativePath = snapshot.MediaRelativePath;
        binding.MediaContentType = snapshot.MediaContentType;
        binding.MediaOriginalFileName = snapshot.MediaOriginalFileName;
        binding.StorageObjectReferenceJson = snapshot.StorageObjectReferenceJson;
    }

    private static void StripCarrierPayload(ProjectObjectRecord node, string sanitizedMetadataJson)
    {
        node.Route = string.Empty;
        node.ExternalArtifactKind = string.Empty;
        node.ExternalArtifactId = null;
        node.MediaRelativePath = string.Empty;
        node.MediaContentType = string.Empty;
        node.MediaOriginalFileName = string.Empty;
        node.StorageObjectReferenceJson = string.Empty;
        node.MetadataJson = sanitizedMetadataJson;
    }

    private static IReadOnlyList<ProjectNodeBindingReferencePayload> ExtractReferences(ProjectObjectMetadataEnvelope metadata)
    {
        var references = new List<ProjectNodeBindingReferencePayload>();
        if (metadata.Meeting is not null)
        {
            references.AddRange(metadata.Meeting.ParticipantIds.Select((participantId, index) =>
                new ProjectNodeBindingReferencePayload(ProjectNodeReferenceKind.MeetingParticipant, participantId, index)));
        }

        AddReference(references, ProjectNodeReferenceKind.RecordingMeetingNode, metadata.Recording?.MeetingNodeArtifactId);
        AddReference(references, ProjectNodeReferenceKind.RecordingTranscriptNode, metadata.Recording?.TranscriptNodeArtifactId);
        AddReference(references, ProjectNodeReferenceKind.TranscriptRecordingNode, metadata.Transcript?.RecordingNodeArtifactId);
        AddReference(references, ProjectNodeReferenceKind.TranscriptProviderProfile, metadata.Transcript?.LastProviderProfileId);
        AddReference(references, ProjectNodeReferenceKind.ParticipantParentParticipant, metadata.Participant?.ParentParticipantArtifactId);
        AddReference(references, ProjectNodeReferenceKind.WorkItemAssigneeParticipant, metadata.WorkItem?.AssigneeParticipantArtifactId);
        AddReference(references, ProjectNodeReferenceKind.WorkItemRepositoryResource, metadata.WorkItem?.RepositoryResourceId);
        AddReference(references, ProjectNodeReferenceKind.RepositoryResource, metadata.Repository?.ResourceId);
        AddReference(references, ProjectNodeReferenceKind.EnvironmentRepositoryResource, metadata.Environment?.RepositoryResourceId);
        AddReference(references, ProjectNodeReferenceKind.InfrastructureSecretReference, metadata.Infrastructure?.SecretReferenceArtifactId);
        AddReference(references, ProjectNodeReferenceKind.InfrastructureStorageCatalog, metadata.Infrastructure?.StorageCatalogId);
        return references;
    }

    private static void AddReference(
        ICollection<ProjectNodeBindingReferencePayload> references,
        ProjectNodeReferenceKind referenceKind,
        Guid? referenceId)
    {
        if (!referenceId.HasValue)
        {
            return;
        }

        references.Add(new ProjectNodeBindingReferencePayload(referenceKind, referenceId.Value, 0));
    }

    private static void ClearReferences(ProjectObjectMetadataEnvelope metadata)
    {
        if (metadata.Meeting is not null)
        {
            metadata.Meeting.ParticipantIds = [];
        }

        if (metadata.Recording is not null)
        {
            metadata.Recording.MeetingNodeArtifactId = null;
            metadata.Recording.TranscriptNodeArtifactId = null;
        }

        if (metadata.Transcript is not null)
        {
            metadata.Transcript.RecordingNodeArtifactId = null;
            metadata.Transcript.LastProviderProfileId = null;
        }

        if (metadata.Participant is not null)
        {
            metadata.Participant.ParentParticipantArtifactId = null;
        }

        if (metadata.WorkItem is not null)
        {
            metadata.WorkItem.AssigneeParticipantArtifactId = null;
            metadata.WorkItem.RepositoryResourceId = null;
        }

        if (metadata.Repository is not null)
        {
            metadata.Repository.ResourceId = null;
        }

        if (metadata.Environment is not null)
        {
            metadata.Environment.RepositoryResourceId = null;
        }

        if (metadata.Infrastructure is not null)
        {
            metadata.Infrastructure.SecretReferenceArtifactId = null;
            metadata.Infrastructure.StorageCatalogId = null;
        }
    }

    private static void ApplyReferences(
        ProjectObjectMetadataEnvelope metadata,
        IReadOnlyList<ProjectNodeBindingReferencePayload> references)
    {
        if (references.Count == 0)
        {
            return;
        }

        foreach (var group in references.GroupBy(item => item.ReferenceKind))
        {
            switch (group.Key)
            {
                case ProjectNodeReferenceKind.MeetingParticipant:
                    metadata.Meeting ??= new ProjectMeetingMetadata();
                    metadata.Meeting.ParticipantIds = group
                        .OrderBy(item => item.OrderIndex)
                        .Select(item => item.ReferenceId)
                        .ToList();
                    break;
                case ProjectNodeReferenceKind.RecordingMeetingNode:
                    metadata.Recording ??= new ProjectRecordingMetadata();
                    metadata.Recording.MeetingNodeArtifactId = group.First().ReferenceId;
                    break;
                case ProjectNodeReferenceKind.RecordingTranscriptNode:
                    metadata.Recording ??= new ProjectRecordingMetadata();
                    metadata.Recording.TranscriptNodeArtifactId = group.First().ReferenceId;
                    break;
                case ProjectNodeReferenceKind.TranscriptRecordingNode:
                    metadata.Transcript ??= new ProjectTranscriptMetadata();
                    metadata.Transcript.RecordingNodeArtifactId = group.First().ReferenceId;
                    break;
                case ProjectNodeReferenceKind.TranscriptProviderProfile:
                    metadata.Transcript ??= new ProjectTranscriptMetadata();
                    metadata.Transcript.LastProviderProfileId = group.First().ReferenceId;
                    break;
                case ProjectNodeReferenceKind.ParticipantParentParticipant:
                    metadata.Participant ??= new ProjectParticipantMetadata();
                    metadata.Participant.ParentParticipantArtifactId = group.First().ReferenceId;
                    break;
                case ProjectNodeReferenceKind.WorkItemAssigneeParticipant:
                    metadata.WorkItem ??= new ProjectWorkItemMetadata();
                    metadata.WorkItem.AssigneeParticipantArtifactId = group.First().ReferenceId;
                    break;
                case ProjectNodeReferenceKind.WorkItemRepositoryResource:
                    metadata.WorkItem ??= new ProjectWorkItemMetadata();
                    metadata.WorkItem.RepositoryResourceId = group.First().ReferenceId;
                    break;
                case ProjectNodeReferenceKind.RepositoryResource:
                    metadata.Repository ??= new ProjectRepositoryMetadata();
                    metadata.Repository.ResourceId = group.First().ReferenceId;
                    break;
                case ProjectNodeReferenceKind.EnvironmentRepositoryResource:
                    metadata.Environment ??= new ProjectEnvironmentMetadata();
                    metadata.Environment.RepositoryResourceId = group.First().ReferenceId;
                    break;
                case ProjectNodeReferenceKind.InfrastructureSecretReference:
                    metadata.Infrastructure ??= new ProjectInfrastructureMetadata();
                    metadata.Infrastructure.SecretReferenceArtifactId = group.First().ReferenceId;
                    break;
                case ProjectNodeReferenceKind.InfrastructureStorageCatalog:
                    metadata.Infrastructure ??= new ProjectInfrastructureMetadata();
                    metadata.Infrastructure.StorageCatalogId = group.First().ReferenceId;
                    break;
            }
        }
    }
}
