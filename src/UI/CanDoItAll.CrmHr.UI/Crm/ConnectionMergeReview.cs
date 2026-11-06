using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Crm;

public sealed class ConnectionMergeReview {
    public ConnectionMergeReview(Guid accountPartyId, IReadOnlyList<CrmAccountConnectionEditorModel> original,
        IReadOnlyList<CrmAccountConnectionEditorModel> local, CrmAccountWorkspaceModel current, ConnectionMergeReview? previous) {
        if (current.AccountPartyId != accountPartyId || current.ConnectionsOriginal is null) {
            throw new ArgumentException("The comparison must identify the account's complete current connection set.", nameof(current));
        }
        AccountPartyId = accountPartyId;
        CurrentOriginal = current.ConnectionsOriginal.Snapshot();
        Current = current.ConnectedRecords.Select(ConnectionValues.FromOwner).ToArray();
        Original = ConnectionValues.Snapshot(original);
        Rows = PartyRowComparison.Compare(Original, ConnectionValues.Snapshot(local), Current, row => row.Id,
            ConnectionValues.Equality, out var whole);
        WholeCollection = whole;
        foreach (var row in Rows) {
            if (row.Field.Original is { Exists: true, Value: { } b } && row.Field.Local is { Exists: true, Value: { } l } &&
                row.Field.Current is { Exists: true, Value: { } r }) {
                Fields.Add(row.Key, new(b, l, r, previous?.Fields.GetValueOrDefault(row.Key)));
            } else if (previous?.Rows.SingleOrDefault(old => old.Key == row.Key) is { } old) {
                ConnectionValues.PreserveChoice(old.Field, row.Field);
            }
        }
        if (WholeCollection is not null && previous?.WholeCollection is { } prior) {
            ConnectionValues.PreserveChoice(prior, WholeCollection);
        }
    }

    public Guid AccountPartyId { get; }
    public CrmAccountConnectionsObservation CurrentOriginal { get; }
    public IReadOnlyList<CrmAccountConnectionEditorModel> Original { get; }
    public IReadOnlyList<CrmAccountConnectionEditorModel> Current { get; }
    public IReadOnlyList<KeyedMergeRow<Guid, CrmAccountConnectionEditorModel>> Rows { get; }
    public Dictionary<Guid, ConnectionRowReview> Fields { get; } = [];
    public MergeField<IReadOnlyList<CrmAccountConnectionEditorModel>>? WholeCollection { get; }
    public bool IsResolved => WholeCollection?.IsResolved ?? Rows.All(row => Fields.TryGetValue(row.Key, out var fields) ? fields.IsResolved : row.Field.IsResolved);
    public string? GroupError => IsResolved ? Validate(AccountPartyId, Candidate(), Original, Current) : null;
    public bool CanApply => IsResolved && GroupError is null;

    public List<CrmAccountConnectionEditorModel> Candidate() {
        if (WholeCollection is { } whole) {
            return ConnectionValues.Snapshot(whole.IsResolved ? whole.Result : whole.Local);
        }
        return Rows.Select(row => Fields.TryGetValue(row.Key, out var fields) ? MergeEntry<CrmAccountConnectionEditorModel>.Present(fields.Candidate()) :
            row.Field.IsResolved ? row.Field.Result : row.Field.Local).Where(entry => entry.Exists).Select(entry => entry.Value!.Snapshot()).ToList();
    }

    public static string? Validate(Guid accountPartyId, IReadOnlyList<CrmAccountConnectionEditorModel> values,
        IReadOnlyList<CrmAccountConnectionEditorModel> original, IReadOnlyList<CrmAccountConnectionEditorModel> current) {
        if (values.Any(row => row.Id is null || row.Id == Guid.Empty || row.RelatedPartyId == Guid.Empty || row.RelatedPartyId == accountPartyId || !Enum.IsDefined(row.Role)) ||
            values.Select(row => row.Id).Distinct().Count() != values.Count) {
            return "Choose distinct connection identities, another directory record and a valid relationship role.";
        }
        if (values.GroupBy(row => (row.RelatedPartyId, row.Role)).Any(group => group.Skip(1).Any())) {
            return "The combined draft contains the same directory record and role more than once.";
        }
        var originalIds = original.Select(row => row.Id).ToHashSet();
        var currentRows = current.ToDictionary(row => row.Id!.Value);
        if (values.Any(row => originalIds.Contains(row.Id) && !currentRows.ContainsKey(row.Id!.Value))) {
            return "A saved connection was removed. Keep its current absence and add a separate new connection if needed.";
        }
        foreach (var row in values) {
            var saved = currentRows.GetValueOrDefault(row.Id!.Value);
            if (row.ProjectIds.Any(id => !row.ProjectAdmissions.ContainsKey(id) ||
                    saved?.ProjectAdmissions.TryGetValue(id, out var binding) == true && binding != row.ProjectAdmissions[id])) {
                return "A selected project's original lifetime is unavailable or changed. Remove and explicitly reselect its current record.";
            }
        }
        return null;
    }

}

public sealed class ConnectionRowReview {
    private readonly CrmAccountConnectionEditorModel local;
    private readonly CrmAccountConnectionEditorModel current;
    public ConnectionRowReview(CrmAccountConnectionEditorModel original, CrmAccountConnectionEditorModel local,
        CrmAccountConnectionEditorModel current, ConnectionRowReview? previous) {
        this.local = local.Snapshot();
        this.current = current.Snapshot();
        OriginalProjects = original.ProjectIds.ToArray();
        Party = new(original.RelatedPartyId, local.RelatedPartyId, current.RelatedPartyId);
        Role = new(original.Role, local.Role, current.Role);
        Primary = new(original.IsPrimary, local.IsPrimary, current.IsPrimary);
        Notes = TextMerge.Compare(original.Notes, local.Notes, current.Notes).Field;
        Projects = CollectionMerge.Membership(original.ProjectIds, local.ProjectIds, current.ProjectIds);
        if (previous is not null) {
            ConnectionValues.PreserveChoice(previous.Party, Party);
            ConnectionValues.PreserveChoice(previous.Role, Role);
            ConnectionValues.PreserveChoice(previous.Primary, Primary);
            ConnectionValues.PreserveChoice(previous.Notes, Notes);
        }
    }
    public MergeField<Guid> Party { get; }
    public MergeField<CrmAccountConnectionRole> Role { get; }
    public MergeField<bool> Primary { get; }
    public MergeField<string> Notes { get; }
    public IReadOnlyList<Guid> Projects { get; }
    public IReadOnlyList<Guid> OriginalProjects { get; }
    public IReadOnlyList<Guid> LocalProjects => local.ProjectIds;
    public IReadOnlyList<Guid> CurrentProjects => current.ProjectIds;
    public bool IsResolved => Party.IsResolved && Role.IsResolved && Primary.IsResolved && Notes.IsResolved;

    public CrmAccountConnectionEditorModel Candidate() => new() {
        Id = local.Id, RelatedPartyId = Chosen(Party), Role = Chosen(Role), IsPrimary = Chosen(Primary), Notes = Chosen(Notes),
        ProjectIds = Projects.ToList(), ProjectAdmissions = Projects.Select(id => (Id: id,
            Binding: local.ProjectAdmissions.GetValueOrDefault(id) ?? current.ProjectAdmissions.GetValueOrDefault(id)))
            .Where(row => row.Binding is not null).ToDictionary(row => row.Id, row => row.Binding!)
    };

    private static T Chosen<T>(MergeField<T> field) => field.IsResolved ? field.Result : field.Local;
}
