using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

internal static class PartyEditorChildren {
    public static bool HasInvalidIdentities(PartyEditorModel model) =>
        Invalid(model.Roles.Select(row => row.Id)) ||
        Invalid(model.ContactPoints.Select(row => row.Id)) ||
        Invalid(model.Addresses.Select(row => row.Id)) ||
        Invalid(model.ConfidentialNotes.Select(row => row.Id));

    public static async Task ReplaceAsync(CrmHrDbContext context, Guid partyId, PartyEditorModel model,
        string actor, DateTimeOffset now, CancellationToken cancellationToken) {
        var roles = await context.Set<PartyRoleAssignment>().Where(row => row.PartyId == partyId).ToListAsync(cancellationToken);
        var contacts = await context.Set<PartyContactPoint>().Where(row => row.PartyId == partyId).ToListAsync(cancellationToken);
        var addresses = await context.Set<PartyAddress>().Where(row => row.PartyId == partyId).ToListAsync(cancellationToken);
        var notes = await context.Set<PartyConfidentialNote>().Where(row => row.PartyId == partyId).ToListAsync(cancellationToken);

        Replace(context, roles, model.Roles, row => row.Id, input => input.Id,
            id => new PartyRoleAssignment { Id = id, PartyId = partyId }, (row, input) => {
                row.RoleKind = input.RoleKind;
                row.Title = input.Title.Trim();
                row.IsPrimary = input.IsPrimary;
                row.ValidFromUtc = input.ValidFromUtc is { } from ? CrmHrEditVersions.Normalize(from) : null;
                row.ValidToUtc = input.ValidToUtc is { } to ? CrmHrEditVersions.Normalize(to) : null;
                row.Notes = input.Notes.Trim();
            });
        Replace(context, contacts, model.ContactPoints.Where(input => !string.IsNullOrWhiteSpace(input.Value)).ToList(), row => row.Id, input => input.Id,
            id => new PartyContactPoint { Id = id, PartyId = partyId }, (row, input) => {
                row.ContactType = input.ContactType;
                row.Label = input.Label.Trim();
                row.Value = input.Value.Trim();
                row.NormalizedValue = input.NormalizedValue.Trim();
                row.IsPrimary = input.IsPrimary;
                row.IsPublic = input.IsPublic;
                row.TagsJson = JsonSerializer.Serialize(input.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Select(tag => tag.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
                row.Notes = input.Notes.Trim();
            });
        Replace(context, addresses, model.Addresses, row => row.Id, input => input.Id,
            id => new PartyAddress { Id = id, PartyId = partyId }, (row, input) => {
                row.AddressType = input.AddressType.Trim();
                row.Line1 = input.Line1.Trim();
                row.Line2 = input.Line2.Trim();
                row.City = input.City.Trim();
                row.Region = input.Region.Trim();
                row.PostalCode = input.PostalCode.Trim();
                row.CountryCode = input.CountryCode.Trim();
                row.IsPrimary = input.IsPrimary;
                row.Notes = input.Notes.Trim();
            });
        Replace(context, notes, model.ConfidentialNotes.Where(input => !string.IsNullOrWhiteSpace(input.NoteText)).ToList(),
            row => row.Id, input => input.Id,
            id => new PartyConfidentialNote { Id = id, PartyId = partyId, CreatedBy = actor, CreatedAtUtc = CrmHrEditVersions.Normalize(now) }, (row, input) => {
                var category = string.IsNullOrWhiteSpace(input.Category) ? PartyConfidentialNoteCategories.Other : input.Category.Trim();
                row.Category = PartyConfidentialNoteCategories.All.Contains(category, StringComparer.Ordinal)
                    ? category
                    : PartyConfidentialNoteCategories.Other;
                row.NoteText = input.NoteText.Trim();
                row.UpdatedAtUtc = CrmHrEditVersions.Normalize(now);
            });
    }

    private static bool Invalid(IEnumerable<Guid?> ids) {
        var seen = new HashSet<Guid>();
        return ids.Any(id => id is Guid value && (value == Guid.Empty || !seen.Add(value)));
    }

    private static void Replace<TEntity, TInput>(CrmHrDbContext context, List<TEntity> existing, IReadOnlyList<TInput> inputs,
        Func<TEntity, Guid> entityId, Func<TInput, Guid?> inputId, Func<Guid, TEntity> create, Action<TEntity, TInput> apply)
        where TEntity : class {
        var byId = existing.ToDictionary(entityId);
        var retained = inputs.Select(inputId).OfType<Guid>().ToHashSet();
        context.RemoveRange(existing.Where(row => !retained.Contains(entityId(row))));
        foreach (var input in inputs) {
            var id = inputId(input);
            if (id is not Guid value || !byId.TryGetValue(value, out var row)) {
                row = create(id ?? Guid.NewGuid());
                context.Add(row);
            }
            apply(row, input);
        }
    }
}
