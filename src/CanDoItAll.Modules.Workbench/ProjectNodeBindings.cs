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

internal sealed record ProjectNodeBindingState(
    string Route,
    string ExternalArtifactKind,
    Guid? ExternalArtifactId,
    string MediaRelativePath,
    string MediaContentType,
    string MediaOriginalFileName,
    string StorageObjectReferenceJson)
{
    public static ProjectNodeBindingState Empty { get; } = new(
        string.Empty,
        string.Empty,
        null,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty);
}

public sealed class ProjectNodeReferenceSet
{
    public static ProjectNodeReferenceSet Empty { get; } = new();

    public IReadOnlyList<Guid> MeetingParticipantIds { get; set; } = [];

    public Guid? RecordingMeetingNodeId { get; set; }

    public Guid? RecordingTranscriptNodeId { get; set; }

    public Guid? TranscriptRecordingNodeId { get; set; }

    public Guid? TranscriptProviderProfileId { get; set; }

    public Guid? ParticipantParentNodeId { get; set; }

    public Guid? WorkItemAssigneeNodeId { get; set; }

    public Guid? WorkItemRepositoryResourceId { get; set; }

    public Guid? RepositoryResourceId { get; set; }

    public Guid? EnvironmentRepositoryResourceId { get; set; }

    public Guid? InfrastructureSecretReferenceId { get; set; }

    public Guid? InfrastructureStorageCatalogId { get; set; }

    public bool IsEmpty =>
        MeetingParticipantIds.Count == 0 &&
        !RecordingMeetingNodeId.HasValue &&
        !RecordingTranscriptNodeId.HasValue &&
        !TranscriptRecordingNodeId.HasValue &&
        !TranscriptProviderProfileId.HasValue &&
        !ParticipantParentNodeId.HasValue &&
        !WorkItemAssigneeNodeId.HasValue &&
        !WorkItemRepositoryResourceId.HasValue &&
        !RepositoryResourceId.HasValue &&
        !EnvironmentRepositoryResourceId.HasValue &&
        !InfrastructureSecretReferenceId.HasValue &&
        !InfrastructureStorageCatalogId.HasValue;
}

internal sealed record ProjectNodeBindingPersistencePlan(
    string SanitizedMetadataJson,
    ProjectNodeBindingState Binding,
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

            node.Binding = binding is null
                ? node.Binding
                : new ProjectNodeBindingState(
                    binding.Route,
                    binding.ExternalArtifactKind,
                    binding.ExternalArtifactId,
                    binding.MediaRelativePath,
                    binding.MediaContentType,
                    binding.MediaOriginalFileName,
                    binding.StorageObjectReferenceJson);
            node.NodeReferences = BuildReferenceSet(
                references
                    .Select(item => new ProjectNodeBindingReferencePayload(item.ReferenceKind, item.ReferenceId, item.OrderIndex))
                    .ToList());
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
            binding is null ? null : new ProjectNodeBindingState(
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
        ProjectNodeBindingState? binding,
        IReadOnlyList<ProjectNodeBindingReferencePayload> references)
    {
        node.Binding = binding ?? ResolveBinding(node);
        node.Route = node.Binding.Route;
        node.ExternalArtifactKind = node.Binding.ExternalArtifactKind;
        node.ExternalArtifactId = node.Binding.ExternalArtifactId;
        node.MediaRelativePath = node.Binding.MediaRelativePath;
        node.MediaContentType = node.Binding.MediaContentType;
        node.MediaOriginalFileName = node.Binding.MediaOriginalFileName;
        node.StorageObjectReferenceJson = node.Binding.StorageObjectReferenceJson;
        node.NodeReferences = BuildReferenceSet(references);
    }

    public static bool HasForeignReferencePayload(ProjectNodeReferenceSet references)
    {
        ArgumentNullException.ThrowIfNull(references);
        return !references.IsEmpty;
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

        var legacyReferences = ProjectNodeLegacyMetadata.ReadLegacyReferences(node.MetadataJson);
        if (references.Count > 0)
        {
            return HasForeignReferencePayload(legacyReferences);
        }

        return HasLegacyCarrierPayload(node) || HasForeignReferencePayload(legacyReferences);
    }

    private static ProjectNodeBindingPersistencePlan CreatePersistencePlan(ProjectObjectRecord node)
    {
        var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
        ValidateSanitizedMetadata(node.ObjectType, node.ObjectSubtype, metadata);
        var references = ExtractReferences(ResolveReferenceSet(node));
        return new ProjectNodeBindingPersistencePlan(
            ProjectObjectMetadataSerializer.Serialize(metadata),
            ResolveBinding(node),
            references);
    }

    private static void ValidateSanitizedMetadata(
        ProjectObjectType objectType,
        string objectSubtype,
        ProjectObjectMetadataEnvelope metadata)
    {
        ProjectObjectMetadataSerializer.Validate(objectType, objectSubtype, metadata);
    }

    private static ProjectNodeReferenceSet ResolveReferenceSet(ProjectObjectRecord node)
    {
        return node.NodeReferences.IsEmpty
            ? ProjectNodeLegacyMetadata.ReadLegacyReferences(node.MetadataJson)
            : CloneReferenceSet(node.NodeReferences);
    }

    private static ProjectNodeBindingState ResolveBinding(ProjectObjectRecord node)
    {
        var binding = node.Binding ?? ProjectNodeBindingState.Empty;
        return new ProjectNodeBindingState(
            ResolveRoute(binding.Route, node.Route, node.ProjectId),
            ResolveArtifactKind(binding.ExternalArtifactKind, node.ExternalArtifactKind, node.ObjectType),
            binding.ExternalArtifactId ?? node.ExternalArtifactId,
            ResolveText(binding.MediaRelativePath, node.MediaRelativePath),
            ResolveText(binding.MediaContentType, node.MediaContentType),
            ResolveText(binding.MediaOriginalFileName, node.MediaOriginalFileName),
            ResolveText(binding.StorageObjectReferenceJson, node.StorageObjectReferenceJson));
    }

    private static string ResolveRoute(string? route, string? legacyRoute, Guid projectId)
    {
        var effectiveRoute = ResolveText(route, legacyRoute);
        return string.IsNullOrWhiteSpace(effectiveRoute)
            ? $"/projects/{projectId}/structure"
            : effectiveRoute;
    }

    private static string ResolveArtifactKind(string? artifactKind, string? legacyArtifactKind, ProjectObjectType objectType)
    {
        var effectiveArtifactKind = ResolveText(artifactKind, legacyArtifactKind);
        return string.IsNullOrWhiteSpace(effectiveArtifactKind)
            ? objectType.ToString()
            : effectiveArtifactKind;
    }

    private static string ResolveText(string? primaryValue, string? fallbackValue)
    {
        return !string.IsNullOrWhiteSpace(primaryValue)
            ? primaryValue.Trim()
            : fallbackValue?.Trim() ?? string.Empty;
    }

    private static bool HasLegacyCarrierPayload(ProjectObjectRecord node)
    {
        return !string.IsNullOrWhiteSpace(node.Route) ||
               !string.IsNullOrWhiteSpace(node.ExternalArtifactKind) ||
               node.ExternalArtifactId.HasValue ||
               !string.IsNullOrWhiteSpace(node.MediaRelativePath) ||
               !string.IsNullOrWhiteSpace(node.MediaContentType) ||
               !string.IsNullOrWhiteSpace(node.MediaOriginalFileName) ||
               !string.IsNullOrWhiteSpace(node.StorageObjectReferenceJson);
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

    private static void ApplyBinding(ProjectNodeBindingRecord binding, ProjectNodeBindingState snapshot)
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

    private static IReadOnlyList<ProjectNodeBindingReferencePayload> ExtractReferences(ProjectNodeReferenceSet referenceSet)
    {
        var references = new List<ProjectNodeBindingReferencePayload>();
        if (referenceSet.MeetingParticipantIds.Count > 0)
        {
            references.AddRange(referenceSet.MeetingParticipantIds.Select((participantId, index) =>
                new ProjectNodeBindingReferencePayload(ProjectNodeReferenceKind.MeetingParticipant, participantId, index)));
        }

        AddReference(references, ProjectNodeReferenceKind.RecordingMeetingNode, referenceSet.RecordingMeetingNodeId);
        AddReference(references, ProjectNodeReferenceKind.RecordingTranscriptNode, referenceSet.RecordingTranscriptNodeId);
        AddReference(references, ProjectNodeReferenceKind.TranscriptRecordingNode, referenceSet.TranscriptRecordingNodeId);
        AddReference(references, ProjectNodeReferenceKind.TranscriptProviderProfile, referenceSet.TranscriptProviderProfileId);
        AddReference(references, ProjectNodeReferenceKind.ParticipantParentParticipant, referenceSet.ParticipantParentNodeId);
        AddReference(references, ProjectNodeReferenceKind.WorkItemAssigneeParticipant, referenceSet.WorkItemAssigneeNodeId);
        AddReference(references, ProjectNodeReferenceKind.WorkItemRepositoryResource, referenceSet.WorkItemRepositoryResourceId);
        AddReference(references, ProjectNodeReferenceKind.RepositoryResource, referenceSet.RepositoryResourceId);
        AddReference(references, ProjectNodeReferenceKind.EnvironmentRepositoryResource, referenceSet.EnvironmentRepositoryResourceId);
        AddReference(references, ProjectNodeReferenceKind.InfrastructureSecretReference, referenceSet.InfrastructureSecretReferenceId);
        AddReference(references, ProjectNodeReferenceKind.InfrastructureStorageCatalog, referenceSet.InfrastructureStorageCatalogId);
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

    private static ProjectNodeReferenceSet BuildReferenceSet(
        IReadOnlyList<ProjectNodeBindingReferencePayload> references)
    {
        if (references.Count == 0)
        {
            return new ProjectNodeReferenceSet();
        }

        var referenceSet = new ProjectNodeReferenceSet();
        foreach (var group in references.GroupBy(item => item.ReferenceKind))
        {
            switch (group.Key)
            {
                case ProjectNodeReferenceKind.MeetingParticipant:
                    referenceSet.MeetingParticipantIds = group
                        .OrderBy(item => item.OrderIndex)
                        .Select(item => item.ReferenceId)
                        .ToList();
                    break;
                case ProjectNodeReferenceKind.RecordingMeetingNode:
                    referenceSet.RecordingMeetingNodeId = group.First().ReferenceId;
                    break;
                case ProjectNodeReferenceKind.RecordingTranscriptNode:
                    referenceSet.RecordingTranscriptNodeId = group.First().ReferenceId;
                    break;
                case ProjectNodeReferenceKind.TranscriptRecordingNode:
                    referenceSet.TranscriptRecordingNodeId = group.First().ReferenceId;
                    break;
                case ProjectNodeReferenceKind.TranscriptProviderProfile:
                    referenceSet.TranscriptProviderProfileId = group.First().ReferenceId;
                    break;
                case ProjectNodeReferenceKind.ParticipantParentParticipant:
                    referenceSet.ParticipantParentNodeId = group.First().ReferenceId;
                    break;
                case ProjectNodeReferenceKind.WorkItemAssigneeParticipant:
                    referenceSet.WorkItemAssigneeNodeId = group.First().ReferenceId;
                    break;
                case ProjectNodeReferenceKind.WorkItemRepositoryResource:
                    referenceSet.WorkItemRepositoryResourceId = group.First().ReferenceId;
                    break;
                case ProjectNodeReferenceKind.RepositoryResource:
                    referenceSet.RepositoryResourceId = group.First().ReferenceId;
                    break;
                case ProjectNodeReferenceKind.EnvironmentRepositoryResource:
                    referenceSet.EnvironmentRepositoryResourceId = group.First().ReferenceId;
                    break;
                case ProjectNodeReferenceKind.InfrastructureSecretReference:
                    referenceSet.InfrastructureSecretReferenceId = group.First().ReferenceId;
                    break;
                case ProjectNodeReferenceKind.InfrastructureStorageCatalog:
                    referenceSet.InfrastructureStorageCatalogId = group.First().ReferenceId;
                    break;
            }
        }

        return referenceSet;
    }

    private static ProjectNodeReferenceSet CloneReferenceSet(ProjectNodeReferenceSet referenceSet)
    {
        return new ProjectNodeReferenceSet
        {
            MeetingParticipantIds = referenceSet.MeetingParticipantIds.ToList(),
            RecordingMeetingNodeId = referenceSet.RecordingMeetingNodeId,
            RecordingTranscriptNodeId = referenceSet.RecordingTranscriptNodeId,
            TranscriptRecordingNodeId = referenceSet.TranscriptRecordingNodeId,
            TranscriptProviderProfileId = referenceSet.TranscriptProviderProfileId,
            ParticipantParentNodeId = referenceSet.ParticipantParentNodeId,
            WorkItemAssigneeNodeId = referenceSet.WorkItemAssigneeNodeId,
            WorkItemRepositoryResourceId = referenceSet.WorkItemRepositoryResourceId,
            RepositoryResourceId = referenceSet.RepositoryResourceId,
            EnvironmentRepositoryResourceId = referenceSet.EnvironmentRepositoryResourceId,
            InfrastructureSecretReferenceId = referenceSet.InfrastructureSecretReferenceId,
            InfrastructureStorageCatalogId = referenceSet.InfrastructureStorageCatalogId
        };
    }
}
