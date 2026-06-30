using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Plugins;

public sealed class PluginInstallationRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string PluginId { get; set; } = string.Empty;

    public string PackageId { get; set; } = string.Empty;

    public string DisplayNameSnapshot { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Vendor { get; set; } = string.Empty;

    public string ManifestSnapshotJson { get; set; } = "{}";

    public bool IsEnabled { get; set; } = true;

    public string InstalledBy { get; set; } = string.Empty;

    public DateTimeOffset InstalledAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

internal sealed class PluginInstallationRecordConfiguration : IEntityTypeConfiguration<PluginInstallationRecord>
{
    public void Configure(EntityTypeBuilder<PluginInstallationRecord> builder)
    {
        builder.ToTable("Plugins_Installations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.PluginId).HasMaxLength(180).IsRequired();
        builder.Property(item => item.PackageId).HasMaxLength(180).IsRequired();
        builder.Property(item => item.DisplayNameSnapshot).HasMaxLength(240).IsRequired();
        builder.Property(item => item.Version).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Vendor).HasMaxLength(180).IsRequired();
        builder.Property(item => item.ManifestSnapshotJson).HasColumnType("TEXT");
        builder.Property(item => item.InstalledBy).HasMaxLength(180).IsRequired();
        builder.HasIndex(item => item.PluginId).IsUnique();
        builder.HasIndex(item => new
        {
            item.IsEnabled,
            item.UpdatedAtUtc
        });
    }
}
