using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Workspace;

internal sealed class SharedProviderServiceIdentityConfiguration
    : IEntityTypeConfiguration<SharedProviderServiceIdentity>
{
    public void Configure(EntityTypeBuilder<SharedProviderServiceIdentity> builder)
    {
        builder.ToTable(
            "Workspace_SharedProviderServiceIdentity",
            table => table.HasCheckConstraint(
                "CK_Workspace_SharedProviderServiceIdentity_Singleton",
                $"\"Id\" = '{SharedProviderServiceIdentity.SingletonId:D}'"));
        builder.HasKey(identity => identity.Id);
        builder.Property(identity => identity.PublicId)
            .HasConversion(
                publicId => publicId.Value,
                value => new(value));
        builder.HasIndex(identity => identity.PublicId).IsUnique();
    }
}
