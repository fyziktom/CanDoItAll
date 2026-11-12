using System.Text.Json;
using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Parties;

public sealed class PartyMergeReview {
    public const int DetailedRowLimit = PartyRowComparison.DetailedLimit;
    public PartyMergeReview(PartyEditorModel original, PartyEditorModel local, PartyEditorModel current) {
        Original = original.Snapshot();
        Local = local.Snapshot();
        Current = current.Snapshot();
        PartyType = new(original.PartyType, local.PartyType, current.PartyType);
        LifecycleStatus = new(original.LifecycleStatus, local.LifecycleStatus, current.LifecycleStatus);
        IsSensitive = new(original.IsSensitive, local.IsSensitive, current.IsSensitive);
        DisplayName = new(original.DisplayName, local.DisplayName, current.DisplayName);
        LegalName = new(original.LegalName, local.LegalName, current.LegalName);
        PreferredName = new(original.PreferredName, local.PreferredName, current.PreferredName);
        ExternalCode = new(original.ExternalCode, local.ExternalCode, current.ExternalCode);
        Summary = TextMerge.Compare(original.Summary, local.Summary, current.Summary).Field;
        Notes = TextMerge.Compare(original.Notes, local.Notes, current.Notes).Field;
        Region = new(original.Region, local.Region, current.Region);
        CountryCode = new(original.CountryCode, local.CountryCode, current.CountryCode);
        TimeZone = new(original.TimeZone, local.TimeZone, current.TimeZone);
        ExtendedDataJson = new(original.ExtendedDataJson, local.ExtendedDataJson, current.ExtendedDataJson);
        Strings = [("Display name", DisplayName, false), ("Legal name", LegalName, false), ("Preferred name", PreferredName, false),
            ("External code", ExternalCode, false), ("Summary", Summary, true), ("Notes", Notes, true), ("Region", Region, false),
            ("Country code", CountryCode, false), ("Time zone", TimeZone, false), ("Extended data JSON", ExtendedDataJson, true)];
        Tags = new(original.Tags, local.Tags, current.Tags, new TagEquality());
        Tags.SetResult(CollectionMerge.Membership(original.Tags, local.Tags, current.Tags, StringComparer.OrdinalIgnoreCase));
        Roles = PartyRowComparison.Compare(Original.Roles, Local.Roles, Current.Roles, row => row.Id, PartyEditorValues.Roles, out var largeRoles);
        LargeRoles = largeRoles;
        Contacts = PartyRowComparison.Compare(Original.ContactPoints, Local.ContactPoints, Current.ContactPoints, row => row.Id, PartyEditorValues.Contacts, out var largeContacts);
        LargeContacts = largeContacts;
        Addresses = PartyRowComparison.Compare(Original.Addresses, Local.Addresses, Current.Addresses, row => row.Id, PartyEditorValues.Addresses, out var largeAddresses);
        LargeAddresses = largeAddresses;
        ConfidentialNotes = PartyRowComparison.Compare(Original.ConfidentialNotes, Local.ConfidentialNotes, Current.ConfidentialNotes, row => row.Id, PartyEditorValues.Notes, out var largeNotes);
        LargeConfidentialNotes = largeNotes;
    }

    public PartyEditorModel Original { get; }
    public PartyEditorModel Local { get; }
    public PartyEditorModel Current { get; }
    public MergeField<PartyType> PartyType { get; }
    public MergeField<PartyLifecycleStatus> LifecycleStatus { get; }
    public MergeField<bool> IsSensitive { get; }
    public MergeField<string> DisplayName { get; }
    public MergeField<string> LegalName { get; }
    public MergeField<string> PreferredName { get; }
    public MergeField<string> ExternalCode { get; }
    public MergeField<string> Summary { get; }
    public MergeField<string> Notes { get; }
    public MergeField<string> Region { get; }
    public MergeField<string> CountryCode { get; }
    public MergeField<string> TimeZone { get; }
    public MergeField<string> ExtendedDataJson { get; }
    public MergeField<IReadOnlyList<string>> Tags { get; }
    public IReadOnlyList<(string Label, MergeField<string> Field, bool Multiline)> Strings { get; }
    public IReadOnlyList<KeyedMergeRow<Guid, PartyRoleAssignmentEditorModel>> Roles { get; }
    public IReadOnlyList<KeyedMergeRow<Guid, PartyContactPointEditorModel>> Contacts { get; }
    public IReadOnlyList<KeyedMergeRow<Guid, PartyAddressEditorModel>> Addresses { get; }
    public IReadOnlyList<KeyedMergeRow<Guid, PartyConfidentialNoteEditorModel>> ConfidentialNotes { get; }
    public MergeField<IReadOnlyList<PartyRoleAssignmentEditorModel>>? LargeRoles { get; }
    public MergeField<IReadOnlyList<PartyContactPointEditorModel>>? LargeContacts { get; }
    public MergeField<IReadOnlyList<PartyAddressEditorModel>>? LargeAddresses { get; }
    public MergeField<IReadOnlyList<PartyConfidentialNoteEditorModel>>? LargeConfidentialNotes { get; }
    public bool IsResolved => Strings.All(row => row.Field.IsResolved) && PartyType.IsResolved && LifecycleStatus.IsResolved &&
        IsSensitive.IsResolved && Tags.IsResolved && Resolved(Roles, LargeRoles) && Resolved(Contacts, LargeContacts) &&
        Resolved(Addresses, LargeAddresses) && Resolved(ConfidentialNotes, LargeConfidentialNotes);
    public bool CanApply => IsResolved && GroupError is null;

    public string? GroupError {
        get {
            if (!IsResolved) {
                return null;
            }
            if (string.IsNullOrWhiteSpace(DisplayName.Result)) {
                return "Display name is required.";
            }
            if (!IsSensitive.Result && Values(ConfidentialNotes, LargeConfidentialNotes).Any(row => !string.IsNullOrWhiteSpace(row.NoteText))) {
                return "Keep the party sensitive while confidential notes remain.";
            }
            if (Values(Contacts, LargeContacts).Where(row => row.IsPrimary && !string.IsNullOrWhiteSpace(row.Value)).GroupBy(row => row.ContactType).Any(group => group.Skip(1).Any())) {
                return "Choose at most one primary contact for each contact type.";
            }
            if (Values(Roles, LargeRoles).Count(row => row.IsPrimary) > 1) {
                return "Choose at most one primary role.";
            }
            try {
                using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(ExtendedDataJson.Result) ? "{}" : ExtendedDataJson.Result);
                return null;
            } catch (JsonException) {
                return "Extended data must be valid JSON.";
            }
        }
    }

    public PartyEditorModel Result() => CanApply ? CaptureCandidate() : throw new InvalidOperationException("Resolve the party comparison before applying it.");

    public PartyEditorModel CaptureCandidate() => new() {
        Id = Current.Id,
        UpdatedAtUtc = Current.UpdatedAtUtc,
        LastChangedBy = Current.LastChangedBy,
        PartyType = Chosen(PartyType),
        LifecycleStatus = Chosen(LifecycleStatus),
        IsSensitive = Chosen(IsSensitive),
        DisplayName = Chosen(DisplayName),
        LegalName = Chosen(LegalName),
        PreferredName = Chosen(PreferredName),
        ExternalCode = Chosen(ExternalCode),
        Summary = Chosen(Summary),
        Notes = Chosen(Notes),
        Region = Chosen(Region),
        CountryCode = Chosen(CountryCode),
        TimeZone = Chosen(TimeZone),
        ExtendedDataJson = Chosen(ExtendedDataJson),
        Tags = Chosen(Tags).ToList(),
        Roles = Values(Roles, LargeRoles).Select(row => row.Snapshot()).ToList(),
        ContactPoints = Values(Contacts, LargeContacts).Select(row => row.Snapshot()).ToList(),
        Addresses = Values(Addresses, LargeAddresses).Select(row => row.Snapshot()).ToList(),
        ConfidentialNotes = Values(ConfidentialNotes, LargeConfidentialNotes).Select(row => row.Snapshot()).ToList()
    };

    public static bool SameValues(PartyEditorModel left, PartyEditorModel right) => PartyEditorValues.Same(left, right);

    private static T Chosen<T>(MergeField<T> field) => field.IsResolved ? field.Result : field.Local;
    private static bool Resolved<T>(IReadOnlyList<KeyedMergeRow<Guid, T>> rows, MergeField<IReadOnlyList<T>>? large) =>
        large?.IsResolved ?? rows.All(row => row.Field.IsResolved);
    private static IEnumerable<T> Values<T>(IReadOnlyList<KeyedMergeRow<Guid, T>> rows, MergeField<IReadOnlyList<T>>? large) => large is not null
        ? Chosen(large) : rows.Select(row => Chosen(row.Field)).Where(entry => entry.Exists).Select(entry => entry.Value!);

    private sealed class TagEquality : IEqualityComparer<IReadOnlyList<string>> {
        public bool Equals(IReadOnlyList<string>? a, IReadOnlyList<string>? b) => a is not null && b is not null &&
            a.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(b);
        public int GetHashCode(IReadOnlyList<string> value) => value.Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }
}
