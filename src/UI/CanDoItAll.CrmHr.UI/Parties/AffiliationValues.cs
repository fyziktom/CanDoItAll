using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Parties;

public enum AffiliationField { Organization, Kind, Primary, Title, EmployeeCode, Unit, Manager, Start, End, Notes }
public readonly record struct AffiliationInput(Guid RowId, AffiliationField Field, bool PendingDate = false);

public static class AffiliationValues {
    public static readonly IEqualityComparer<PartyOrganizationAffiliationEditorModel> Equality =
        EqualityComparer<PartyOrganizationAffiliationEditorModel>.Create((a, b) => a is not null && b is not null && Values(a) == Values(b), row => Values(row).GetHashCode());

    public static PartyOrganizationAffiliationEditorModel FromOwner(PartyOrganizationAffiliationListItemModel row) => new() {
        Id = row.Id, PersonPartyId = row.PersonPartyId, OrganizationPartyId = row.OrganizationPartyId, AffiliationKind = row.AffiliationKind,
        IsPrimary = row.IsPrimary, JobTitle = row.JobTitle, EmployeeCode = row.EmployeeCode, OrganizationUnitPartyId = row.OrganizationUnitPartyId,
        ManagerPartyId = row.ManagerPartyId, ValidFrom = row.ValidFrom, ValidTo = row.ValidTo, Notes = row.Notes, ExpectedUpdatedAtUtc = row.UpdatedAtUtc
    };

    public static List<PartyOrganizationAffiliationEditorModel> Snapshot(IEnumerable<PartyOrganizationAffiliationEditorModel> rows) => rows.Select(row => row.Snapshot()).ToList();
    public static bool Same(IReadOnlyList<PartyOrganizationAffiliationEditorModel> a, IReadOnlyList<PartyOrganizationAffiliationEditorModel> b) =>
        PartyEditorValues.SameRows(a, b, row => row.Id, Equality);

    public static void Adopt(List<PartyOrganizationAffiliationEditorModel> draft, IReadOnlyList<PartyOrganizationAffiliationEditorModel> values) {
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

    public static void Copy(PartyOrganizationAffiliationEditorModel target, PartyOrganizationAffiliationEditorModel value) {
        target.Id = value.Id;
        target.IsNew = value.IsNew;
        target.PersonPartyId = value.PersonPartyId;
        target.OrganizationPartyId = value.OrganizationPartyId;
        target.AffiliationKind = value.AffiliationKind;
        target.IsPrimary = value.IsPrimary;
        target.JobTitle = value.JobTitle;
        target.EmployeeCode = value.EmployeeCode;
        target.OrganizationUnitPartyId = value.OrganizationUnitPartyId;
        target.ManagerPartyId = value.ManagerPartyId;
        target.ValidFrom = value.ValidFrom;
        target.ValidTo = value.ValidTo;
        target.Notes = value.Notes;
        target.ExpectedUpdatedAtUtc = value.ExpectedUpdatedAtUtc;
    }

    public static PartyOrganizationAffiliationEditorModel Rebase(PartyOrganizationAffiliationEditorModel s, PartyOrganizationAffiliationEditorModel l,
        PartyOrganizationAffiliationEditorModel a, IReadOnlySet<AffiliationInput> later) {
        var result = a.Snapshot();
        result.OrganizationPartyId = Value(AffiliationField.Organization, s.OrganizationPartyId, l.OrganizationPartyId, a.OrganizationPartyId);
        result.AffiliationKind = Value(AffiliationField.Kind, s.AffiliationKind, l.AffiliationKind, a.AffiliationKind);
        result.IsPrimary = Value(AffiliationField.Primary, s.IsPrimary, l.IsPrimary, a.IsPrimary);
        result.JobTitle = Value(AffiliationField.Title, s.JobTitle, l.JobTitle, a.JobTitle);
        result.EmployeeCode = Value(AffiliationField.EmployeeCode, s.EmployeeCode, l.EmployeeCode, a.EmployeeCode);
        result.OrganizationUnitPartyId = Value(AffiliationField.Unit, s.OrganizationUnitPartyId, l.OrganizationUnitPartyId, a.OrganizationUnitPartyId);
        result.ManagerPartyId = Value(AffiliationField.Manager, s.ManagerPartyId, l.ManagerPartyId, a.ManagerPartyId);
        result.ValidFrom = Value(AffiliationField.Start, s.ValidFrom, l.ValidFrom, a.ValidFrom);
        result.ValidTo = Value(AffiliationField.End, s.ValidTo, l.ValidTo, a.ValidTo);
        result.Notes = Value(AffiliationField.Notes, s.Notes, l.Notes, a.Notes);
        return result;

        T Value<T>(AffiliationField field, T submitted, T local, T accepted) =>
            later.Contains(new(l.Id!.Value, field)) || !EqualityComparer<T>.Default.Equals(submitted, local) ? local : accepted;
    }

    private static (Guid?, Guid, Guid, PartyOrganizationAffiliationKind, bool, string, string, Guid?, Guid?, DateOnly?, DateOnly?, string) Values(PartyOrganizationAffiliationEditorModel row) =>
        (row.Id, row.PersonPartyId, row.OrganizationPartyId, row.AffiliationKind, row.IsPrimary, row.JobTitle, row.EmployeeCode,
            row.OrganizationUnitPartyId, row.ManagerPartyId, row.ValidFrom, row.ValidTo, row.Notes);
}
