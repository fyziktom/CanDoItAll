using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Parties;

public sealed class AffiliationMergeReview {
    public AffiliationMergeReview(IReadOnlyList<PartyOrganizationAffiliationEditorModel> original,
        IReadOnlyList<PartyOrganizationAffiliationEditorModel> local, IReadOnlyList<PartyOrganizationAffiliationEditorModel> current, DateOnly today) {
        Today = today;
        Original = AffiliationValues.Snapshot(original);
        Current = AffiliationValues.Snapshot(current);
        Rows = CollectionMerge.ByKey(Original, AffiliationValues.Snapshot(local), Current, row => row.Id!.Value,
            valueComparer: AffiliationValues.Equality, maximumRows: PartyOrganizationAffiliationLimits.MaximumAffiliationsPerPerson);
    }

    public IReadOnlyList<PartyOrganizationAffiliationEditorModel> Original { get; }
    public DateOnly Today { get; }
    public IReadOnlyList<PartyOrganizationAffiliationEditorModel> Current { get; }
    public IReadOnlyList<KeyedMergeRow<Guid, PartyOrganizationAffiliationEditorModel>> Rows { get; }
    public bool IsResolved => Rows.All(row => row.Field.IsResolved);
    public bool CanApply => IsResolved && GroupError is null;
    public string? GroupError {
        get {
            if (!IsResolved) {
                return null;
            }
            var values = CaptureCandidate();
            if (values.Count > PartyOrganizationAffiliationLimits.MaximumAffiliationsPerPerson) {
                return "The combined affiliations exceed the supported limit. Keep or end-date the existing history before adding another row.";
            }
            if (Current.Any(saved => !values.Any(row => row.Id == saved.Id))) {
                return "Saved affiliations cannot be deleted. Keep the saved row and end-date it in your draft.";
            }
            if (values.Any(row => Original.Any(before => before.Id == row.Id) && !Current.Any(saved => saved.Id == row.Id))) {
                return "An affiliation was removed or moved by another operation. Keep its current absence; comparison cannot recreate it.";
            }
            if (values.Count(row => row.IsPrimary) > 1) {
                return "Choose at most one primary affiliation.";
            }
            if (values.Any(row => row.IsPrimary && (row.ValidFrom > Today || row.ValidTo < Today))) {
                return "Only a current affiliation can be primary. Adjust its dates or clear Primary in the merged draft.";
            }
            if (values.Any(row => row.JobTitle.Trim().Length > PartyOrganizationAffiliationLimits.MaximumJobTitleLength ||
                row.EmployeeCode.Trim().Length > PartyOrganizationAffiliationLimits.MaximumEmployeeCodeLength)) {
                return "The merged job title or employee code exceeds its supported length.";
            }
            if (values.Any(row => row.OrganizationPartyId == Guid.Empty || row.PersonPartyId == Guid.Empty || row.OrganizationPartyId == row.PersonPartyId ||
                row.ManagerPartyId == row.PersonPartyId || row.OrganizationUnitPartyId == row.PersonPartyId ||
                row.ValidFrom.HasValue && row.ValidTo.HasValue && row.ValidFrom > row.ValidTo)) {
                return "Choose valid endpoints and effective dates before applying.";
            }
            return values.GroupBy(row => (row.PersonPartyId, row.OrganizationPartyId, row.AffiliationKind, row.ValidFrom, row.ValidTo)).Any(group => group.Skip(1).Any())
                ? "The combined affiliations contain the same business identity more than once." : null;
        }
    }

    public List<PartyOrganizationAffiliationEditorModel> Result() => CanApply ? CaptureCandidate() : throw new InvalidOperationException("Resolve the affiliation comparison before applying it.");

    public List<PartyOrganizationAffiliationEditorModel> CaptureCandidate() {
        var current = Current.ToDictionary(row => row.Id!.Value);
        return Rows.Select(row => row.Field.IsResolved ? row.Field.Result : row.Field.Local).Where(entry => entry.Exists).Select(entry => {
            var value = entry.Value!.Snapshot();
            if (current.TryGetValue(value.Id!.Value, out var saved)) {
                value.ExpectedUpdatedAtUtc = saved.ExpectedUpdatedAtUtc;
                value.IsNew = false;
            }
            return value;
        }).ToList();
    }
}
