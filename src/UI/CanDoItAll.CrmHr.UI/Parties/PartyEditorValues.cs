using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Parties;

internal static class PartyEditorValues {
    public static readonly IEqualityComparer<PartyRoleAssignmentEditorModel> Roles = EqualityComparer<PartyRoleAssignmentEditorModel>.Create(
        (a, b) => a is not null && b is not null && Role(a) == Role(b), row => Role(row).GetHashCode());
    public static readonly IEqualityComparer<PartyContactPointEditorModel> Contacts = EqualityComparer<PartyContactPointEditorModel>.Create(
        (a, b) => a is not null && b is not null && Contact(a) == Contact(b) && TagsEqual(a.Tags, b.Tags), row => Contact(row).GetHashCode());
    public static readonly IEqualityComparer<PartyAddressEditorModel> Addresses = EqualityComparer<PartyAddressEditorModel>.Create(
        (a, b) => a is not null && b is not null && Address(a) == Address(b), row => Address(row).GetHashCode());
    public static readonly IEqualityComparer<PartyConfidentialNoteEditorModel> Notes = EqualityComparer<PartyConfidentialNoteEditorModel>.Create(
        (a, b) => a is not null && b is not null && (a.Id, a.Category, a.NoteText) == (b.Id, b.Category, b.NoteText),
        row => (row.Id, row.Category, row.NoteText).GetHashCode());

    public static bool Same(PartyEditorModel a, PartyEditorModel b) =>
        (a.PartyType, a.LifecycleStatus, a.IsSensitive, a.DisplayName, a.LegalName, a.PreferredName, a.ExternalCode,
            a.Summary, a.Notes, a.Region, a.CountryCode, a.TimeZone, a.ExtendedDataJson) ==
        (b.PartyType, b.LifecycleStatus, b.IsSensitive, b.DisplayName, b.LegalName, b.PreferredName, b.ExternalCode,
            b.Summary, b.Notes, b.Region, b.CountryCode, b.TimeZone, b.ExtendedDataJson) &&
        TagsEqual(a.Tags, b.Tags) &&
        SameRows(a.Roles, b.Roles, row => row.Id, Roles) &&
        SameRows(a.ContactPoints, b.ContactPoints, row => row.Id, Contacts) &&
        SameRows(a.Addresses, b.Addresses, row => row.Id, Addresses) &&
        SameRows(a.ConfidentialNotes, b.ConfidentialNotes, row => row.Id, Notes);

    public static PartyEditorModel Rebase(PartyEditorModel submitted, PartyEditorModel local, PartyEditorModel accepted,
        IReadOnlySet<PartyEditorFieldKey> laterInput) {
        var result = accepted.Snapshot();
        result.DisplayName = Later(PartyEditableField.DisplayName, submitted.DisplayName, local.DisplayName, accepted.DisplayName);
        result.LegalName = Later(PartyEditableField.LegalName, submitted.LegalName, local.LegalName, accepted.LegalName);
        result.PreferredName = Later(PartyEditableField.PreferredName, submitted.PreferredName, local.PreferredName, accepted.PreferredName);
        result.ExternalCode = Later(PartyEditableField.ExternalCode, submitted.ExternalCode, local.ExternalCode, accepted.ExternalCode);
        result.Summary = Later(PartyEditableField.Summary, submitted.Summary, local.Summary, accepted.Summary);
        result.Notes = Later(PartyEditableField.Notes, submitted.Notes, local.Notes, accepted.Notes);
        result.Region = Later(PartyEditableField.Region, submitted.Region, local.Region, accepted.Region);
        result.CountryCode = Later(PartyEditableField.CountryCode, submitted.CountryCode, local.CountryCode, accepted.CountryCode);
        result.TimeZone = Later(PartyEditableField.TimeZone, submitted.TimeZone, local.TimeZone, accepted.TimeZone);
        result.ExtendedDataJson = Later(PartyEditableField.ExtendedDataJson, submitted.ExtendedDataJson, local.ExtendedDataJson, accepted.ExtendedDataJson);
        result.PartyType = Later(PartyEditableField.PartyType, submitted.PartyType, local.PartyType, accepted.PartyType);
        result.LifecycleStatus = Later(PartyEditableField.LifecycleStatus, submitted.LifecycleStatus, local.LifecycleStatus, accepted.LifecycleStatus);
        result.IsSensitive = Later(PartyEditableField.IsSensitive, submitted.IsSensitive, local.IsSensitive, accepted.IsSensitive);
        if (laterInput.Contains(new(PartyEditableField.Tags)) || !TagsEqual(submitted.Tags, local.Tags)) {
            result.Tags = local.Tags.ToList();
        }
        result.Roles = Rows(submitted.Roles, local.Roles, result.Roles, row => row.Id, Roles, PartyEditableField.Roles, laterInput);
        result.ContactPoints = Rows(submitted.ContactPoints, local.ContactPoints, result.ContactPoints, row => row.Id, Contacts, PartyEditableField.Contacts, laterInput);
        result.Addresses = Rows(submitted.Addresses, local.Addresses, result.Addresses, row => row.Id, Addresses, PartyEditableField.Addresses, laterInput);
        result.ConfidentialNotes = Rows(submitted.ConfidentialNotes, local.ConfidentialNotes, result.ConfidentialNotes, row => row.Id, Notes, PartyEditableField.ConfidentialNotes, laterInput);
        return result;

        T Later<T>(PartyEditableField field, T s, T l, T a) =>
            laterInput.Contains(new(field)) || !EqualityComparer<T>.Default.Equals(s, l) ? l : a;
    }

    private static List<T> Rows<T>(IReadOnlyList<T> submitted, IReadOnlyList<T> local, List<T> accepted, Func<T, Guid?> key,
        IEqualityComparer<T> equality, PartyEditableField field, IReadOnlySet<PartyEditorFieldKey> laterInput) {
        var original = submitted.ToDictionary(row => key(row)!.Value);
        var remaining = local.ToDictionary(row => key(row)!.Value);
        var result = new List<T>(accepted.Count + local.Count);
        foreach (var acceptedRow in accepted) {
            var id = key(acceptedRow)!.Value;
            if (!remaining.Remove(id, out var localRow)) {
                if (!original.ContainsKey(id)) {
                    result.Add(acceptedRow);
                }
                continue;
            }
            result.Add(laterInput.Contains(new(field, id)) || !original.TryGetValue(id, out var before) || !equality.Equals(before, localRow)
                ? localRow : acceptedRow);
        }
        result.AddRange(remaining.Values.Where(row => !original.TryGetValue(key(row)!.Value, out var before) || !equality.Equals(before, row) ||
            laterInput.Contains(new(field, key(row)!.Value))));
        return result;
    }

    internal static bool SameRows<T>(IReadOnlyList<T> a, IReadOnlyList<T> b, Func<T, Guid?> key, IEqualityComparer<T> equality) {
        if (a.Count != b.Count) {
            return false;
        }
        var rows = new Dictionary<Guid, T>();
        foreach (var row in a) {
            if (key(row) is not Guid id || !rows.TryAdd(id, row)) {
                return false;
            }
        }
        foreach (var row in b) {
            if (key(row) is not Guid id || !rows.Remove(id, out var before) || !equality.Equals(before, row)) {
                return false;
            }
        }
        return rows.Count == 0;
    }

    private static bool TagsEqual(IReadOnlyList<string> a, IReadOnlyList<string> b) => a.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(b);
    private static (Guid?, PartyRoleKind, string, bool, DateTimeOffset?, DateTimeOffset?, string) Role(PartyRoleAssignmentEditorModel row) =>
        (row.Id, row.RoleKind, row.Title, row.IsPrimary, row.ValidFromUtc, row.ValidToUtc, row.Notes);
    private static (Guid?, PartyContactType, string, string, bool, bool, string) Contact(PartyContactPointEditorModel row) =>
        (row.Id, row.ContactType, row.Label, row.Value, row.IsPrimary, row.IsPublic, row.Notes);
    private static (Guid?, string, string, string, string, string, string, string, bool, string) Address(PartyAddressEditorModel row) =>
        (row.Id, row.AddressType, row.Line1, row.Line2, row.City, row.Region, row.PostalCode, row.CountryCode, row.IsPrimary, row.Notes);
}
