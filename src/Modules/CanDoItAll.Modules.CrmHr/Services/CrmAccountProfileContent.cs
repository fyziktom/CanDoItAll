namespace CanDoItAll.Modules.CrmHr;

internal static class CrmAccountProfileContent {
    public static CrmAccountProfileEditorModel Map(Guid accountId, DateTimeOffset partyVersion,
        CrmAccountRelationshipStage stage, CrmAccountProfile? profile) => new() {
        Id = profile?.Id,
        AccountPartyId = accountId,
        Original = new(profile?.Id, profile?.UpdatedAtUtc, partyVersion),
        RelationshipStage = stage,
        CommercialNotes = profile?.CommercialNotes ?? string.Empty,
        ConstraintNotes = profile?.ConstraintNotes ?? string.Empty,
        TimingRiskNotes = profile?.TimingRiskNotes ?? string.Empty,
        LastChangedBy = string.IsNullOrWhiteSpace(profile?.LastChangedBy) ? "crm-hr-ui" : profile.LastChangedBy
    };
}
