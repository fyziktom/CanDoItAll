using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AppComponents;
using CanDoItAll.CrmHr.UI.Activity;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.CrmHr.UI.Pickers;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.CrmHr.UI.Parties;

// Presentation helpers of the Parties workspace: labels, tones, option lists and projections its surface renders.
public static class CrmHrDirectoryWorkspacePresentation
{
    public static readonly IReadOnlyList<string> ConfidentialNoteCategories = PartyConfidentialNoteCategories.All;

    public static string FormatAffiliation(
        PartyOrganizationAffiliationListItemModel affiliation)
    {
        return string.IsNullOrWhiteSpace(affiliation.JobTitle)
            ? affiliation.OrganizationDisplayName
            : $"{affiliation.OrganizationDisplayName} / {affiliation.JobTitle}";
    }

    public static string BuildAssignmentSchedule(PartyProjectAssignmentItemModel assignment)
    {
        var allocation = assignment.AllocationPercent.HasValue
            ? $"{assignment.AllocationPercent.Value:0.##}% allocation"
            : "No allocation set";
        var window = assignment.StartsOn.HasValue || assignment.EndsOn.HasValue
            ? $"{assignment.StartsOn?.ToString("d") ?? "Open"} -> {assignment.EndsOn?.ToString("d") ?? "Open"}"
            : "No schedule window";
        return $"{allocation} / {window}";
    }

    public static string ResolveStatusTone(PartyLifecycleStatus lifecycleStatus)
    {
        return lifecycleStatus switch
        {
            PartyLifecycleStatus.Active => "success",
            PartyLifecycleStatus.Candidate or PartyLifecycleStatus.Prospect => "info",
            PartyLifecycleStatus.Former or PartyLifecycleStatus.Inactive => "warning",
            PartyLifecycleStatus.Archived => "neutral",
            _ => "neutral"
        };
    }

    public static string FormatActor(string actor)
    {
        return string.IsNullOrWhiteSpace(actor) ? "Unknown" : actor;
    }

    public static string FormatMoment(DateTimeOffset value)
    {
        if (value == default)
        {
            return "not saved yet";
        }

        return value.LocalDateTime.ToString("g");
    }
}

public sealed class PartyEditorViewModel
{
    public Guid? Id { get; set; }

    public PartyType PartyType { get; set; } = PartyType.Person;

    public PartyLifecycleStatus LifecycleStatus { get; set; } = PartyLifecycleStatus.Draft;

    public string DisplayName { get; set; } = string.Empty;

    public string LegalName { get; set; } = string.Empty;

    public string PreferredName { get; set; } = string.Empty;

    public string ExternalCode { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public IReadOnlyList<string> Tags { get; set; } = [];

    public string Region { get; set; } = string.Empty;

    public string CountryCode { get; set; } = string.Empty;

    public string TimeZone { get; set; } = string.Empty;

    public bool IsSensitive { get; set; }

    public string ExtendedDataJson { get; set; } = "{}";

    public string LastChangedBy { get; set; } = "crm-hr-ui";

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string PrimaryRoleKindText { get; set; } = string.Empty;

    public string PrimaryEmail { get; set; } = string.Empty;

    public string PrimaryPhone { get; set; } = string.Empty;

    private PartyContactPointEditorModel? PrimaryEmailContactPoint { get; set; }

    private PartyContactPointEditorModel? PrimaryPhoneContactPoint { get; set; }

    public List<PartyRoleAssignmentEditorModel> AdditionalRoles { get; set; } = [];

    public List<PartyContactPointEditorModel> AdditionalContactPoints { get; set; } = [];

    public List<PartyAddressEditorModel> Addresses { get; set; } = [];

    public List<PartyConfidentialNoteEditorModel> ConfidentialNotes { get; set; } = [];

    public static PartyEditorViewModel CreateNew()
    {
        return new PartyEditorViewModel();
    }

    // Owner-assigned state the form never edits: the primary contact points the next save updates instead of
    // recreating. A host that keeps this draft after its own commit (the operator typed on while the write was in
    // flight) takes them from the owner's accepted record, so the identities stay the committed ones.
    public void AdoptOwnerContactPoints(PartyEditorViewModel owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        PrimaryEmailContactPoint = owner.PrimaryEmailContactPoint;
        PrimaryPhoneContactPoint = owner.PrimaryPhoneContactPoint;
    }

    public static PartyEditorViewModel FromEditorModel(PartyEditorModel model)
    {
        var roles = model.Roles.Select(CloneRole).ToList();
        var primaryRoleIndex = FindPrimaryRoleIndex(roles);
        var primaryRole = primaryRoleIndex >= 0 ? roles[primaryRoleIndex] : null;

        var contactPoints = model.ContactPoints.Select(CloneContactPoint).ToList();
        var primaryEmailIndex = FindPrimaryContactIndex(contactPoints, PartyContactType.Email);
        var primaryPhoneIndex = FindPrimaryContactIndex(contactPoints, PartyContactType.Phone);

        return new PartyEditorViewModel
        {
            Id = model.Id,
            PartyType = model.PartyType,
            LifecycleStatus = model.LifecycleStatus,
            DisplayName = model.DisplayName,
            LegalName = model.LegalName,
            PreferredName = model.PreferredName,
            ExternalCode = model.ExternalCode,
            Summary = model.Summary,
            Notes = model.Notes,
            Tags = model.Tags.ToList(),
            Region = model.Region,
            CountryCode = model.CountryCode,
            TimeZone = model.TimeZone,
            IsSensitive = model.IsSensitive,
            ExtendedDataJson = model.ExtendedDataJson,
            LastChangedBy = string.IsNullOrWhiteSpace(model.LastChangedBy) ? "crm-hr-ui" : model.LastChangedBy,
            UpdatedAtUtc = model.UpdatedAtUtc,
            PrimaryRoleKindText = primaryRole?.RoleKind.ToString() ?? string.Empty,
            PrimaryEmail = primaryEmailIndex >= 0 ? contactPoints[primaryEmailIndex].Value : string.Empty,
            PrimaryPhone = primaryPhoneIndex >= 0 ? contactPoints[primaryPhoneIndex].Value : string.Empty,
            PrimaryEmailContactPoint = primaryEmailIndex >= 0
                ? CloneContactPoint(contactPoints[primaryEmailIndex])
                : null,
            PrimaryPhoneContactPoint = primaryPhoneIndex >= 0
                ? CloneContactPoint(contactPoints[primaryPhoneIndex])
                : null,
            AdditionalRoles = roles
                .Where((_, index) => index != primaryRoleIndex)
                .Select(CloneRole)
                .ToList(),
            AdditionalContactPoints = contactPoints
                .Where((_, index) => index != primaryEmailIndex && index != primaryPhoneIndex)
                .Select(CloneContactPoint)
                .ToList(),
            Addresses = model.Addresses.Select(CloneAddress).ToList(),
            ConfidentialNotes = model.ConfidentialNotes.Select(CloneConfidentialNote).ToList()
        };
    }

    public PartyEditorModel ToEditorModel()
    {
        return new PartyEditorModel
        {
            Id = Id,
            PartyType = PartyType,
            LifecycleStatus = LifecycleStatus,
            DisplayName = DisplayName,
            LegalName = LegalName,
            PreferredName = PreferredName,
            ExternalCode = ExternalCode,
            Summary = Summary,
            Notes = Notes,
            Tags = Tags.ToList(),
            Region = Region,
            CountryCode = CountryCode,
            TimeZone = TimeZone,
            IsSensitive = IsSensitive,
            ExtendedDataJson = string.IsNullOrWhiteSpace(ExtendedDataJson) ? "{}" : ExtendedDataJson,
            LastChangedBy = LastChangedBy,
            UpdatedAtUtc = UpdatedAtUtc,
            Roles = BuildRoles(),
            ContactPoints = BuildContactPoints(),
            Addresses = Addresses
                .Where(address => !string.IsNullOrWhiteSpace(address.Line1))
                .Select(CloneAddress)
                .ToList(),
            ConfidentialNotes = ConfidentialNotes
                .Where(note => !string.IsNullOrWhiteSpace(note.NoteText))
                .Select(CloneConfidentialNote)
                .ToList()
        };
    }

    private List<PartyRoleAssignmentEditorModel> BuildRoles()
    {
        var roles = new List<PartyRoleAssignmentEditorModel>();
        if (Enum.TryParse<PartyRoleKind>(PrimaryRoleKindText, ignoreCase: true, out var primaryRole))
        {
            roles.Add(new PartyRoleAssignmentEditorModel
            {
                RoleKind = primaryRole,
                Title = primaryRole.ToString(),
                IsPrimary = true
            });
        }

        roles.AddRange(AdditionalRoles
            .Where(role => !string.IsNullOrWhiteSpace(role.Title) || Enum.IsDefined(role.RoleKind))
            .Select(role => new PartyRoleAssignmentEditorModel
            {
                Id = role.Id,
                RoleKind = role.RoleKind,
                Title = string.IsNullOrWhiteSpace(role.Title) ? role.RoleKind.ToString() : role.Title.Trim(),
                IsPrimary = false,
                ValidFromUtc = role.ValidFromUtc,
                ValidToUtc = role.ValidToUtc,
                Notes = role.Notes.Trim()
            }));

        return roles;
    }

    private List<PartyContactPointEditorModel> BuildContactPoints()
    {
        var contactPoints = new List<PartyContactPointEditorModel>();
        AddPrimaryContactPoint(
            contactPoints,
            PartyContactType.Email,
            PrimaryEmail,
            "Primary email",
            PrimaryEmailContactPoint);
        AddPrimaryContactPoint(
            contactPoints,
            PartyContactType.Phone,
            PrimaryPhone,
            "Primary phone",
            PrimaryPhoneContactPoint);

        contactPoints.AddRange(AdditionalContactPoints
            .Where(contactPoint => !string.IsNullOrWhiteSpace(contactPoint.Value))
            .Select(contactPoint => new PartyContactPointEditorModel
            {
                Id = contactPoint.Id,
                ContactType = contactPoint.ContactType,
                Label = string.IsNullOrWhiteSpace(contactPoint.Label) ? contactPoint.ContactType.ToString() : contactPoint.Label.Trim(),
                Value = contactPoint.Value.Trim(),
                NormalizedValue = NormalizeContactValue(contactPoint.ContactType, contactPoint.Value),
                IsPrimary = false,
                IsPublic = contactPoint.IsPublic,
                Tags = contactPoint.Tags.ToList(),
                Notes = contactPoint.Notes.Trim()
            }));

        return contactPoints;
    }

    private static void AddPrimaryContactPoint(
        ICollection<PartyContactPointEditorModel> contactPoints,
        PartyContactType contactType,
        string value,
        string defaultLabel,
        PartyContactPointEditorModel? existingContactPoint)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        contactPoints.Add(new PartyContactPointEditorModel
        {
            Id = existingContactPoint?.Id,
            ContactType = contactType,
            Label = string.IsNullOrWhiteSpace(existingContactPoint?.Label)
                ? defaultLabel
                : existingContactPoint.Label.Trim(),
            Value = value.Trim(),
            NormalizedValue = NormalizeContactValue(contactType, value),
            IsPrimary = true,
            IsPublic = existingContactPoint?.IsPublic ?? true,
            Tags = existingContactPoint?.Tags.ToList() ?? [],
            Notes = existingContactPoint?.Notes.Trim() ?? string.Empty
        });
    }

    private static int FindPrimaryRoleIndex(IReadOnlyList<PartyRoleAssignmentEditorModel> roles)
    {
        if (roles.Count == 0)
        {
            return -1;
        }

        for (var index = 0; index < roles.Count; index++)
        {
            if (roles[index].IsPrimary)
            {
                return index;
            }
        }

        return 0;
    }

    private static int FindPrimaryContactIndex(
        IReadOnlyList<PartyContactPointEditorModel> contactPoints,
        PartyContactType contactType)
    {
        for (var index = 0; index < contactPoints.Count; index++)
        {
            if (contactPoints[index].ContactType == contactType && contactPoints[index].IsPrimary)
            {
                return index;
            }
        }

        for (var index = 0; index < contactPoints.Count; index++)
        {
            if (contactPoints[index].ContactType == contactType)
            {
                return index;
            }
        }

        return -1;
    }

    private static PartyRoleAssignmentEditorModel CloneRole(PartyRoleAssignmentEditorModel role)
    {
        return new PartyRoleAssignmentEditorModel
        {
            Id = role.Id,
            RoleKind = role.RoleKind,
            Title = role.Title,
            IsPrimary = role.IsPrimary,
            ValidFromUtc = role.ValidFromUtc,
            ValidToUtc = role.ValidToUtc,
            Notes = role.Notes
        };
    }

    private static PartyContactPointEditorModel CloneContactPoint(PartyContactPointEditorModel contactPoint)
    {
        return new PartyContactPointEditorModel
        {
            Id = contactPoint.Id,
            ContactType = contactPoint.ContactType,
            Label = contactPoint.Label,
            Value = contactPoint.Value,
            NormalizedValue = contactPoint.NormalizedValue,
            IsPrimary = contactPoint.IsPrimary,
            IsPublic = contactPoint.IsPublic,
            Tags = contactPoint.Tags.ToList(),
            Notes = contactPoint.Notes
        };
    }

    private static PartyAddressEditorModel CloneAddress(PartyAddressEditorModel address)
    {
        return new PartyAddressEditorModel
        {
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
        };
    }

    private static PartyConfidentialNoteEditorModel CloneConfidentialNote(PartyConfidentialNoteEditorModel note)
    {
        return new PartyConfidentialNoteEditorModel
        {
            Id = note.Id,
            Category = note.Category,
            NoteText = note.NoteText,
            CreatedBy = note.CreatedBy,
            CreatedAtUtc = note.CreatedAtUtc,
            UpdatedAtUtc = note.UpdatedAtUtc
        };
    }

    private static string NormalizeContactValue(PartyContactType contactType, string value)
    {
        var trimmedValue = value.Trim();
        return contactType switch
        {
            PartyContactType.Phone => new string(trimmedValue.Where(character => char.IsDigit(character) || character == '+').ToArray()),
            _ => trimmedValue.ToLowerInvariant()
        };
    }
}
