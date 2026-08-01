namespace CanDoItAll.Modules.CrmHr.Components;

public enum OpportunityEditorSection
{
    All,
    Basics,
    Ownership,
    Commercial
}

internal static class OpportunityEditorDrafts
{
    public static CrmOpportunityEditorModel Clone(CrmOpportunityEditorModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new CrmOpportunityEditorModel
        {
            Id = source.Id,
            ExpectedUpdatedAtUtc = source.ExpectedUpdatedAtUtc,
            AccountPartyId = source.AccountPartyId,
            Title = source.Title,
            Stage = source.Stage,
            RelationshipStage = source.RelationshipStage,
            OpportunitySource = source.OpportunitySource,
            OwnerPartyId = source.OwnerPartyId,
            DeliveryUnitPartyId = source.DeliveryUnitPartyId,
            CurrencyCode = source.CurrencyCode,
            Amount = source.Amount,
            ProbabilityPercent = source.ProbabilityPercent,
            ExpectedCloseOn = source.ExpectedCloseOn,
            LostReason = source.LostReason,
            CompetitorName = source.CompetitorName,
            PartnerContributionSummary = source.PartnerContributionSummary,
            Summary = source.Summary,
            Notes = source.Notes,
            StageNotes = source.StageNotes,
            LinkedProjectId = source.LinkedProjectId,
            Parties = source.Parties.Select(ClonePartyLink).ToList(),
            LastChangedBy = source.LastChangedBy
        };
    }

    public static CrmOpportunityEditorModel FromDetail(CrmOpportunityDetailModel opportunity)
    {
        ArgumentNullException.ThrowIfNull(opportunity);
        return new CrmOpportunityEditorModel
        {
            Id = opportunity.Id,
            ExpectedUpdatedAtUtc = opportunity.UpdatedAtUtc,
            AccountPartyId = opportunity.AccountPartyId,
            Title = opportunity.Title,
            Stage = opportunity.Stage,
            RelationshipStage = opportunity.RelationshipStage,
            OpportunitySource = opportunity.OpportunitySource,
            OwnerPartyId = opportunity.OwnerPartyId,
            DeliveryUnitPartyId = opportunity.DeliveryUnitPartyId,
            CurrencyCode = opportunity.CurrencyCode,
            Amount = opportunity.Amount,
            ProbabilityPercent = opportunity.ProbabilityPercent,
            ExpectedCloseOn = opportunity.ExpectedCloseOn,
            LostReason = opportunity.LostReason,
            CompetitorName = opportunity.CompetitorName,
            PartnerContributionSummary = opportunity.PartnerContributionSummary,
            Summary = opportunity.Summary,
            Notes = opportunity.Notes,
            LinkedProjectId = opportunity.LinkedProjectId,
            Parties = opportunity.Parties
                .Select(item => new CrmOpportunityPartyLinkEditorModel
                {
                    Id = item.Id,
                    PartyId = item.PartyId,
                    Role = item.Role
                })
                .ToList()
        };
    }

    private static CrmOpportunityPartyLinkEditorModel ClonePartyLink(
        CrmOpportunityPartyLinkEditorModel source)
    {
        return new CrmOpportunityPartyLinkEditorModel
        {
            Id = source.Id,
            PartyId = source.PartyId,
            Role = source.Role
        };
    }
}
