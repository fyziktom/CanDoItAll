using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureLeaseRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ProjectStructureLeaseScopeKind ScopeKind { get; set; }

    public string ScopeKey { get; set; } = string.Empty;

    public string LeaseToken { get; set; } = string.Empty;

    public string AgentId { get; set; } = string.Empty;

    public string AgentName { get; set; } = string.Empty;

    public string MachineName { get; set; } = string.Empty;

    public string RepositoryRoot { get; set; } = string.Empty;

    public string BranchName { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset AcquiredAtUtc { get; set; }

    public DateTimeOffset RenewedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? ReleasedAtUtc { get; set; }
}

internal sealed class ProjectStructureLeaseRecordConfiguration : IEntityTypeConfiguration<ProjectStructureLeaseRecord>
{
    public void Configure(EntityTypeBuilder<ProjectStructureLeaseRecord> builder)
    {
        builder.ToTable("Workbench_ProjectStructureLeases");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ScopeKey).HasMaxLength(300).IsRequired();
        builder.Property(item => item.LeaseToken).HasMaxLength(120).IsRequired();
        builder.Property(item => item.AgentId).HasMaxLength(120).IsRequired();
        builder.Property(item => item.AgentName).HasMaxLength(200).IsRequired();
        builder.Property(item => item.MachineName).HasMaxLength(200).IsRequired();
        builder.Property(item => item.RepositoryRoot).HasMaxLength(600).IsRequired();
        builder.Property(item => item.BranchName).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Reason).HasColumnType("TEXT");
        builder.HasIndex(item => item.LeaseToken).IsUnique();
        builder.HasIndex(item => new { item.ScopeKind, item.ScopeKey });
    }
}

public sealed class ProjectStructureOperationAnalyticsRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string OperationName { get; set; } = string.Empty;

    public Guid? ProjectId { get; set; }

    public string? NodeKey { get; set; }

    public ProjectStructureLeaseScopeKind? ScopeKind { get; set; }

    public string? ScopeKey { get; set; }

    public string AgentId { get; set; } = string.Empty;

    public string AgentName { get; set; } = string.Empty;

    public string MachineName { get; set; } = string.Empty;

    public string RepositoryRoot { get; set; } = string.Empty;

    public string BranchName { get; set; } = string.Empty;

    public bool Succeeded { get; set; }

    public long DurationMs { get; set; }

    public int WarningCount { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public string RequestSummaryJson { get; set; } = "{}";

    public string ResponseSummaryJson { get; set; } = "{}";

    public string WarningsJson { get; set; } = "[]";

    public DateTimeOffset OccurredAtUtc { get; set; }
}

internal sealed class ProjectStructureOperationAnalyticsRecordConfiguration : IEntityTypeConfiguration<ProjectStructureOperationAnalyticsRecord>
{
    public void Configure(EntityTypeBuilder<ProjectStructureOperationAnalyticsRecord> builder)
    {
        builder.ToTable("Workbench_ProjectStructureOperationAnalytics");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.OperationName).HasMaxLength(160).IsRequired();
        builder.Property(item => item.NodeKey).HasMaxLength(160);
        builder.Property(item => item.ScopeKey).HasMaxLength(300);
        builder.Property(item => item.AgentId).HasMaxLength(120).IsRequired();
        builder.Property(item => item.AgentName).HasMaxLength(200).IsRequired();
        builder.Property(item => item.MachineName).HasMaxLength(200).IsRequired();
        builder.Property(item => item.RepositoryRoot).HasMaxLength(600).IsRequired();
        builder.Property(item => item.BranchName).HasMaxLength(200).IsRequired();
        builder.Property(item => item.ErrorCode).HasMaxLength(120);
        builder.Property(item => item.ErrorMessage).HasColumnType("TEXT");
        builder.Property(item => item.RequestSummaryJson).HasColumnType("TEXT");
        builder.Property(item => item.ResponseSummaryJson).HasColumnType("TEXT");
        builder.Property(item => item.WarningsJson).HasColumnType("TEXT");
        builder.HasIndex(item => item.OccurredAtUtc);
        builder.HasIndex(item => new { item.ProjectId, item.OperationName });
    }
}
