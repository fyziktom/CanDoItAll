using CanDoItAll.CrmHr.UI.Home;

namespace CanDoItAll.Modules.CrmHr.Components;

// Projects the application query snapshot into the Home presentation contract. Totals come from the owner, never
// from preview lengths; enum labels are formatted here so the renderer needs no domain enums; the sensitive preview
// deliberately drops the operational summary; nothing beyond what Home renders is copied.
public static class CrmHrHomePresentationMapper
{
    public static CrmHrHomeOverview ToOverview(CrmHrHomeSnapshotModel snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new CrmHrHomeOverview(
            new CrmHrHomeTotals(
                snapshot.PartyCount,
                snapshot.OrganizationCount,
                snapshot.OpportunityCount,
                snapshot.WorkforceProfileCount,
                snapshot.AgentProjectionCount,
                snapshot.SensitiveCount),
            snapshot.DirectoryPreview.Select(ToDirectoryEntry).ToArray(),
            snapshot.SensitivePreview.Select(ToSensitiveEntry).ToArray(),
            snapshot.OpenPipelinePreview.Select(ToOpportunityEntry).ToArray());
    }

    public static CrmHrHomeDirectoryEntry ToDirectoryEntry(CrmHrHomePartyPreviewModel party)
    {
        ArgumentNullException.ThrowIfNull(party);
        return new CrmHrHomeDirectoryEntry(
            party.Id,
            party.DisplayName,
            FormatPartyType(party.PartyType),
            FormatLifecycle(party.LifecycleStatus),
            string.IsNullOrWhiteSpace(party.Summary) ? null : party.Summary,
            party.IsSensitive);
    }

    public static CrmHrHomeSensitiveEntry ToSensitiveEntry(CrmHrHomePartyPreviewModel party)
    {
        ArgumentNullException.ThrowIfNull(party);
        return new CrmHrHomeSensitiveEntry(
            party.Id,
            party.DisplayName,
            FormatPartyType(party.PartyType),
            FormatLifecycle(party.LifecycleStatus));
    }

    public static CrmHrHomeOpportunityEntry ToOpportunityEntry(OpportunitySummaryModel opportunity)
    {
        ArgumentNullException.ThrowIfNull(opportunity);
        return new CrmHrHomeOpportunityEntry(
            opportunity.Id,
            opportunity.AccountPartyId,
            opportunity.Title,
            opportunity.AccountDisplayName,
            opportunity.OwnerDisplayName,
            FormatStage(opportunity.Stage),
            FormatSource(opportunity.OpportunitySource),
            opportunity.Amount,
            opportunity.ProbabilityPercent);
    }

    // The labels are the enum names the page has always shown; they are display text, not identifiers.
    public static string FormatPartyType(PartyType value) => value.ToString();

    public static string FormatLifecycle(PartyLifecycleStatus value) => value.ToString();

    public static string FormatStage(OpportunityStage value) => value.ToString();

    public static string FormatSource(OpportunitySource value) => value.ToString();
}
