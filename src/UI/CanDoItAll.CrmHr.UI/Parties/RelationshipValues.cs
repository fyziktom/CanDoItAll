using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Parties;

public enum RelationshipField { Party, Direction, Kind, Primary, Start, End, Notes }
public readonly record struct RelationshipInput(Guid RowId, RelationshipField Field, bool PendingDate = false);

public static class RelationshipValues {
    public static readonly IEqualityComparer<PartyRelationshipEditorModel> Equality = EqualityComparer<PartyRelationshipEditorModel>.Create(
        (a, b) => a is not null && b is not null && Values(a) == Values(b), row => Values(row).GetHashCode());
    public static PartyRelationshipEditorModel FromOwner(PartyRelationshipListItemModel row) => new() {
        Id = row.Id, RelatedPartyId = row.RelatedPartyId, RelatedPartyDisplayName = row.RelatedPartyDisplayName,
        RelatedPartyType = row.RelatedPartyType, RelationshipKind = row.RelationshipKind, IsOutgoing = row.IsOutgoing,
        IsPrimary = row.IsPrimary, StartDateUtc = row.StartDateUtc, EndDateUtc = row.EndDateUtc, Notes = row.Notes, ExpectedVersion = row.Version
    };
    public static List<PartyRelationshipEditorModel> Snapshot(IEnumerable<PartyRelationshipEditorModel> rows) => rows.Select(row => row.Snapshot()).ToList();
    public static bool Same(IReadOnlyList<PartyRelationshipEditorModel> a, IReadOnlyList<PartyRelationshipEditorModel> b) => PartyEditorValues.SameRows(a, b, row => row.Id, Equality);
    public static void Adopt(List<PartyRelationshipEditorModel> draft, IReadOnlyList<PartyRelationshipEditorModel> values) {
        var existing = draft.ToDictionary(row => row.Id!.Value);
        var result = values.Select(value => {
            if (existing.TryGetValue(value.Id!.Value, out var row)) {
                Copy(row, value);
                return row;
            }
            return value.Snapshot();
        }).ToList();
        draft.Clear();
        draft.AddRange(result);
    }
    public static void Copy(PartyRelationshipEditorModel target, PartyRelationshipEditorModel value) {
        target.Id = value.Id;
        target.RelatedPartyId = value.RelatedPartyId;
        target.RelatedPartyDisplayName = value.RelatedPartyDisplayName;
        target.RelatedPartyType = value.RelatedPartyType;
        target.RelationshipKind = value.RelationshipKind;
        target.IsOutgoing = value.IsOutgoing;
        target.IsPrimary = value.IsPrimary;
        target.StartDateUtc = value.StartDateUtc;
        target.EndDateUtc = value.EndDateUtc;
        target.Notes = value.Notes;
        target.ExpectedVersion = value.ExpectedVersion;
    }
    public static PartyRelationshipEditorModel Rebase(PartyRelationshipEditorModel s, PartyRelationshipEditorModel l,
        PartyRelationshipEditorModel a, IReadOnlySet<RelationshipInput> later) {
        var result = a.Snapshot();
        result.RelatedPartyId = Value(RelationshipField.Party, s.RelatedPartyId, l.RelatedPartyId, a.RelatedPartyId);
        if (result.RelatedPartyId == l.RelatedPartyId && result.RelatedPartyId != a.RelatedPartyId) {
            result.RelatedPartyDisplayName = l.RelatedPartyDisplayName;
            result.RelatedPartyType = l.RelatedPartyType;
        }
        result.IsOutgoing = Value(RelationshipField.Direction, s.IsOutgoing, l.IsOutgoing, a.IsOutgoing);
        result.RelationshipKind = Value(RelationshipField.Kind, s.RelationshipKind, l.RelationshipKind, a.RelationshipKind);
        result.IsPrimary = Value(RelationshipField.Primary, s.IsPrimary, l.IsPrimary, a.IsPrimary);
        result.StartDateUtc = Value(RelationshipField.Start, s.StartDateUtc, l.StartDateUtc, a.StartDateUtc);
        result.EndDateUtc = Value(RelationshipField.End, s.EndDateUtc, l.EndDateUtc, a.EndDateUtc);
        result.Notes = Value(RelationshipField.Notes, s.Notes, l.Notes, a.Notes);
        return result;

        T Value<T>(RelationshipField field, T submitted, T local, T accepted) =>
            later.Contains(new(l.Id!.Value, field)) || !EqualityComparer<T>.Default.Equals(submitted, local) ? local : accepted;
    }
    private static (Guid?, Guid, PartyRelationshipKind, bool, bool, DateTimeOffset?, DateTimeOffset?, string) Values(PartyRelationshipEditorModel row) =>
        (row.Id, row.RelatedPartyId, row.RelationshipKind, row.IsOutgoing, row.IsPrimary, row.StartDateUtc, row.EndDateUtc, row.Notes);
}
