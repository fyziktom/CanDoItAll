using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Workspace;

internal sealed class ProviderSharePublicationConfiguration
    : IEntityTypeConfiguration<ProviderSharePublication>
{
    public void Configure(EntityTypeBuilder<ProviderSharePublication> builder)
    {
        builder.ToTable(
            "Workspace_ProviderSharePublications",
            table => table.HasCheckConstraint(
                "CK_Workspace_ProviderSharePublications_PublicIdentity",
                "\"PublicId\" <> \"ProviderProfileId\""));
        builder.HasKey(publication => publication.Id);
        builder.Property(publication => publication.PublicId)
            .HasConversion(
                publicId => publicId.Value,
                value => new(value));
        builder.Property(publication => publication.ConcurrencyToken).IsConcurrencyToken();
        builder.HasAlternateKey(publication => publication.PublicId);
        builder.HasAlternateKey(publication => new
        {
            publication.PublicId,
            publication.ProviderProfileId
        });
        builder.HasIndex(publication => publication.ProviderProfileId).IsUnique();
        builder.HasIndex(publication => new
        {
            publication.IsPublished,
            publication.UpdatedAtUtc
        });
        builder.HasOne<ProviderProfile>()
            .WithOne()
            .HasForeignKey<ProviderSharePublication>(publication => publication.ProviderProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
