using System.Text.Json;

namespace CanDoItAll.Modules.CrmHr;

internal static class PartyEditorContent {
    public static PartyEditorModel Accepted(CrmHrDbContext context, Guid partyId) => Map(
        context.Set<Party>().Local.Single(party => party.Id == partyId),
        context.Set<PartyRoleAssignment>().Local.Where(row => row.PartyId == partyId).ToList(),
        context.Set<PartyContactPoint>().Local.Where(row => row.PartyId == partyId).ToList(),
        context.Set<PartyAddress>().Local.Where(row => row.PartyId == partyId).ToList(),
        context.Set<PartyConfidentialNote>().Local.Where(row => row.PartyId == partyId).ToList());

    public static PartyEditorModel Map(Party party, IReadOnlyList<PartyRoleAssignment> roles,
        IReadOnlyList<PartyContactPoint> contactPoints, IReadOnlyList<PartyAddress> addresses,
        IReadOnlyList<PartyConfidentialNote> confidentialNotes) {
        return new PartyEditorModel {
            Id = party.Id,
            PartyType = party.PartyType,
            LifecycleStatus = party.LifecycleStatus,
            DisplayName = party.DisplayName,
            LegalName = party.LegalName,
            PreferredName = party.PreferredName,
            ExternalCode = party.ExternalCode,
            Summary = party.Summary,
            Notes = party.Notes,
            Tags = ReadTags(party.TagsJson, party.Id),
            Region = party.Region,
            CountryCode = party.CountryCode,
            TimeZone = party.TimeZone,
            IsSensitive = party.IsSensitive,
            ExtendedDataJson = party.ExtendedDataJson,
            LastChangedBy = party.LastChangedBy,
            UpdatedAtUtc = party.UpdatedAtUtc,
            Roles = roles.Select(role => new PartyRoleAssignmentEditorModel {
                Id = role.Id,
                RoleKind = role.RoleKind,
                Title = role.Title,
                IsPrimary = role.IsPrimary,
                ValidFromUtc = role.ValidFromUtc,
                ValidToUtc = role.ValidToUtc,
                Notes = role.Notes
            }).ToList(),
            ContactPoints = contactPoints.Select(contactPoint => new PartyContactPointEditorModel {
                Id = contactPoint.Id,
                ContactType = contactPoint.ContactType,
                Label = contactPoint.Label,
                Value = contactPoint.Value,
                NormalizedValue = contactPoint.NormalizedValue,
                IsPrimary = contactPoint.IsPrimary,
                IsPublic = contactPoint.IsPublic,
                Tags = ReadTags(contactPoint.TagsJson, party.Id),
                Notes = contactPoint.Notes
            }).ToList(),
            Addresses = addresses.Select(address => new PartyAddressEditorModel {
                Id = address.Id,
                AddressType = address.AddressType,
                Line1 = address.Line1,
                Line2 = address.Line2,
                City = address.City,
                Region = address.Region,
                PostalCode = address.PostalCode,
                CountryCode = address.CountryCode,
                IsPrimary = address.IsPrimary,
                Notes = address.Notes
            }).ToList(),
            ConfidentialNotes = confidentialNotes.Select(note => new PartyConfidentialNoteEditorModel {
                Id = note.Id,
                Category = note.Category,
                NoteText = note.NoteText,
                CreatedBy = note.CreatedBy,
                CreatedAtUtc = note.CreatedAtUtc,
                UpdatedAtUtc = note.UpdatedAtUtc
            }).ToList()
        };
    }

    public static List<string> ReadTags(string json, Guid partyId) {
        try {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        } catch (JsonException exception) {
            throw new InvalidOperationException($"Party '{partyId}' contains invalid tags JSON.", exception);
        }
    }
}
