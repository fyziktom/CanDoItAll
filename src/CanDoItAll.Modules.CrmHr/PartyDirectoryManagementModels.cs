namespace CanDoItAll.Modules.CrmHr;

public sealed class PartyRelationshipEditorModel
{
    public Guid? Id { get; set; }

    public Guid RelatedPartyId { get; set; }

    public PartyRelationshipKind RelationshipKind { get; set; } = PartyRelationshipKind.MemberOf;

    public bool IsOutgoing { get; set; } = true;

    public bool IsPrimary { get; set; }

    public DateTimeOffset? StartDateUtc { get; set; }

    public DateTimeOffset? EndDateUtc { get; set; }

    public string Notes { get; set; } = string.Empty;
}

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
    string Notes);

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
