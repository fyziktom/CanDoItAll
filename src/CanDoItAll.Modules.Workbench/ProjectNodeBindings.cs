using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectNodeReferenceKinds
{
    public const string MeetingParticipant = "meeting.participant";
    public const string RecordingMeetingNode = "recording.meeting-node";
    public const string RecordingTranscriptNode = "recording.transcript-node";
    public const string TranscriptRecordingNode = "transcript.recording-node";
    public const string TranscriptProviderProfile = "transcript.provider-profile";
    public const string ParticipantParentParticipant = "participant.parent-participant";
    public const string WorkItemAssigneeParticipant = "work-item.assignee-participant";
    public const string WorkItemRepositoryResource = "work-item.repository-resource";
    public const string RepositoryResource = "repository.resource";
    public const string EnvironmentRepositoryResource = "environment.repository-resource";
    public const string InfrastructureSecretReference = "infrastructure.secret-reference";
    public const string InfrastructureStorageCatalog = "infrastructure.storage-catalog";
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
    public string ReferenceKind { get; set; } = string.Empty;
    public string ReferenceId { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class ProjectNodeReferenceRecordConfiguration : IEntityTypeConfiguration<ProjectNodeReferenceRecord>
{
    public void Configure(EntityTypeBuilder<ProjectNodeReferenceRecord> builder)
    {
        builder.ToTable("Workbench_ProjectNodeReferences");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ReferenceKind).HasMaxLength(160).IsRequired();
        builder.Property(item => item.ReferenceId).HasMaxLength(200).IsRequired();
        builder.HasIndex(item => new { item.ProjectObjectId, item.ReferenceKind, item.ReferenceId }).IsUnique();
        builder.HasIndex(item => new { item.ProjectObjectId, item.ReferenceKind, item.OrderIndex });
        builder.HasOne<ProjectObjectRecord>()
            .WithMany()
            .HasForeignKey(item => item.ProjectObjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed record ProjectNodeReferenceEntry(string ReferenceKind, string ReferenceId, int OrderIndex);

internal sealed record ProjectNodeBindingReferencePayload(
    string ReferenceKind,
    string ReferenceId,
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

public sealed class ProjectNodeReferenceCollection
{
    public static ProjectNodeReferenceCollection Empty { get; } = new();

    public IReadOnlyList<ProjectNodeReferenceEntry> Entries { get; set; } = [];

    public IReadOnlyList<Guid> MeetingParticipantIds
    {
        get => GetGuidValues(ProjectNodeReferenceKinds.MeetingParticipant);
        set => SetGuidValues(ProjectNodeReferenceKinds.MeetingParticipant, value);
    }

    public Guid? RecordingMeetingNodeId
    {
        get => GetGuidValue(ProjectNodeReferenceKinds.RecordingMeetingNode);
        set => SetGuidValue(ProjectNodeReferenceKinds.RecordingMeetingNode, value);
    }

    public Guid? RecordingTranscriptNodeId
    {
        get => GetGuidValue(ProjectNodeReferenceKinds.RecordingTranscriptNode);
        set => SetGuidValue(ProjectNodeReferenceKinds.RecordingTranscriptNode, value);
    }

    public Guid? TranscriptRecordingNodeId
    {
        get => GetGuidValue(ProjectNodeReferenceKinds.TranscriptRecordingNode);
        set => SetGuidValue(ProjectNodeReferenceKinds.TranscriptRecordingNode, value);
    }

    public Guid? TranscriptProviderProfileId
    {
        get => GetGuidValue(ProjectNodeReferenceKinds.TranscriptProviderProfile);
        set => SetGuidValue(ProjectNodeReferenceKinds.TranscriptProviderProfile, value);
    }

    public Guid? ParticipantParentNodeId
    {
        get => GetGuidValue(ProjectNodeReferenceKinds.ParticipantParentParticipant);
        set => SetGuidValue(ProjectNodeReferenceKinds.ParticipantParentParticipant, value);
    }

    public Guid? WorkItemAssigneeNodeId
    {
        get => GetGuidValue(ProjectNodeReferenceKinds.WorkItemAssigneeParticipant);
        set => SetGuidValue(ProjectNodeReferenceKinds.WorkItemAssigneeParticipant, value);
    }

    public Guid? WorkItemRepositoryResourceId
    {
        get => GetGuidValue(ProjectNodeReferenceKinds.WorkItemRepositoryResource);
        set => SetGuidValue(ProjectNodeReferenceKinds.WorkItemRepositoryResource, value);
    }

    public Guid? RepositoryResourceId
    {
        get => GetGuidValue(ProjectNodeReferenceKinds.RepositoryResource);
        set => SetGuidValue(ProjectNodeReferenceKinds.RepositoryResource, value);
    }

    public Guid? EnvironmentRepositoryResourceId
    {
        get => GetGuidValue(ProjectNodeReferenceKinds.EnvironmentRepositoryResource);
        set => SetGuidValue(ProjectNodeReferenceKinds.EnvironmentRepositoryResource, value);
    }

    public Guid? InfrastructureSecretReferenceId
    {
        get => GetGuidValue(ProjectNodeReferenceKinds.InfrastructureSecretReference);
        set => SetGuidValue(ProjectNodeReferenceKinds.InfrastructureSecretReference, value);
    }

    public Guid? InfrastructureStorageCatalogId
    {
        get => GetGuidValue(ProjectNodeReferenceKinds.InfrastructureStorageCatalog);
        set => SetGuidValue(ProjectNodeReferenceKinds.InfrastructureStorageCatalog, value);
    }

    public bool IsEmpty => Entries.Count == 0;

    public ProjectNodeReferenceCollection Clone()
    {
        return new ProjectNodeReferenceCollection
        {
            Entries = Entries
                .Select(entry => entry with { })
                .ToList()
        };
    }

    private IReadOnlyList<Guid> GetGuidValues(string referenceKind)
    {
        return Entries
            .Where(entry => string.Equals(entry.ReferenceKind, referenceKind, StringComparison.Ordinal))
            .OrderBy(entry => entry.OrderIndex)
            .Select(entry => TryParseGuid(entry.ReferenceId))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();
    }

    private Guid? GetGuidValue(string referenceKind)
    {
        return Entries
            .Where(entry => string.Equals(entry.ReferenceKind, referenceKind, StringComparison.Ordinal))
            .OrderBy(entry => entry.OrderIndex)
            .Select(entry => TryParseGuid(entry.ReferenceId))
            .FirstOrDefault(value => value.HasValue);
    }

    private void SetGuidValues(string referenceKind, IEnumerable<Guid> values)
    {
        ReplaceEntries(
            referenceKind,
            values.Select(value => value.ToString("D")));
    }

    private void SetGuidValue(string referenceKind, Guid? value)
    {
        ReplaceEntries(
            referenceKind,
            value.HasValue ? [value.Value.ToString("D")] : []);
    }

    private void ReplaceEntries(string referenceKind, IEnumerable<string> referenceIds)
    {
        var replacements = referenceIds
            .Where(referenceId => !string.IsNullOrWhiteSpace(referenceId))
            .Select((referenceId, index) => new ProjectNodeReferenceEntry(referenceKind, referenceId.Trim(), index))
            .ToList();
        var retainedEntries = Entries
            .Where(entry => !string.Equals(entry.ReferenceKind, referenceKind, StringComparison.Ordinal))
            .ToList();
        Entries = retainedEntries
            .Concat(replacements)
            .OrderBy(entry => entry.ReferenceKind, StringComparer.Ordinal)
            .ThenBy(entry => entry.OrderIndex)
            .ToList();
    }

    private static Guid? TryParseGuid(string? value)
    {
        return Guid.TryParse(value, out var parsed)
            ? parsed
            : null;
    }
}

internal sealed record ProjectNodeBindingPersistencePlan(
    string SanitizedMetadataJson,
    ProjectNodeBindingState Binding,
    IReadOnlyList<ProjectNodeBindingReferencePayload> References);

internal static class ProjectNodeBindingStorage
{
    public static async Task LoadAsync(
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

        node.MetadataJson = plan.SanitizedMetadataJson;
        return plan;
    }

    public static void Apply(ProjectObjectRecord node, ProjectNodeBindingPersistencePlan plan)
    {
        Apply(node, plan.Binding, plan.References);
        node.MetadataJson = plan.SanitizedMetadataJson;
    }

    public static void Apply(
        ProjectObjectRecord node,
        ProjectNodeBindingRecord? binding,
        IReadOnlyList<ProjectNodeReferenceRecord> references)
    {
        Apply(
            node,
            binding is null
                ? null
                : new ProjectNodeBindingState(
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
        node.NodeReferences = references.Count == 0
            ? ResolveReferenceCollection(node)
            : BuildReferenceCollection(references);
    }

    public static bool HasForeignReferencePayload(ProjectNodeReferenceCollection references)
    {
        ArgumentNullException.ThrowIfNull(references);
        return !references.IsEmpty;
    }

    public static ProjectNodeBindingState ResolveForRuntime(ProjectObjectRecord node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return ResolveBinding(node);
    }

    private static ProjectNodeBindingPersistencePlan CreatePersistencePlan(ProjectObjectRecord node)
    {
        var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
        ValidateSanitizedMetadata(node.ObjectType, node.ObjectSubtype, metadata);
        var references = ExtractReferences(ResolveReferenceCollection(node));
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

    private static ProjectNodeReferenceCollection ResolveReferenceCollection(ProjectObjectRecord node)
    {
        return node.NodeReferences.IsEmpty
            ? ProjectNodeLegacyMetadata.ReadLegacyReferences(node.MetadataJson)
            : node.NodeReferences.Clone();
    }

    private static ProjectNodeBindingState ResolveBinding(ProjectObjectRecord node)
    {
        var binding = node.Binding;
        var route = string.IsNullOrWhiteSpace(binding.Route)
            ? $"/projects/{node.ProjectId}/structure"
            : binding.Route.Trim();
        var externalArtifactKind = string.IsNullOrWhiteSpace(binding.ExternalArtifactKind)
            ? node.ObjectType.ToString()
            : binding.ExternalArtifactKind.Trim();

        return new ProjectNodeBindingState(
            route,
            externalArtifactKind,
            binding.ExternalArtifactId,
            binding.MediaRelativePath?.Trim() ?? string.Empty,
            binding.MediaContentType?.Trim() ?? string.Empty,
            binding.MediaOriginalFileName?.Trim() ?? string.Empty,
            binding.StorageObjectReferenceJson?.Trim() ?? string.Empty);
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

    private static IReadOnlyList<ProjectNodeBindingReferencePayload> ExtractReferences(ProjectNodeReferenceCollection references)
    {
        return references.Entries
            .Where(entry =>
                !string.IsNullOrWhiteSpace(entry.ReferenceKind) &&
                !string.IsNullOrWhiteSpace(entry.ReferenceId))
            .OrderBy(entry => entry.ReferenceKind, StringComparer.Ordinal)
            .ThenBy(entry => entry.OrderIndex)
            .Select(entry => new ProjectNodeBindingReferencePayload(
                entry.ReferenceKind.Trim(),
                entry.ReferenceId.Trim(),
                entry.OrderIndex))
            .ToList();
    }

    private static ProjectNodeReferenceCollection BuildReferenceCollection(
        IReadOnlyList<ProjectNodeBindingReferencePayload> references)
    {
        return references.Count == 0
            ? new ProjectNodeReferenceCollection()
            : new ProjectNodeReferenceCollection
            {
                Entries = references
                    .Where(reference =>
                        !string.IsNullOrWhiteSpace(reference.ReferenceKind) &&
                        !string.IsNullOrWhiteSpace(reference.ReferenceId))
                    .OrderBy(reference => reference.ReferenceKind, StringComparer.Ordinal)
                    .ThenBy(reference => reference.OrderIndex)
                    .Select(reference => new ProjectNodeReferenceEntry(
                        reference.ReferenceKind.Trim(),
                        reference.ReferenceId.Trim(),
                        reference.OrderIndex))
                    .ToList()
            };
    }
}
