using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.CrmHr;

public enum PartyType
{
    Person,
    Organization,
    OrganizationUnit,
    AiAgent
}

public enum PartyLifecycleStatus
{
    Draft,
    Active,
    Inactive,
    Archived,
    Former,
    Candidate,
    Prospect
}

public enum PartyRoleKind
{
    Customer,
    CustomerContact,
    Partner,
    Vendor,
    Employee,
    Contractor,
    Freelancer,
    DeliveryUnit,
    Candidate,
    AiSteward,
    AccountManager,
    Recruiter,
    Stakeholder
}

public enum PartyContactType
{
    Email,
    Phone,
    Website,
    Messaging,
    Social,
    Other
}

public enum PartyRelationshipKind
{
    MemberOf,
    PartOf,
    ReportsTo,
    CustomerOf,
    PartnerOf,
    VendorTo,
    Represents,
    ManagedBy,
    OwnedBy,
    Supports
}

public enum LookupCatalogKind
{
    OpportunityStage,
    RelationshipStage,
    AssignmentKind
}

public sealed class Party
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public PartyType PartyType { get; set; }
    public PartyLifecycleStatus LifecycleStatus { get; set; } = PartyLifecycleStatus.Draft;
    public string DisplayName { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string PreferredName { get; set; } = string.Empty;
    public string ExternalCode { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string TagsJson { get; set; } = "[]";
    public string Region { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public bool IsSensitive { get; set; }
    public string ExtendedDataJson { get; set; } = "{}";
    public string LastChangedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class PartyRoleAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PartyId { get; set; }
    public PartyRoleKind RoleKind { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public DateTimeOffset? ValidFromUtc { get; set; }
    public DateTimeOffset? ValidToUtc { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class PartyContactPoint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PartyId { get; set; }
    public PartyContactType ContactType { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string NormalizedValue { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public bool IsPublic { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class PartyAddress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PartyId { get; set; }
    public string AddressType { get; set; } = string.Empty;
    public string Line1 { get; set; } = string.Empty;
    public string Line2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class PartyRelationship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourcePartyId { get; set; }
    public Guid TargetPartyId { get; set; }
    public PartyRelationshipKind RelationshipKind { get; set; }
    public bool IsPrimary { get; set; }
    public DateTimeOffset? StartDateUtc { get; set; }
    public DateTimeOffset? EndDateUtc { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class PartyConfidentialNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PartyId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string NoteText { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class CrmHrAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string DetailJson { get; set; } = "{}";
    public string Actor { get; set; } = string.Empty;
    public bool IsSensitive { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CrmHrLookupOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public LookupCatalogKind CatalogKind { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsSystemDefault { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class PartyConfiguration : IEntityTypeConfiguration<Party>
{
    public void Configure(EntityTypeBuilder<Party> builder)
    {
        builder.ToTable("CrmHr_Parties");
        builder.HasKey(party => party.Id);
        builder.Property(party => party.PartyType).HasConversion<string>().HasMaxLength(64);
        builder.Property(party => party.LifecycleStatus).HasConversion<string>().HasMaxLength(64);
        builder.Property(party => party.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(party => party.LegalName).HasMaxLength(200);
        builder.Property(party => party.PreferredName).HasMaxLength(200);
        builder.Property(party => party.ExternalCode).HasMaxLength(120);
        builder.Property(party => party.Summary).HasColumnType("TEXT");
        builder.Property(party => party.Notes).HasColumnType("TEXT");
        builder.Property(party => party.TagsJson).HasColumnType("TEXT");
        builder.Property(party => party.Region).HasMaxLength(120);
        builder.Property(party => party.CountryCode).HasMaxLength(16);
        builder.Property(party => party.TimeZone).HasMaxLength(80);
        builder.Property(party => party.ExtendedDataJson).HasColumnType("TEXT");
        builder.Property(party => party.LastChangedBy).HasMaxLength(160);
        builder.HasIndex(party => party.DisplayName);
        builder.HasIndex(party => new { party.PartyType, party.LifecycleStatus });
        builder.HasIndex(party => party.ExternalCode);
    }
}

internal sealed class PartyRoleAssignmentConfiguration : IEntityTypeConfiguration<PartyRoleAssignment>
{
    public void Configure(EntityTypeBuilder<PartyRoleAssignment> builder)
    {
        builder.ToTable("CrmHr_PartyRoles");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.RoleKind).HasConversion<string>().HasMaxLength(80);
        builder.Property(role => role.Title).HasMaxLength(160);
        builder.Property(role => role.Notes).HasColumnType("TEXT");
        builder.HasIndex(role => new { role.PartyId, role.RoleKind });
    }
}

internal sealed class PartyContactPointConfiguration : IEntityTypeConfiguration<PartyContactPoint>
{
    public void Configure(EntityTypeBuilder<PartyContactPoint> builder)
    {
        builder.ToTable("CrmHr_PartyContactPoints");
        builder.HasKey(contactPoint => contactPoint.Id);
        builder.Property(contactPoint => contactPoint.ContactType).HasConversion<string>().HasMaxLength(64);
        builder.Property(contactPoint => contactPoint.Label).HasMaxLength(120);
        builder.Property(contactPoint => contactPoint.Value).HasMaxLength(400).IsRequired();
        builder.Property(contactPoint => contactPoint.NormalizedValue).HasMaxLength(400);
        builder.Property(contactPoint => contactPoint.Notes).HasColumnType("TEXT");
        builder.HasIndex(contactPoint => contactPoint.NormalizedValue);
        builder.HasIndex(contactPoint => new { contactPoint.PartyId, contactPoint.IsPrimary });
    }
}

internal sealed class PartyAddressConfiguration : IEntityTypeConfiguration<PartyAddress>
{
    public void Configure(EntityTypeBuilder<PartyAddress> builder)
    {
        builder.ToTable("CrmHr_PartyAddresses");
        builder.HasKey(address => address.Id);
        builder.Property(address => address.AddressType).HasMaxLength(80);
        builder.Property(address => address.Line1).HasMaxLength(200).IsRequired();
        builder.Property(address => address.Line2).HasMaxLength(200);
        builder.Property(address => address.City).HasMaxLength(120);
        builder.Property(address => address.Region).HasMaxLength(120);
        builder.Property(address => address.PostalCode).HasMaxLength(40);
        builder.Property(address => address.CountryCode).HasMaxLength(16);
        builder.Property(address => address.Notes).HasColumnType("TEXT");
        builder.HasIndex(address => new { address.PartyId, address.IsPrimary });
    }
}

internal sealed class PartyRelationshipConfiguration : IEntityTypeConfiguration<PartyRelationship>
{
    public void Configure(EntityTypeBuilder<PartyRelationship> builder)
    {
        builder.ToTable("CrmHr_PartyRelationships");
        builder.HasKey(relationship => relationship.Id);
        builder.Property(relationship => relationship.RelationshipKind).HasConversion<string>().HasMaxLength(64);
        builder.Property(relationship => relationship.Notes).HasColumnType("TEXT");
        builder.HasIndex(relationship => new { relationship.SourcePartyId, relationship.TargetPartyId, relationship.RelationshipKind });
        builder.HasIndex(relationship => relationship.TargetPartyId);
    }
}

internal sealed class PartyConfidentialNoteConfiguration : IEntityTypeConfiguration<PartyConfidentialNote>
{
    public void Configure(EntityTypeBuilder<PartyConfidentialNote> builder)
    {
        builder.ToTable("CrmHr_ConfidentialNotes");
        builder.HasKey(note => note.Id);
        builder.Property(note => note.Category).HasMaxLength(80);
        builder.Property(note => note.NoteText).HasColumnType("TEXT");
        builder.Property(note => note.CreatedBy).HasMaxLength(160);
        builder.HasIndex(note => note.PartyId);
    }
}

internal sealed class CrmHrAuditEntryConfiguration : IEntityTypeConfiguration<CrmHrAuditEntry>
{
    public void Configure(EntityTypeBuilder<CrmHrAuditEntry> builder)
    {
        builder.ToTable("CrmHr_AuditEntries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.EntityType).HasMaxLength(120).IsRequired();
        builder.Property(entry => entry.Action).HasMaxLength(80).IsRequired();
        builder.Property(entry => entry.Summary).HasMaxLength(400).IsRequired();
        builder.Property(entry => entry.DetailJson).HasColumnType("TEXT");
        builder.Property(entry => entry.Actor).HasMaxLength(160);
        builder.HasIndex(entry => new { entry.EntityType, entry.EntityId });
    }
}

internal sealed class CrmHrLookupOptionConfiguration : IEntityTypeConfiguration<CrmHrLookupOption>
{
    public void Configure(EntityTypeBuilder<CrmHrLookupOption> builder)
    {
        builder.ToTable("CrmHr_LookupOptions");
        builder.HasKey(option => option.Id);
        builder.Property(option => option.CatalogKind).HasConversion<string>().HasMaxLength(64);
        builder.Property(option => option.Key).HasMaxLength(120).IsRequired();
        builder.Property(option => option.DisplayName).HasMaxLength(160).IsRequired();
        builder.HasIndex(option => new { option.CatalogKind, option.Key }).IsUnique();
    }
}
