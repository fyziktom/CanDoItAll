using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class ProviderProfile : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public ProviderKind? ProviderKind { get; set; }

    public string ConnectorPluginKey { get; set; } = string.Empty;

    public string ConfigSchemaVersion { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public Guid? ApiKeySecretId { get; set; }

    public string DefaultModel { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 45;

    public bool IsEnabled { get; set; } = true;

    public bool SupportsStreaming { get; set; }

    public bool SupportsToolCalling { get; set; }

    public bool SupportsStructuredOutput { get; set; }

    public bool SupportsVision { get; set; }

    public DateTimeOffset? LastHealthCheckAtUtc { get; set; }

    public string? LastHealthStatus { get; set; }

    public string ExtraSettingsJson { get; set; } = "{}";

    public Guid ConcurrencyToken { get; set; }
}

internal sealed class ProviderProfileConfiguration : IEntityTypeConfiguration<ProviderProfile>
{
    public void Configure(EntityTypeBuilder<ProviderProfile> builder)
    {
        builder.ToTable("Workspace_ProviderProfiles");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.Name).HasMaxLength(200).IsRequired();
        builder.Property(profile => profile.BaseUrl).HasMaxLength(500).IsRequired();
        builder.Property(profile => profile.ConnectorPluginKey).HasMaxLength(160).IsRequired();
        builder.Property(profile => profile.ConfigSchemaVersion).HasMaxLength(40).IsRequired();
        builder.Property(profile => profile.DefaultModel).HasMaxLength(120);
        builder.Property(profile => profile.LastHealthStatus).HasMaxLength(120);
        builder.Property(profile => profile.ExtraSettingsJson).HasColumnType("TEXT");
        builder.Property(profile => profile.ConcurrencyToken).IsConcurrencyToken();
    }
}
