using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Workspace;

public enum ConnectorCommandStatus
{
    Pending,
    Completed,
    DeadLettered,
    Rejected
}

public enum ConnectorCommandApprovalState
{
    NotRequired,
    Pending,
    Approved,
    Rejected
}

public enum ConnectorCommandAuditEventKind
{
    Enqueued,
    IdempotencyHit,
    ApprovalRequested,
    Approved,
    Rejected,
    AttemptStarted,
    AttemptFailed,
    Completed,
    DeadLettered,
    Replayed
}

public enum ConnectorCommandExecutionOutcome
{
    Completed,
    RetryableFailure,
    PermanentFailure
}

public sealed class ConnectorCommandRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string ConnectorPluginKey { get; set; } = string.Empty;
    public string CommandKey { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public ConnectorCommandStatus Status { get; set; } = ConnectorCommandStatus.Pending;
    public ConnectorCommandApprovalState ApprovalState { get; set; } = ConnectorCommandApprovalState.NotRequired;
    public int AttemptCount { get; set; }
    public DateTimeOffset? LastAttemptAtUtc { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string LastError { get; set; } = string.Empty;
    public string ResultJson { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class ConnectorCommandRecordConfiguration : IEntityTypeConfiguration<ConnectorCommandRecord>
{
    public void Configure(EntityTypeBuilder<ConnectorCommandRecord> builder)
    {
        builder.ToTable("Workspace_ConnectorCommands");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ConnectorPluginKey).HasMaxLength(160).IsRequired();
        builder.Property(item => item.CommandKey).HasMaxLength(160).IsRequired();
        builder.Property(item => item.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(item => item.PayloadJson).HasColumnType("TEXT");
        builder.Property(item => item.LastError).HasColumnType("TEXT");
        builder.Property(item => item.ResultJson).HasColumnType("TEXT");
        builder.Property(item => item.RequestedBy).HasMaxLength(160).IsRequired();
        builder.HasIndex(item => new
        {
            item.ProjectId,
            item.ConnectorPluginKey,
            item.CommandKey,
            item.IdempotencyKey
        }).IsUnique();
        builder.HasIndex(item => new
        {
            item.Status,
            item.ApprovalState,
            item.NextAttemptAtUtc
        });
        builder.HasIndex(item => new
        {
            item.ProjectId,
            item.CreatedAtUtc
        });
    }
}

public sealed class ConnectorCommandAuditRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConnectorCommandId { get; set; }
    public Guid ProjectId { get; set; }
    public ConnectorCommandAuditEventKind EventKind { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string DetailsJson { get; set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class ConnectorCommandAuditRecordConfiguration : IEntityTypeConfiguration<ConnectorCommandAuditRecord>
{
    public void Configure(EntityTypeBuilder<ConnectorCommandAuditRecord> builder)
    {
        builder.ToTable("Workspace_ConnectorCommandAudits");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Actor).HasMaxLength(160).IsRequired();
        builder.Property(item => item.Message).HasMaxLength(400).IsRequired();
        builder.Property(item => item.DetailsJson).HasColumnType("TEXT");
        builder.HasIndex(item => new
        {
            item.ConnectorCommandId,
            item.CreatedAtUtc
        });
        builder.HasOne<ConnectorCommandRecord>()
            .WithMany()
            .HasForeignKey(item => item.ConnectorCommandId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed record ConnectorCommandEnqueueRequest(
    Guid ProjectId,
    string ConnectorPluginKey,
    string CommandKey,
    string PayloadJson,
    string IdempotencyKey,
    string RequestedBy,
    bool RequiresApproval = false);

public sealed record ConnectorCommandEnqueueResult(
    Guid CommandId,
    bool IsDuplicate,
    ConnectorCommandStatus Status,
    ConnectorCommandApprovalState ApprovalState);

public sealed record ConnectorCommandSnapshot(
    Guid Id,
    Guid ProjectId,
    string ConnectorPluginKey,
    string CommandKey,
    string IdempotencyKey,
    ConnectorCommandStatus Status,
    ConnectorCommandApprovalState ApprovalState,
    int AttemptCount,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset? NextAttemptAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string LastError,
    string ResultJson,
    string RequestedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record ConnectorCommandAuditEntry(
    Guid Id,
    Guid ConnectorCommandId,
    Guid ProjectId,
    ConnectorCommandAuditEventKind EventKind,
    string Actor,
    string Message,
    string DetailsJson,
    DateTimeOffset CreatedAtUtc);

public sealed record ConnectorCommandExecutionRequest(
    Guid CommandId,
    Guid ProjectId,
    string ConnectorPluginKey,
    string CommandKey,
    string PayloadJson,
    string IdempotencyKey,
    int AttemptCount);

public sealed record ConnectorCommandExecutionResult(
    ConnectorCommandExecutionOutcome Outcome,
    string ResultJson,
    string ErrorMessage)
{
    public static ConnectorCommandExecutionResult Completed(string resultJson = "")
    {
        return new ConnectorCommandExecutionResult(
            ConnectorCommandExecutionOutcome.Completed,
            resultJson,
            string.Empty);
    }

    public static ConnectorCommandExecutionResult RetryableFailure(string errorMessage, string resultJson = "")
    {
        return new ConnectorCommandExecutionResult(
            ConnectorCommandExecutionOutcome.RetryableFailure,
            resultJson,
            errorMessage);
    }

    public static ConnectorCommandExecutionResult PermanentFailure(string errorMessage, string resultJson = "")
    {
        return new ConnectorCommandExecutionResult(
            ConnectorCommandExecutionOutcome.PermanentFailure,
            resultJson,
            errorMessage);
    }
}

public interface IConnectorCommandHandler
{
    bool CanHandle(string connectorPluginKey, string commandKey);

    Task<ConnectorCommandExecutionResult> ExecuteAsync(
        ConnectorCommandExecutionRequest request,
        CancellationToken cancellationToken);
}
