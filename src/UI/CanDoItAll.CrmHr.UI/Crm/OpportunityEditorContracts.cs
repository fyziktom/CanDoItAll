using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Crm;

public enum OpportunityEditorSection
{
    All,
    Basics,
    Ownership,
    Commercial
}

public static class OpportunityEditorDrafts
{
    public static CrmOpportunityEditorModel Clone(CrmOpportunityEditorModel source) {
        ArgumentNullException.ThrowIfNull(source);
        return source.Snapshot();
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
            LinkedProjectAdmission = opportunity.LinkedProjectAdmission,
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

}
