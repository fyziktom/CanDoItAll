using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.CognitiveMemory;

internal sealed class CognitiveMemoryScoreEvaluationTraceRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryScoreEvaluationTraceRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryScoreEvaluationTraceRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ScoreEvaluations");
        builder.HasKey(trace => trace.Id);
        builder.Property(trace => trace.SchemaVersion).HasMaxLength(80).IsRequired();
        builder.Property(trace => trace.NormalizationProfile).HasMaxLength(120).IsRequired();
        builder.Property(trace => trace.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder.Property(trace => trace.InputHash).HasMaxLength(128).IsRequired();
        builder.Property(trace => trace.TracePayloadJson).HasColumnType("TEXT");
        builder.Property(trace => trace.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(trace => new { trace.ProjectId, trace.SpaceKind, trace.SchemaVersion, trace.CalculatedAtUtc });
        builder.HasIndex(trace => new { trace.OwnerKind, trace.OwnerId, trace.SpaceKind });
        builder.HasIndex(trace => trace.InputHash);
    }
}

internal sealed class CognitiveMemoryScoreComponentRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryScoreComponentRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryScoreComponentRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ScoreComponents");
        builder.HasKey(component => component.Id);
        builder.Property(component => component.SchemaVersion).HasMaxLength(80).IsRequired();
        builder.Property(component => component.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder.Property(component => component.ComponentPayloadJson).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryScoreEvaluationTraceRecord>()
            .WithMany()
            .HasForeignKey(component => component.ScoreEvaluationTraceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(component => new { component.ScoreEvaluationTraceId, component.DimensionKind });
        builder.HasIndex(component => new { component.ProjectId, component.SpaceKind, component.DimensionKind, component.CalculatedAtUtc });
        builder.HasIndex(component => new { component.OwnerKind, component.OwnerId, component.DimensionKind });
        builder.HasIndex(component => new { component.SchemaVersion, component.DimensionKind });
    }
}
