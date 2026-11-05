using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Editing;

namespace CanDoItAll.CrmHr.UI.Crm;

public enum ConnectionField { Party, Role, Primary, Notes, Projects }
public readonly record struct ConnectionInput(Guid RowId, ConnectionField Field);
public sealed record ConnectionFieldInput(CrmAccountConnectionEditorModel Row, ConnectionField Field);
public sealed record ConnectionProjectSelection(CrmAccountConnectionEditorModel Row, IReadOnlyList<Guid> ProjectIds);

public static class ConnectionValues {
    public static readonly IEqualityComparer<CrmAccountConnectionEditorModel> Equality = EqualityComparer<CrmAccountConnectionEditorModel>.Create(
        (a, b) => a is not null && b is not null && Scalar(a) == Scalar(b) && SameProjects(a, b), row => HashCode.Combine(Scalar(row), row.ProjectIds.Distinct().Count()));

    public static CrmAccountConnectionEditorModel FromOwner(CrmAccountConnectedRecordItemModel row) => new() {
        Id = row.Id, RelatedPartyId = row.RelatedPartyId, Role = row.Role, IsPrimary = row.IsPrimary, Notes = row.Notes,
        ProjectIds = row.Projects.Select(project => project.Id).ToList(),
        ProjectAdmissions = row.Projects.Where(project => project.Admission is not null).ToDictionary(project => project.Id, project => project.Admission!)
    };

    public static List<CrmAccountConnectionEditorModel> Snapshot(IEnumerable<CrmAccountConnectionEditorModel> rows) => rows.Select(row => row.Snapshot()).ToList();
    public static bool Same(IReadOnlyList<CrmAccountConnectionEditorModel> a, IReadOnlyList<CrmAccountConnectionEditorModel> b) {
        if (a.Count != b.Count) {
            return false;
        }
        var rows = new Dictionary<Guid, CrmAccountConnectionEditorModel>();
        foreach (var row in b) {
            if (row.Id is not Guid id || !rows.TryAdd(id, row)) {
                return false;
            }
        }
        return a.All(row => row.Id is Guid id && rows.TryGetValue(id, out var other) && Equality.Equals(row, other));
    }

    public static void Adopt(List<CrmAccountConnectionEditorModel> draft, IReadOnlyList<CrmAccountConnectionEditorModel> values) {
        var byId = draft.Where(row => row.Id.HasValue).ToDictionary(row => row.Id!.Value);
        var desired = values.Select(value => {
            if (value.Id is Guid id && byId.TryGetValue(id, out var row)) {
                Copy(row, value);
                return row;
            }
            return value.Snapshot();
        }).ToArray();
        draft.Clear();
        draft.AddRange(desired);
    }

    public static void Copy(CrmAccountConnectionEditorModel target, CrmAccountConnectionEditorModel source) {
        target.Id = source.Id;
        target.RelatedPartyId = source.RelatedPartyId;
        target.Role = source.Role;
        target.IsPrimary = source.IsPrimary;
        target.Notes = source.Notes;
        target.ProjectIds = source.ProjectIds.ToList();
        target.ProjectAdmissions = new(source.ProjectAdmissions);
    }

    public static CrmAccountConnectionEditorModel Rebase(CrmAccountConnectionEditorModel submitted,
        CrmAccountConnectionEditorModel local, CrmAccountConnectionEditorModel accepted, IReadOnlySet<ConnectionInput> later) {
        var result = accepted.Snapshot();
        result.RelatedPartyId = Value(ConnectionField.Party, submitted.RelatedPartyId, local.RelatedPartyId, accepted.RelatedPartyId);
        result.Role = Value(ConnectionField.Role, submitted.Role, local.Role, accepted.Role);
        result.IsPrimary = Value(ConnectionField.Primary, submitted.IsPrimary, local.IsPrimary, accepted.IsPrimary);
        result.Notes = Value(ConnectionField.Notes, submitted.Notes, local.Notes, accepted.Notes);
        if (later.Contains(new(local.Id!.Value, ConnectionField.Projects)) || !SameProjects(submitted, local)) {
            result.ProjectIds = local.ProjectIds.ToList();
            result.ProjectAdmissions = new(local.ProjectAdmissions);
        }
        return result;

        T Value<T>(ConnectionField field, T sent, T live, T stored) => later.Contains(new(local.Id!.Value, field)) ||
            !EqualityComparer<T>.Default.Equals(sent, live) ? live : stored;
    }

    public static bool SameProjects(CrmAccountConnectionEditorModel a, CrmAccountConnectionEditorModel b) =>
        a.ProjectIds.ToHashSet().SetEquals(b.ProjectIds) && a.ProjectIds.All(id => a.ProjectAdmissions.GetValueOrDefault(id) == b.ProjectAdmissions.GetValueOrDefault(id));

    internal static void PreserveChoice<T>(MergeField<T> old, MergeField<T> current) {
        switch (old.Choice) {
            case MergeChoice.Local:
                current.UseLocal();
                break;
            case MergeChoice.Current:
                current.UseCurrent();
                break;
            case MergeChoice.Custom:
                current.SetResult(old.Result);
                break;
        }
    }

    private static (Guid?, Guid, CrmAccountConnectionRole, bool, string) Scalar(CrmAccountConnectionEditorModel row) =>
        (row.Id, row.RelatedPartyId, row.Role, row.IsPrimary, row.Notes);
}
