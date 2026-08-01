using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Plugins;

public sealed class PluginLogRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string PluginId { get; set; } = string.Empty;

    public string PackageId { get; set; } = string.Empty;

    public string WorkflowExecutorId { get; set; } = string.Empty;

    public string StreamKind { get; set; } = string.Empty;

    public string OperationKind { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string DetailsJson { get; set; } = "{}";

    public string CorrelationId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

internal sealed class PluginLogRecordConfiguration : IEntityTypeConfiguration<PluginLogRecord>
{
    public void Configure(EntityTypeBuilder<PluginLogRecord> builder)
    {
        builder.ToTable("Plugins_Logs");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.PluginId).HasMaxLength(180).IsRequired();
        builder.Property(item => item.PackageId).HasMaxLength(180).IsRequired();
        builder.Property(item => item.WorkflowExecutorId).HasMaxLength(180).IsRequired();
        builder.Property(item => item.StreamKind).HasMaxLength(40).IsRequired();
        builder.Property(item => item.OperationKind).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Severity).HasMaxLength(40).IsRequired();
        builder.Property(item => item.Status).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Message).HasMaxLength(1200).IsRequired();
        builder.Property(item => item.DetailsJson).HasColumnType("TEXT").IsRequired();
        builder.Property(item => item.CorrelationId).HasMaxLength(180).IsRequired();
        builder.HasIndex(item => new
        {
            item.StreamKind,
            item.PluginId,
            item.CreatedAtUtc
        });
        builder.HasIndex(item => new
        {
            item.PackageId,
            item.CreatedAtUtc
        });
    }
}
