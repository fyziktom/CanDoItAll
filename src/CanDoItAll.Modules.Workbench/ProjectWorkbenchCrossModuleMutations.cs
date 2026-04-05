using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal enum ProjectCrossModuleMutationKind
{
    DeleteSubtree = 1,
    MoveDescendants = 2
}

internal enum ProjectCrossModuleMutationStatus
{
    Pending = 0,
    WorkbenchCommitted = 1,
    Completed = 2,
    Compensated = 3,
    Failed = 4
}

internal sealed class ProjectCrossModuleMutationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public string ScopeNodeKey { get; set; } = string.Empty;

    public ProjectCrossModuleMutationKind MutationKind { get; set; }

    public ProjectCrossModuleMutationStatus Status { get; set; } = ProjectCrossModuleMutationStatus.Pending;

    public string PayloadJson { get; set; } = "{}";

    public string ErrorMessage { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class ProjectCrossModuleMutationRecordConfiguration : IEntityTypeConfiguration<ProjectCrossModuleMutationRecord>
{
    public void Configure(EntityTypeBuilder<ProjectCrossModuleMutationRecord> builder)
    {
        builder.ToTable("Workbench_ProjectCrossModuleMutations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ScopeNodeKey).HasMaxLength(160).IsRequired();
        builder.Property(item => item.PayloadJson).HasColumnType("TEXT");
        builder.Property(item => item.ErrorMessage).HasColumnType("TEXT");
        builder.HasIndex(item => new { item.ProjectId, item.ScopeNodeKey, item.CreatedAtUtc });
        builder.HasIndex(item => new { item.ProjectId, item.Status, item.UpdatedAtUtc });
    }
}

public sealed class ProjectCrossModuleMutationCoordinator(IClock clock)
{
    internal ProjectCrossModuleMutationRecord Begin(
        Guid projectId,
        string scopeNodeKey,
        ProjectCrossModuleMutationKind mutationKind,
        string payloadJson)
    {
        var timestamp = clock.GetUtcNow();
        return new ProjectCrossModuleMutationRecord
        {
            ProjectId = projectId,
            ScopeNodeKey = scopeNodeKey.Trim(),
            MutationKind = mutationKind,
            Status = ProjectCrossModuleMutationStatus.Pending,
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp
        };
    }

    internal void MarkWorkbenchCommitted(ProjectCrossModuleMutationRecord record)
    {
        Update(record, ProjectCrossModuleMutationStatus.WorkbenchCommitted, null);
    }

    internal void MarkCompleted(ProjectCrossModuleMutationRecord record)
    {
        Update(record, ProjectCrossModuleMutationStatus.Completed, null);
    }

    internal void MarkCompensated(ProjectCrossModuleMutationRecord record, string errorMessage)
    {
        Update(record, ProjectCrossModuleMutationStatus.Compensated, errorMessage);
    }

    internal void MarkFailed(ProjectCrossModuleMutationRecord record, string errorMessage)
    {
        Update(record, ProjectCrossModuleMutationStatus.Failed, errorMessage);
    }

    private void Update(
        ProjectCrossModuleMutationRecord record,
        ProjectCrossModuleMutationStatus status,
        string? errorMessage)
    {
        record.Status = status;
        record.ErrorMessage = errorMessage?.Trim() ?? string.Empty;
        record.UpdatedAtUtc = clock.GetUtcNow();
    }
}
