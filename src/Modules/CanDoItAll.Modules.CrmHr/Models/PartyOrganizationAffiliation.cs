using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.CrmHr;

public enum PartyOrganizationAffiliationKind
{
    Employee,
    Contractor,
    Freelancer,
    ExternalContact
}

public sealed class PartyOrganizationAffiliation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonPartyId { get; set; }
    public Guid OrganizationPartyId { get; set; }
    public PartyOrganizationAffiliationKind AffiliationKind { get; set; }
    public bool IsPrimary { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public Guid? OrganizationUnitPartyId { get; set; }
    public Guid? ManagerPartyId { get; set; }
    public DateTimeOffset? ValidFromUtc { get; set; }
    public DateTimeOffset? ValidToUtc { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string LastChangedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class PartyOrganizationAffiliationConfiguration
    : IEntityTypeConfiguration<PartyOrganizationAffiliation>
{
    public void Configure(EntityTypeBuilder<PartyOrganizationAffiliation> builder)
    {
        builder.ToTable(
            "CrmHr_PartyOrganizationAffiliations",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_CrmHr_PartyOrganizationAffiliations_ValidDates",
                    "\"ValidToUtc\" IS NULL OR \"ValidFromUtc\" IS NULL OR \"ValidToUtc\" >= \"ValidFromUtc\"");
                table.HasCheckConstraint(
                    "CK_CrmHr_PartyOrganizationAffiliations_PersonOrganization",
                    "\"PersonPartyId\" <> \"OrganizationPartyId\"");
                table.HasCheckConstraint(
                    "CK_CrmHr_PartyOrganizationAffiliations_PersonUnit",
                    "\"OrganizationUnitPartyId\" IS NULL OR \"PersonPartyId\" <> \"OrganizationUnitPartyId\"");
                table.HasCheckConstraint(
                    "CK_CrmHr_PartyOrganizationAffiliations_PersonManager",
                    "\"ManagerPartyId\" IS NULL OR \"PersonPartyId\" <> \"ManagerPartyId\"");
            });
        builder.HasKey(affiliation => affiliation.Id);
        builder.Property(affiliation => affiliation.AffiliationKind)
            .HasConversion<string>()
            .HasMaxLength(64);
        builder.Property(affiliation => affiliation.JobTitle).HasMaxLength(160);
        builder.Property(affiliation => affiliation.EmployeeCode).HasMaxLength(80);
        builder.Property(affiliation => affiliation.Notes).HasColumnType("TEXT");
        builder.Property(affiliation => affiliation.LastChangedBy).HasMaxLength(160);
        builder.Property(affiliation => affiliation.UpdatedAtUtc).IsConcurrencyToken();

        builder.HasOne<Party>()
            .WithMany()
            .HasForeignKey(affiliation => affiliation.PersonPartyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Party>()
            .WithMany()
            .HasForeignKey(affiliation => affiliation.OrganizationPartyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Party>()
            .WithMany()
            .HasForeignKey(affiliation => affiliation.OrganizationUnitPartyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Party>()
            .WithMany()
            .HasForeignKey(affiliation => affiliation.ManagerPartyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(affiliation => new
        {
            affiliation.PersonPartyId,
            affiliation.ValidFromUtc,
            affiliation.ValidToUtc
        });
        builder.HasIndex(affiliation => new
        {
            affiliation.PersonPartyId,
            affiliation.OrganizationPartyId,
            affiliation.AffiliationKind,
            affiliation.ValidFromUtc,
            affiliation.ValidToUtc
        })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("UX_CrmHr_Affiliations_BusinessKey");
        builder.HasIndex(affiliation => affiliation.OrganizationPartyId);
        builder.HasIndex(affiliation => affiliation.OrganizationUnitPartyId);
        builder.HasIndex(affiliation => affiliation.ManagerPartyId);
        builder.HasIndex(affiliation => affiliation.PersonPartyId)
            .IsUnique()
            .HasFilter("\"IsPrimary\" = TRUE")
            .HasDatabaseName("UX_CrmHr_Affiliations_PrimaryPerson");
    }
}
