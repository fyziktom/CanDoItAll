using CanDoItAll.Modules.CrmHr;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.CrmHr.UI.Parties;

public enum PartyEditableField {
    DisplayName, LegalName, PreferredName, ExternalCode, Summary, Notes, Region, CountryCode, TimeZone,
    ExtendedDataJson, PartyType, LifecycleStatus, IsSensitive, Tags, Roles, Contacts, Addresses, ConfidentialNotes
}

public readonly record struct PartyEditorFieldKey(PartyEditableField Field, Guid RowId = default) {
    public static PartyEditorFieldKey? From(FieldIdentifier field, PartyEditorViewModel draft) {
        if (ReferenceEquals(field.Model, draft)) {
            return field.FieldName switch {
                nameof(PartyEditorViewModel.DisplayName) => new(PartyEditableField.DisplayName),
                nameof(PartyEditorViewModel.LegalName) => new(PartyEditableField.LegalName),
                nameof(PartyEditorViewModel.PreferredName) => new(PartyEditableField.PreferredName),
                nameof(PartyEditorViewModel.ExternalCode) => new(PartyEditableField.ExternalCode),
                nameof(PartyEditorViewModel.Summary) => new(PartyEditableField.Summary),
                nameof(PartyEditorViewModel.Notes) => new(PartyEditableField.Notes),
                nameof(PartyEditorViewModel.Region) => new(PartyEditableField.Region),
                nameof(PartyEditorViewModel.CountryCode) => new(PartyEditableField.CountryCode),
                nameof(PartyEditorViewModel.TimeZone) => new(PartyEditableField.TimeZone),
                nameof(PartyEditorViewModel.ExtendedDataJson) => new(PartyEditableField.ExtendedDataJson),
                nameof(PartyEditorViewModel.PartyType) => new(PartyEditableField.PartyType),
                nameof(PartyEditorViewModel.LifecycleStatus) => new(PartyEditableField.LifecycleStatus),
                nameof(PartyEditorViewModel.IsSensitive) => new(PartyEditableField.IsSensitive),
                nameof(PartyEditorViewModel.PrimaryRoleKindText) => new(PartyEditableField.Roles, draft.PrimaryRoleIdentity),
                nameof(PartyEditorViewModel.PrimaryEmail) => new(PartyEditableField.Contacts, draft.PrimaryEmailIdentity),
                nameof(PartyEditorViewModel.PrimaryPhone) => new(PartyEditableField.Contacts, draft.PrimaryPhoneIdentity),
                _ => null
            };
        }
        return field.Model switch {
            PartyRoleAssignmentEditorModel row => new(PartyEditableField.Roles, row.Id ??= Guid.NewGuid()),
            PartyContactPointEditorModel row => new(PartyEditableField.Contacts, row.Id ??= Guid.NewGuid()),
            PartyAddressEditorModel row => new(PartyEditableField.Addresses, row.Id ??= Guid.NewGuid()),
            PartyConfidentialNoteEditorModel row => new(PartyEditableField.ConfidentialNotes, row.Id ??= Guid.NewGuid()),
            _ => null
        };
    }
}

public readonly record struct PartyEditorInputChange(PartyEditorFieldKey Key, bool PendingDateInput = false);


