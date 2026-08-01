using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Processes.Persistence;

internal sealed class ProcessRunRecordEntityConfiguration : IEntityTypeConfiguration<ProcessRunRecordEntity>
{
    public void Configure(EntityTypeBuilder<ProcessRunRecordEntity> builder)
    {
        builder.ToTable("process_run_records");
        builder.HasKey(record => record.RunId);
        builder.Property(record => record.Disposition).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(record => record.LifecycleState).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(record => record.Completeness).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(record => record.EstimatedCost).HasPrecision(20, 6);
        builder.Property(record => record.ActualCost).HasPrecision(20, 6);
        builder.Property(record => record.FactsJson).HasColumnType("jsonb");
        builder.Property(record => record.ParticipantIdsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(record => record.AvailableEvidenceSources).HasConversion<string>().HasMaxLength(256).IsRequired();
        builder.Property(record => record.MissingEvidenceSources).HasConversion<string>().HasMaxLength(256).IsRequired();
        builder.Property(record => record.CompletenessWarningsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(record => record.FactsStatus).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(record => record.FactsLastErrorClass).HasMaxLength(256);
        builder.Property(record => record.FactsLastErrorDiagnosticReference).HasMaxLength(512);
        builder.Property(record => record.NarrativeJson).HasColumnType("jsonb");
        builder.Property(record => record.NarrativeStatus).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(record => record.NarrativeLastErrorClass).HasMaxLength(256);
        builder.Property(record => record.NarrativeLastErrorDiagnosticReference).HasMaxLength(512);
        builder.Property(record => record.SchemaVersion).HasMaxLength(64).IsRequired();
        builder.HasIndex(record => new { record.EndedAtUtc, record.RunId })
            .IsDescending(true, true);
        builder.HasIndex(record => record.ProjectId);
        builder.HasIndex(record => record.DefinitionId);
        builder.HasIndex(record => record.RootRunId);
        builder.HasIndex(record => new
            {
                record.LifecycleState,
                record.EndedAtUtc,
                record.RunId
            })
            .IsDescending(false, true, true);
        builder.HasIndex(record => new
            {
                record.LifecycleState,
                record.ProjectId,
                record.EndedAtUtc,
                record.RunId
            })
            .IsDescending(false, false, true, true);
        builder.HasIndex(record => new
            {
                record.LifecycleState,
                record.DefinitionId,
                record.EndedAtUtc,
                record.RunId
            })
            .IsDescending(false, false, true, true);
        builder.HasIndex(record => new
            {
                record.LifecycleState,
                record.RootRunId,
                record.EndedAtUtc,
                record.RunId
            })
            .IsDescending(false, false, true, true);
        builder.HasIndex(record => new
            {
                record.LifecycleState,
                record.ParentRunId,
                record.EndedAtUtc,
                record.RunId
            })
            .IsDescending(false, false, true, true);
        builder.HasIndex(record => new
            {
                record.LifecycleState,
                record.Disposition,
                record.EndedAtUtc,
                record.RunId
            })
            .IsDescending(false, false, true, true);
        builder.HasIndex(record => new
            {
                record.LifecycleState,
                record.FactsStatus,
                record.FactsNextAttemptAtUtc,
                record.FactsLeaseExpiresAtUtc
            });
        builder.HasIndex(record => new
            {
                record.LifecycleState,
                record.NarrativeStatus,
                record.NarrativeNextAttemptAtUtc,
                record.NarrativeLeaseExpiresAtUtc
            });
    }
}

internal sealed class ProcessRunRecordParticipantEntityConfiguration :
    IEntityTypeConfiguration<ProcessRunRecordParticipantEntity>
{
    public void Configure(EntityTypeBuilder<ProcessRunRecordParticipantEntity> builder)
    {
        builder.ToTable("process_run_record_participants");
        builder.HasKey(participant => new { participant.ParticipantId, participant.RunId });
        builder.Property(participant => participant.ParticipantId).HasMaxLength(256).IsRequired();
        builder.HasIndex(participant => participant.RunId);
    }
}
