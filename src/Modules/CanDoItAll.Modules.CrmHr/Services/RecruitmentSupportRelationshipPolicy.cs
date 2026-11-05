using System.Linq.Expressions;

namespace CanDoItAll.Modules.CrmHr;

internal static class RecruitmentSupportRelationshipPolicy {
    internal const string Buddy = RecruitmentSupportRelationshipSemantics.Buddy;
    internal const string Mentor = RecruitmentSupportRelationshipSemantics.Mentor;
    internal const string Manager = RecruitmentSupportRelationshipSemantics.Manager;

    internal static RecruitmentSupportRole Role(PartyRelationshipKind kind, string notes) => RecruitmentSupportRelationshipSemantics.Role(kind, notes);

    internal static Expression<Func<PartyRelationship, bool>> Owned(Guid partyId) => row =>
        row.SourcePartyId == partyId && row.RelationshipKind == PartyRelationshipKind.ManagedBy ||
        row.TargetPartyId == partyId && row.RelationshipKind == PartyRelationshipKind.Supports && (row.Notes == Buddy || row.Notes == Mentor);

    internal static RecruitmentSupportAssignmentsModel ToModel(Guid partyId, IReadOnlyList<PartyRelationshipListItemModel> rows) {
        var manager = rows.FirstOrDefault(row => row.IsOutgoing && row.RelationshipKind == PartyRelationshipKind.ManagedBy);
        var buddy = rows.FirstOrDefault(row => !row.IsOutgoing && row.RelationshipKind == PartyRelationshipKind.Supports && row.Notes == Buddy);
        var mentor = rows.FirstOrDefault(row => !row.IsOutgoing && row.RelationshipKind == PartyRelationshipKind.Supports && row.Notes == Mentor);
        return new(partyId, manager?.RelatedPartyId, manager?.RelatedPartyDisplayName ?? string.Empty,
            buddy?.RelatedPartyId, buddy?.RelatedPartyDisplayName ?? string.Empty,
            mentor?.RelatedPartyId, mentor?.RelatedPartyDisplayName ?? string.Empty) { Original = PartyRelationshipObservation.FromRows(rows) };
    }

    internal static IReadOnlyList<PartyRelationshipEditorModel> Desired(RecruitmentSupportAssignmentsEditorModel model, IReadOnlyList<PartyRelationship> current) {
        var result = new List<PartyRelationshipEditorModel>();
        Add(model.ManagerPartyId, RecruitmentSupportRole.Manager, PartyRelationshipKind.ManagedBy, Manager, true);
        Add(model.BuddyPartyId, RecruitmentSupportRole.Buddy, PartyRelationshipKind.Supports, Buddy, false);
        Add(model.MentorPartyId, RecruitmentSupportRole.Mentor, PartyRelationshipKind.Supports, Mentor, false);
        return result;

        void Add(Guid? relatedId, RecruitmentSupportRole role, PartyRelationshipKind kind, string notes, bool outgoing) {
            if (relatedId is not Guid id) {
                return;
            }
            var retained = current.OrderBy(row => row.Id).FirstOrDefault(row => Role(row.RelationshipKind, row.Notes) == role);
            result.Add(new() {
                Id = retained?.Id ?? Guid.NewGuid(), RelatedPartyId = id, RelationshipKind = kind,
                IsOutgoing = outgoing, IsPrimary = true, StartDateUtc = retained?.StartDateUtc,
                EndDateUtc = retained?.EndDateUtc, Notes = notes
            });
        }
    }
}
