using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.CognitiveMemory;

internal sealed class CognitiveMemoryConsolidationRunRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryConsolidationRunRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryConsolidationRunRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ConsolidationRuns");
        builder.HasKey(run => run.Id);
        builder.Property(run => run.ProfileName).HasMaxLength(120).IsRequired();
        builder.Property(run => run.IdempotencyKey).HasMaxLength(240).IsRequired();
        builder.Property(run => run.InputHash).HasMaxLength(128).IsRequired();
        builder.Property(run => run.OutputHash).HasMaxLength(128).IsRequired();
        builder.Property(run => run.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder.Property(run => run.Cursor).HasMaxLength(1000).IsRequired();
        builder.Property(run => run.NextCursor).HasMaxLength(1000).IsRequired();
        builder.Property(run => run.LeaseOwnerId).HasMaxLength(160).IsRequired();
        builder.Property(run => run.FailureCode).HasMaxLength(120).IsRequired();
        builder.Property(run => run.FailureMessage).HasColumnType("TEXT");
        builder.Property(run => run.ConcurrencyToken).IsConcurrencyToken();
        builder.HasOne<CognitiveMemoryRunRecord>()
            .WithOne()
            .HasForeignKey<CognitiveMemoryConsolidationRunRecord>(run => run.Id)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(run => run.IdempotencyKey).IsUnique();
        builder.HasIndex(run => new { run.ProjectId, run.Mode, run.Status, run.StartedAtUtc });
        builder.HasIndex(run => new { run.ProjectId, run.Mode, run.LeaseExpiresAtUtc });
        builder.HasIndex(run => run.InputHash);
    }
}

internal sealed class CognitiveMemoryConsolidationCandidateRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryConsolidationCandidateRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryConsolidationCandidateRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ConsolidationCandidates");
        builder.HasKey(candidate => candidate.Id);
        builder.Property(candidate => candidate.SourceContentHash).HasMaxLength(128).IsRequired();
        builder.Property(candidate => candidate.OutputHash).HasMaxLength(128).IsRequired();
        builder.Property(candidate => candidate.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder.Property(candidate => candidate.ReasonCode).HasMaxLength(120).IsRequired();
        builder.Property(candidate => candidate.ReasonText).HasColumnType("TEXT");
        builder.Property(candidate => candidate.PayloadJson).HasColumnType("TEXT");
        builder.Property(candidate => candidate.ConcurrencyToken).IsConcurrencyToken();
        builder.HasOne<CognitiveMemoryConsolidationRunRecord>()
            .WithMany()
            .HasForeignKey(candidate => candidate.RunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(candidate => new { candidate.RunId, candidate.CandidateKind, candidate.Status });
        builder.HasIndex(candidate => new { candidate.ProjectId, candidate.CandidateKind, candidate.Status });
        builder.HasIndex(candidate => new { candidate.ProjectId, candidate.SourceItemId, candidate.CandidateKind, candidate.SourceContentHash, candidate.AlgorithmVersion }).IsUnique();
        builder.HasIndex(candidate => candidate.MutationCommandId);
        builder.HasIndex(candidate => candidate.ReviewItemId);
        builder.HasIndex(candidate => candidate.ScoreEvaluationTraceId);
    }
}

internal sealed class CognitiveMemoryConsolidationCursorRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryConsolidationCursorRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryConsolidationCursorRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ConsolidationCursors");
        builder.HasKey(cursor => cursor.Id);
        builder.Property(cursor => cursor.SourceSystem).HasMaxLength(120).IsRequired();
        builder.Property(cursor => cursor.Cursor).HasMaxLength(1000).IsRequired();
        builder.Property(cursor => cursor.LastSourceHash).HasMaxLength(128).IsRequired();
        builder.Property(cursor => cursor.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(cursor => new { cursor.ProjectId, cursor.Mode, cursor.SourceSystem }).IsUnique();
        builder.HasIndex(cursor => cursor.LastRunId);
    }
}

internal sealed class CognitiveMemoryConsolidationReportRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryConsolidationReportRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryConsolidationReportRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ConsolidationReports");
        builder.HasKey(report => report.Id);
        builder.Property(report => report.ReportHash).HasMaxLength(128).IsRequired();
        builder.Property(report => report.ReportJson).HasColumnType("TEXT");
        builder.HasOne<CognitiveMemoryConsolidationRunRecord>()
            .WithOne()
            .HasForeignKey<CognitiveMemoryConsolidationReportRecord>(report => report.RunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(report => report.RunId).IsUnique();
        builder.HasIndex(report => new { report.ProjectId, report.CreatedAtUtc });
    }
}
