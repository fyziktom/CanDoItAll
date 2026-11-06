using System.ComponentModel;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.CrmHr;

public enum PartyCsvExportScope
{
    SelectedParties,
    EntireDirectory
}

public sealed class PartyRelationshipEditorModel
{
    public Guid? Id { get; set; }

    public Guid RelatedPartyId { get; set; }

    public string RelatedPartyDisplayName { get; set; } = string.Empty;

    public PartyType? RelatedPartyType { get; set; }

    public PartyRelationshipKind RelationshipKind { get; set; } = PartyRelationshipKind.MemberOf;

    public bool IsOutgoing { get; set; } = true;

    public bool IsPrimary { get; set; }

    public DateTimeOffset? StartDateUtc { get; set; }

    public DateTimeOffset? EndDateUtc { get; set; }

    public string Notes { get; set; } = string.Empty;
    public uint ExpectedVersion { get; set; }

    // An independent submission; the model holds no mutable descendants.
    public PartyRelationshipEditorModel Snapshot() => (PartyRelationshipEditorModel)MemberwiseClone();
}

/// <summary>
/// A relationship of a party, seen from the party whose relationships were requested with
/// <c>GET /api/crm-hr/parties/{partyId}/relationships</c>. Copy its fields into a relationship replacement to keep it.
/// </summary>
/// <param name="Id">
/// Identifier of the relationship record. The same relationship listed for the other party has the same identifier.
/// </param>
/// <param name="RelatedPartyId">Identifier of the other party of the relationship.</param>
/// <param name="RelatedPartyDisplayName">Display name of the other party, also when that party is sensitive.</param>
/// <param name="RelatedPartyType">
/// Kind of the other party, as a JSON integer: 0 Person, 1 Organization, 2 OrganizationUnit, 3 AiAgent.
/// </param>
/// <param name="RelationshipKind">
/// Kind of relationship, read as source, kind, target, as a JSON integer: 0 MemberOf, 1 PartOf, 2 ReportsTo,
/// 3 CustomerOf, 4 PartnerOf, 5 VendorTo, 6 Represents, 7 ManagedBy, 8 OwnedBy, 9 Supports.
/// </param>
/// <param name="IsOutgoing">
/// True when the requested party is the source (for MemberOf: the requested party is a member of the related party);
/// false when the related party is the source.
/// </param>
/// <param name="IsPrimary">True when the relationship is marked as primary.</param>
/// <param name="StartDateUtc">Start of the relationship in UTC; null when no start was stated.</param>
/// <param name="EndDateUtc">End of the relationship in UTC; null when no end was stated.</param>
/// <param name="Notes">
/// Free-text notes. A Supports relationship whose notes are exactly <c>Buddy</c> or <c>Mentor</c> is a recruiting
/// buddy or mentor assignment.
/// </param>
public sealed record PartyRelationshipListItemModel(
    Guid Id,
    Guid RelatedPartyId,
    string RelatedPartyDisplayName,
    PartyType RelatedPartyType,
    PartyRelationshipKind RelationshipKind,
    bool IsOutgoing,
    bool IsPrimary,
    DateTimeOffset? StartDateUtc,
    DateTimeOffset? EndDateUtc,
    string Notes) {
    [Description("Owner-issued unsigned relationship row version. Retain it with this row ID and related party ID in Original.Rows; do not calculate or increment it.")]
    public uint Version { get; init; }
}

[Description("Owner-issued identity and version of one relationship in a complete relationship-set observation. These values are preconditions, not authorization grants.")]
public readonly record struct PartyRelationshipVersion(
    [property: Description("GUID of the observed relationship row.")] Guid Id,
    [property: Description("GUID of the other party in the observed relationship; retain it with the row version.")] Guid RelatedPartyId,
    [property: Description("Owner-issued unsigned row version used to detect changes to this exact relationship; send unchanged.")] uint Version);

[Description("Complete owner-read relationship-set basis for a conditional replacement. It includes the identities and versions of all observed rows; do not build it from a filtered subset.")]
public sealed record PartyRelationshipObservation(
    [property: Description("Complete original relationship identities and versions. An empty array records an originally empty set, rather than permission to overwrite a populated set.")] IReadOnlyList<PartyRelationshipVersion> Rows) {
    public PartyRelationshipObservation Snapshot() => new(Rows.ToArray());
    public static PartyRelationshipObservation FromRows(IEnumerable<PartyRelationshipListItemModel> rows) =>
        new(rows.Select(row => new PartyRelationshipVersion(row.Id, row.RelatedPartyId, row.Version)).ToArray());
}

[Description("Exact accepted relationship replacement returned by the native owner, including the canonical rows and their new versions.")]
public sealed record PartyRelationshipSaveReceipt(
    [property: Description("GUID of the party whose relationship set was conditionally replaced.")] Guid PartyId,
    [property: Description("Complete accepted relationship rows with owner-issued versions for the next observation.")] IReadOnlyList<PartyRelationshipListItemModel> Accepted);

public interface IPartyRelationshipService {
    Task<IReadOnlyList<PartyRelationshipListItemModel>> ListAsync(Guid partyId, CancellationToken cancellationToken = default, int? maximumRows = null);
    Task<Result<PartyRelationshipSaveReceipt>> ReplaceAsync(Guid partyId, IReadOnlyList<PartyRelationshipEditorModel> relationships,
        PartyRelationshipObservation? original, string actor, CancellationToken cancellationToken = default,
        Action<PartyRelationshipSaveReceipt>? onCommitted = null);
}

public sealed class RelationshipReadLimitExceededException(int maximumRows) : InvalidOperationException("The relationship set exceeds this automatic read budget. Request a complete comparison explicitly.") {
    public int MaximumRows { get; } = maximumRows;
}

public enum RecruitmentSupportRole { None, Manager, Buddy, Mentor }

public static class RecruitmentSupportRelationshipSemantics {
    public const string Buddy = "Buddy";
    public const string Mentor = "Mentor";
    public const string Manager = "Recruiting support manager";
    public static RecruitmentSupportRole Role(PartyRelationshipKind kind, string notes) => kind switch {
        PartyRelationshipKind.ManagedBy => RecruitmentSupportRole.Manager,
        PartyRelationshipKind.Supports when notes.Trim() == Buddy => RecruitmentSupportRole.Buddy,
        PartyRelationshipKind.Supports when notes.Trim() == Mentor => RecruitmentSupportRole.Mentor,
        _ => RecruitmentSupportRole.None
    };
}

public sealed record PartyDuplicateCandidateModel(
    Guid Id,
    string DisplayName,
    PartyType PartyType,
    PartyLifecycleStatus LifecycleStatus,
    string Summary,
    IReadOnlyList<string> MatchReasons);

public sealed class PartyCsvImportPreviewRowModel
{
    public int RowNumber { get; init; }

    public PartyEditorModel Party { get; init; } = new();

    public IReadOnlyList<string> Messages { get; init; } = [];

    public IReadOnlyList<PartyDuplicateCandidateModel> DuplicateCandidates { get; init; } = [];

    public bool CanImport { get; init; }
}

public sealed class PartyCsvImportPreviewModel
{
    public IReadOnlyList<PartyCsvImportPreviewRowModel> Rows { get; init; } = [];

    public int ReadyRowCount => Rows.Count(row => row.CanImport);

    public int BlockingRowCount => Rows.Count(row => !row.CanImport);
}

public sealed record PartyMergeSummaryModel(
    Guid RetainedPartyId,
    Guid MergedPartyId,
    string Summary);
