using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

internal sealed class SharedProviderImportConfiguration
    : IEntityTypeConfiguration<SharedProviderImport>
{
    public void Configure(EntityTypeBuilder<SharedProviderImport> builder)
    {
        builder.ToTable("Workspace_SharedProviderImports");
        builder.HasKey(import => import.Id);
        builder.Property(import => import.RemotePublicationId)
            .HasConversion(
                publicationId => publicationId.Value,
                value => new(value));
        builder.Property(import => import.RemoteDisplayName).HasMaxLength(256).IsRequired();
        builder.Property(import => import.RemoteRevision)
            .HasConversion(
                revision => revision.Value,
                value => new(value))
            .HasMaxLength(71)
            .IsRequired();
        builder.Property(import => import.RemotePurpose)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(import => import.RemoteTransport)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(import => import.RemoteDefaultModelId)
            .HasConversion(
                modelId => modelId.Value,
                value => SharedProviderRoutingModelIdCodec.Parse(value))
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(import => import.RemoteCatalogSnapshotJson)
            .HasColumnType("TEXT")
            .IsRequired();
        builder.Property(import => import.SelectionState)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(import => import.AvailabilityState)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(import => import.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(import => new
        {
            import.SourceId,
            import.RemotePublicationId
        }).IsUnique();
        builder.HasIndex(import => import.ProviderProfileId).IsUnique();
        builder.HasIndex(import => new
        {
            import.SelectionState,
            import.AvailabilityState,
            import.UpdatedAtUtc
        });
        builder.HasOne<SharedProviderSource>()
            .WithMany()
            .HasForeignKey(import => import.SourceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProviderProfile>()
            .WithOne()
            .HasForeignKey<SharedProviderImport>(import => import.ProviderProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
