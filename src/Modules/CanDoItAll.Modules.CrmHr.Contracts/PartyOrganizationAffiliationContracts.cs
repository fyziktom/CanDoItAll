using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.CrmHr;

public enum PartyOrganizationAffiliationKind
{
    Employee,
    Contractor,
    Freelancer,
    ExternalContact
}

public static class PartyOrganizationAffiliationLimits
{
    public const int MaximumAffiliationsPerPerson = 100;
    public const int MaximumActorLength = 160;
    public const int MaximumJobTitleLength = 160;
    public const int MaximumEmployeeCodeLength = 80;
}

public sealed class PartyOrganizationAffiliationEditorModel
{
    public Guid? Id { get; set; }
    public Guid PersonPartyId { get; set; }
    public Guid OrganizationPartyId { get; set; }
    public PartyOrganizationAffiliationKind AffiliationKind { get; set; }
        = PartyOrganizationAffiliationKind.ExternalContact;
    public bool IsPrimary { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public Guid? OrganizationUnitPartyId { get; set; }
    public Guid? ManagerPartyId { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTimeOffset? ExpectedUpdatedAtUtc { get; set; }

    // An independent submission; the model holds no mutable descendants.
    public PartyOrganizationAffiliationEditorModel Snapshot() => (PartyOrganizationAffiliationEditorModel)MemberwiseClone();
}

public sealed record PartyOrganizationAffiliationListItemModel(
    Guid Id,
    Guid PersonPartyId,
    string PersonDisplayName,
    Guid OrganizationPartyId,
    string OrganizationDisplayName,
    PartyOrganizationAffiliationKind AffiliationKind,
    bool IsPrimary,
    string JobTitle,
    string EmployeeCode,
    Guid? OrganizationUnitPartyId,
    string OrganizationUnitDisplayName,
    Guid? ManagerPartyId,
    string ManagerDisplayName,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    string Notes,
    string LastChangedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    bool IsCurrent);

// The read side of a person's organization affiliations. Renderers that only list affiliations depend on this port,
// never on the command surface below.
public interface IPartyOrganizationAffiliationReader
{
    Task<IReadOnlyList<PartyOrganizationAffiliationListItemModel>> ListAsync(
        Guid personPartyId,
        CancellationToken cancellationToken = default);
}

public interface IPartyOrganizationAffiliationService : IPartyOrganizationAffiliationReader
{

    Task<Result<IReadOnlyList<PartyOrganizationAffiliationListItemModel>>> ReplaceAsync(
        Guid personPartyId,
        IReadOnlyCollection<PartyOrganizationAffiliationEditorModel> affiliations,
        string actor,
        CancellationToken cancellationToken = default);

    Task<Result<PartyOrganizationAffiliationListItemModel>> UpsertAsync(
        PartyOrganizationAffiliationEditorModel affiliation,
        string actor,
        DateTimeOffset? expectedUpdatedAtUtc = null,
        CancellationToken cancellationToken = default);
}
