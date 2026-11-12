using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal enum ProjectCrossModuleMutationKind
{
    DeleteSubtree = 1,
    MoveDescendants = 2,
    MoveSelectedNodes = 3,
    DeleteProject = 4
}

internal enum ProjectCrossModuleMutationStatus
{
    Pending = 0,
    WorkbenchCommitted = 1,
    Completed = 2,
    Processing = 3,
    Failed = 4
}

internal enum ProjectCrossModuleMutationApprovalState
{
    NotRequired = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

internal sealed class ProjectCrossModuleMutationRecord
{
    internal static string PendingDeletionFilter => FormattableString.Invariant(
        $"\"MutationKind\" = {(int)ProjectCrossModuleMutationKind.DeleteProject} AND \"Status\" <> {(int)ProjectCrossModuleMutationStatus.Completed}");

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public Guid? CompletionNamespaceId { get; set; }

    public Guid? CompletionId { get; set; }

    public string ScopeNodeKey { get; set; } = string.Empty;

    public ProjectCrossModuleMutationKind MutationKind { get; set; }

    public ProjectCrossModuleMutationStatus Status { get; set; } = ProjectCrossModuleMutationStatus.Pending;

    public ProjectCrossModuleMutationApprovalState ApprovalState { get; set; } = ProjectCrossModuleMutationApprovalState.NotRequired;

    public string PayloadJson { get; set; } = "{}";

    public string ErrorMessage { get; set; } = string.Empty;

    public int AttemptCount { get; set; }

    public DateTimeOffset? LastAttemptAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class ProjectCrossModuleMutationRecordConfiguration : IEntityTypeConfiguration<ProjectCrossModuleMutationRecord>
{
    public void Configure(EntityTypeBuilder<ProjectCrossModuleMutationRecord> builder)
    {
        builder.ToTable("Workbench_ProjectCrossModuleMutations", table =>
            table.HasCheckConstraint("CK_Workbench_ProjectMutation_CompletionBinding",
                "(\"CompletionNamespaceId\" IS NULL AND \"CompletionId\" IS NULL) OR " +
                "(\"CompletionNamespaceId\" IS NOT NULL AND \"CompletionId\" IS NOT NULL AND " +
                "\"CompletionNamespaceId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND " +
                "\"CompletionId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"MutationKind\" = 4)"));
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => item.Id, "IX_Workbench_ProjectCrossModuleMutations_UnfinishedDeletion")
            .HasFilter(ProjectCrossModuleMutationRecord.PendingDeletionFilter);
        builder.HasIndex(item => new { item.ProjectId, item.Id }, "IX_Workbench_ProjectDeletion_Subject")
            .HasFilter(FormattableString.Invariant($"\"MutationKind\" = {(int)ProjectCrossModuleMutationKind.DeleteProject}"));
        builder.Property(item => item.ScopeNodeKey).HasMaxLength(160).IsRequired();
        builder.Property(item => item.PayloadJson).HasColumnType("TEXT");
        builder.Property(item => item.ErrorMessage).HasColumnType("TEXT");
        builder.HasIndex(item => new { item.ProjectId, item.ScopeNodeKey, item.CreatedAtUtc });
        builder.HasIndex(item => new { item.ProjectId, item.Status, item.UpdatedAtUtc });
        builder.HasIndex(item => new { item.ProjectId, item.ApprovalState, item.Status, item.UpdatedAtUtc });
    }
}

public sealed class ProjectCrossModuleMutationCoordinator(IClock clock)
{
    internal ProjectCrossModuleMutationRecord Begin(
        Guid projectId,
        string scopeNodeKey,
        ProjectCrossModuleMutationKind mutationKind,
        string payloadJson,
        ProjectCrossModuleMutationApprovalState approvalState = ProjectCrossModuleMutationApprovalState.NotRequired)
    {
        var timestamp = clock.GetUtcNow();
        return new ProjectCrossModuleMutationRecord
        {
            ProjectId = projectId,
            ScopeNodeKey = scopeNodeKey.Trim(),
            MutationKind = mutationKind,
            Status = ProjectCrossModuleMutationStatus.Pending,
            ApprovalState = approvalState,
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
        record.CompletedAtUtc = record.UpdatedAtUtc;
    }

    internal void MarkFailed(ProjectCrossModuleMutationRecord record, string errorMessage)
    {
        Update(record, ProjectCrossModuleMutationStatus.Failed, errorMessage);
        record.CompletedAtUtc = null;
    }

    internal void MarkAttempt(ProjectCrossModuleMutationRecord record)
    {
        record.AttemptCount++;
        record.LastAttemptAtUtc = clock.GetUtcNow();
        record.UpdatedAtUtc = record.LastAttemptAtUtc.Value;
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
