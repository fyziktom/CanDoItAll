using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Plugins;

public sealed class PluginCapabilityGrantRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string PluginId { get; set; } = string.Empty;

    public int Capability { get; set; }

    public string RecipeId { get; set; } = string.Empty;

    public string ScopeKind { get; set; } = "Plugin";

    public string ScopeKey { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string RiskKind { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string UpdatedBy { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class PluginConnectionRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string PluginId { get; set; } = string.Empty;

    public string ConnectionKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string SettingsJson { get; set; } = "{}";

    public bool IsEnabled { get; set; } = true;

    public string HealthStatus { get; set; } = "Not checked";

    public string UpdatedBy { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

internal sealed class PluginCapabilityGrantRecordConfiguration : IEntityTypeConfiguration<PluginCapabilityGrantRecord>
{
    public void Configure(EntityTypeBuilder<PluginCapabilityGrantRecord> builder)
    {
        builder.ToTable("Plugins_CapabilityGrants");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.PluginId).HasMaxLength(180).IsRequired();
        builder.Property(item => item.RecipeId).HasMaxLength(180).IsRequired();
        builder.Property(item => item.ScopeKind).HasMaxLength(40).IsRequired();
        builder.Property(item => item.ScopeKey).HasMaxLength(180).IsRequired();
        builder.Property(item => item.State).HasMaxLength(40).IsRequired();
        builder.Property(item => item.RiskKind).HasMaxLength(40).IsRequired();
        builder.Property(item => item.Reason).HasMaxLength(600).IsRequired();
        builder.Property(item => item.UpdatedBy).HasMaxLength(180).IsRequired();
        builder.HasIndex(item => new
        {
            item.PluginId,
            item.Capability,
            item.RecipeId,
            item.ScopeKind,
            item.ScopeKey
        }).IsUnique();
        builder.HasIndex(item => new
        {
            item.PluginId,
            item.State,
            item.UpdatedAtUtc
        });
    }
}

internal sealed class PluginConnectionRecordConfiguration : IEntityTypeConfiguration<PluginConnectionRecord>
{
    public void Configure(EntityTypeBuilder<PluginConnectionRecord> builder)
    {
        builder.ToTable("Plugins_Connections");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.PluginId).HasMaxLength(180).IsRequired();
        builder.Property(item => item.ConnectionKey).HasMaxLength(180).IsRequired();
        builder.Property(item => item.DisplayName).HasMaxLength(240).IsRequired();
        builder.Property(item => item.SettingsJson).HasColumnType("TEXT").IsRequired();
        builder.Property(item => item.HealthStatus).HasMaxLength(180).IsRequired();
        builder.Property(item => item.UpdatedBy).HasMaxLength(180).IsRequired();
        builder.HasIndex(item => new
        {
            item.PluginId,
            item.ConnectionKey,
            item.DisplayName
        });
        builder.HasIndex(item => new
        {
            item.PluginId,
            item.ConnectionKey
        });
    }
}
