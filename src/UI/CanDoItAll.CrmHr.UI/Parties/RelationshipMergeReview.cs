using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Parties;

public sealed class RelationshipMergeReview {
    public RelationshipMergeReview(Guid partyId, IReadOnlyList<PartyRelationshipEditorModel> original,
        IReadOnlyList<PartyRelationshipEditorModel> local, IReadOnlyList<PartyRelationshipEditorModel> current) {
        PartyId = partyId;
        Original = RelationshipValues.Snapshot(original);
        Current = RelationshipValues.Snapshot(current);
        Rows = PartyRowComparison.Compare(Original, RelationshipValues.Snapshot(local), Current, row => row.Id, RelationshipValues.Equality, out var whole);
        WholeCollection = whole;
    }
    public Guid PartyId { get; }
    public IReadOnlyList<PartyRelationshipEditorModel> Original { get; }
    public IReadOnlyList<PartyRelationshipEditorModel> Current { get; }
    public IReadOnlyList<KeyedMergeRow<Guid, PartyRelationshipEditorModel>> Rows { get; }
    public MergeField<IReadOnlyList<PartyRelationshipEditorModel>>? WholeCollection { get; }
    public bool IsResolved => WholeCollection?.IsResolved ?? Rows.All(row => row.Field.IsResolved);
    public bool CanApply => IsResolved && GroupError is null;
    public string? GroupError {
        get {
            if (!IsResolved) {
                return null;
            }
            var values = CaptureCandidate();
            if (values.Any(row => Original.Any(before => before.Id == row.Id) && !Current.Any(saved => saved.Id == row.Id))) {
                return "A relationship was removed by another operation. Keep its current absence; add a new relationship separately if needed.";
            }
            return Validate(PartyId, values);
        }
    }
    public List<PartyRelationshipEditorModel> Result() => CanApply ? CaptureCandidate() : throw new InvalidOperationException("Resolve the relationship comparison before applying it.");
    public List<PartyRelationshipEditorModel> CaptureCandidate() {
        var chosen = WholeCollection is { } whole ? (whole.IsResolved ? whole.Result : whole.Local) :
            Rows.Select(row => row.Field.IsResolved ? row.Field.Result : row.Field.Local).Where(entry => entry.Exists).Select(entry => entry.Value!).ToList();
        var current = Current.ToDictionary(row => row.Id!.Value);
        return chosen.Select(value => {
            var row = value.Snapshot();
            row.ExpectedVersion = current.TryGetValue(row.Id!.Value, out var saved) ? saved.ExpectedVersion : 0;
            return row;
        }).ToList();
    }
    public static string? Validate(Guid partyId, IReadOnlyList<PartyRelationshipEditorModel> values) {
        if (partyId == Guid.Empty || values.Any(row => row.Id is null || row.Id == Guid.Empty || row.RelatedPartyId == Guid.Empty || row.RelatedPartyId == partyId || !Enum.IsDefined(row.RelationshipKind)) ||
            values.Select(row => row.Id).Distinct().Count() != values.Count) {
            return "Choose valid relationship identities, endpoints and kinds before continuing.";
        }
        return values.GroupBy(row => (row.RelatedPartyId, row.IsOutgoing, row.RelationshipKind,
            Role: RecruitmentSupportRelationshipSemantics.Role(row.RelationshipKind, row.Notes))).Any(group => group.Skip(1).Any())
                ? "The combined draft contains the same relationship identity more than once." : null;
    }
}
