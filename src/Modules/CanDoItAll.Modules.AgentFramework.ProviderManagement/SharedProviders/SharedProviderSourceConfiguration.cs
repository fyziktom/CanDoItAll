using CanDoItAll.Modules.Security;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

internal sealed class SharedProviderSourceConfiguration
    : IEntityTypeConfiguration<SharedProviderSource>
{
    public void Configure(EntityTypeBuilder<SharedProviderSource> builder)
    {
        var sourceInstanceIdConverter = new ValueConverter<SharedProviderSourceInstanceId, Guid>(
            sourceInstanceId => sourceInstanceId.Value,
            value => new(value));
        var entityTagConverter = new ValueConverter<SharedProviderCatalogEntityTag, string>(
            entityTag => entityTag.Value,
            value => new(value));

        builder.ToTable("Workspace_SharedProviderSources");
        builder.HasKey(source => source.Id);
        builder.Property(source => source.Name).HasMaxLength(200).IsRequired();
        builder.Property(source => source.BaseUri).HasMaxLength(2_048).IsRequired();
        builder.Property(source => source.Status)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(source => source.RemoteInstanceId)
            .HasConversion(sourceInstanceIdConverter);
        builder.Property(source => source.LastCatalogETag)
            .HasConversion(entityTagConverter)
            .HasMaxLength(73);
        builder.Property(source => source.LastStatusMessage).HasMaxLength(400).IsRequired();
        builder.Property(source => source.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(source => source.BaseUri);
        builder.HasIndex(source => new
        {
            source.IsEnabled,
            source.Status,
            source.UpdatedAtUtc
        });
        builder.HasOne<SecretRecord>()
            .WithMany()
            .HasForeignKey(source => source.ApiTokenSecretId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
