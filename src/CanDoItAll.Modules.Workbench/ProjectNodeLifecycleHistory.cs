using System.Text.Json;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Workbench;

internal enum ProjectNodeLifecycleTransitionMode
{
    NotePromotion,
    SubtypeChange
}

internal sealed class ProjectNodeLifecycleEventRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid ProjectObjectId { get; set; }
    public string NodeKey { get; set; } = string.Empty;
    public ProjectNodeLifecycleTransitionMode TransitionMode { get; set; }
    public ProjectNodeKindFamily SourceFamily { get; set; }
    public ProjectNodeKindFamily TargetFamily { get; set; }
    public ProjectObjectType SourceObjectType { get; set; }
    public string SourceObjectSubtype { get; set; } = string.Empty;
    public ProjectObjectType TargetObjectType { get; set; }
    public string TargetObjectSubtype { get; set; } = string.Empty;
    public string SourceSnapshotJson { get; set; } = "{}";
    public string TargetSnapshotJson { get; set; } = "{}";
    public DateTimeOffset OccurredAtUtc { get; set; }
}

internal sealed class ProjectNodeLifecycleEventRecordConfiguration : IEntityTypeConfiguration<ProjectNodeLifecycleEventRecord>
{
    public void Configure(EntityTypeBuilder<ProjectNodeLifecycleEventRecord> builder)
    {
        builder.ToTable("Workbench_ProjectNodeLifecycleEvents");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.NodeKey).HasMaxLength(160).IsRequired();
        builder.Property(item => item.SourceObjectSubtype).HasMaxLength(120);
        builder.Property(item => item.TargetObjectSubtype).HasMaxLength(120);
        builder.Property(item => item.SourceSnapshotJson).HasColumnType("TEXT");
        builder.Property(item => item.TargetSnapshotJson).HasColumnType("TEXT");
        builder.HasIndex(item => new { item.ProjectId, item.NodeKey, item.OccurredAtUtc });
        builder.HasOne<ProjectObjectRecord>()
            .WithMany()
            .HasForeignKey(item => item.ProjectObjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed record ProjectNodeLifecycleSnapshot(
    string Title,
    string Subtitle,
    string Notes,
    string Status,
    string Route,
    string ArtifactKind,
    Guid? ArtifactId,
    string MediaRelativePath,
    string MediaContentType,
    string MediaOriginalFileName,
    string StorageObjectReferenceJson,
    string MetadataJson,
    string ProgressMode,
    int ProgressPercent,
    string MarkerIcon,
    string MarkerTone,
    string MarkerLabel,
    int Priority,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    int? DurationSeconds);

internal static class ProjectNodeLifecycleHistory
{
    public static ProjectNodeLifecycleEventRecord CaptureReclassification(
        Guid projectId,
        ProjectObjectRecord sourceNode,
        ProjectNodeKindDescriptor sourceDescriptor,
        ProjectObjectRecord targetNode,
        ProjectNodeKindDescriptor targetDescriptor,
        DateTimeOffset occurredAtUtc)
        => new()
        {
            ProjectId = projectId,
            ProjectObjectId = targetNode.Id,
            NodeKey = targetNode.NodeKey,
            TransitionMode = sourceNode.ObjectType == targetNode.ObjectType
                ? ProjectNodeLifecycleTransitionMode.SubtypeChange
                : ProjectNodeLifecycleTransitionMode.NotePromotion,
            SourceFamily = sourceDescriptor.Family,
            TargetFamily = targetDescriptor.Family,
            SourceObjectType = sourceNode.ObjectType,
            SourceObjectSubtype = sourceNode.ObjectSubtype,
            TargetObjectType = targetNode.ObjectType,
            TargetObjectSubtype = targetNode.ObjectSubtype,
            SourceSnapshotJson = SerializeSnapshot(sourceNode),
            TargetSnapshotJson = SerializeSnapshot(targetNode),
            OccurredAtUtc = occurredAtUtc
        };

    private static string SerializeSnapshot(ProjectObjectRecord node)
        => JsonSerializer.Serialize(new ProjectNodeLifecycleSnapshot(
            node.Title,
            node.Subtitle,
            node.Notes,
            node.Status,
            node.Route,
            node.ExternalArtifactKind,
            node.ExternalArtifactId,
            node.MediaRelativePath,
            node.MediaContentType,
            node.MediaOriginalFileName,
            node.StorageObjectReferenceJson,
            string.IsNullOrWhiteSpace(node.MetadataJson) ? "{}" : node.MetadataJson,
            node.ProgressMode,
            node.ProgressPercent,
            node.MarkerIcon,
            node.MarkerTone,
            node.MarkerLabel,
            node.Priority,
            node.StartUtc,
            node.EndUtc,
            node.DurationSeconds));
}
