using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;

namespace CanDoItAll.Tests.Unit.CrmHr;

// The host-side projection of the application snapshot into the Home presentation contract.
public sealed class CrmHrHomePresentationMapperTests
{
    [Fact]
    public void Totals_come_from_the_snapshot_and_never_from_preview_lengths()
    {
        var snapshot = new CrmHrHomeSnapshotModel(
            PartyCount: 1284,
            OrganizationCount: 311,
            OpportunityCount: 97,
            WorkforceProfileCount: 542,
            AgentProjectionCount: 18,
            SensitiveCount: 41,
            DirectoryPreview: [Party("A"), Party("B")],
            SensitivePreview: [Party("C", isSensitive: true)],
            OpenPipelinePreview: [Opportunity("Deal", 100m)]);

        var overview = CrmHrHomePresentationMapper.ToOverview(snapshot);

        Assert.Equal(new CanDoItAll.CrmHr.UI.Home.CrmHrHomeTotals(1284, 311, 97, 542, 18, 41), overview.Totals);
        Assert.Equal(2, overview.DirectoryPreview.Count);
        Assert.Single(overview.SensitivePreview);
        Assert.Single(overview.OpenPipelinePreview);
    }

    [Fact]
    public void Directory_rows_keep_order_labels_and_optional_summary_while_sensitive_rows_carry_no_summary()
    {
        var sensitive = Party("Dana Reyes", isSensitive: true, summary: "Operational summary that must not reach the sensitive card");
        var snapshot = new CrmHrHomeSnapshotModel(3, 0, 0, 0, 0, 1, [Party("Zed", summary: "  "), sensitive], [sensitive], []);

        var overview = CrmHrHomePresentationMapper.ToOverview(snapshot);

        Assert.Equal(["Zed", "Dana Reyes"], overview.DirectoryPreview.Select(item => item.DisplayName));
        Assert.Null(overview.DirectoryPreview[0].Summary);
        Assert.Equal("Operational summary that must not reach the sensitive card", overview.DirectoryPreview[1].Summary);
        Assert.True(overview.DirectoryPreview[1].IsSensitive);
        Assert.Equal("Person", overview.DirectoryPreview[1].TypeLabel);
        Assert.Equal("Active", overview.DirectoryPreview[1].LifecycleLabel);

        var sensitiveRow = Assert.Single(overview.SensitivePreview);
        Assert.Equal(sensitive.Id, sensitiveRow.PartyId);
        Assert.Equal("Dana Reyes", sensitiveRow.DisplayName);
        Assert.DoesNotContain(
            sensitiveRow.GetType().GetProperties(),
            property => property.Name.Contains("Summary", StringComparison.Ordinal) || property.Name.Contains("Note", StringComparison.Ordinal));
    }

    [Fact]
    public void Opportunity_rows_keep_both_identities_labels_and_nullable_amount()
    {
        var withAmount = Opportunity("Renewal", 45000m, OpportunityStage.Proposal, OpportunitySource.Renewal, probability: 65);
        var withoutAmount = Opportunity("Discovery", null, OpportunityStage.Identified, OpportunitySource.Direct, probability: 0) with
        {
            AccountDisplayName = "Unknown account",
            OwnerDisplayName = "Unknown owner"
        };
        var snapshot = new CrmHrHomeSnapshotModel(0, 0, 2, 0, 0, 0, [], [], [withAmount, withoutAmount]);

        var overview = CrmHrHomePresentationMapper.ToOverview(snapshot);

        var first = overview.OpenPipelinePreview[0];
        Assert.Equal(withAmount.Id, first.OpportunityId);
        Assert.Equal(withAmount.AccountPartyId, first.AccountPartyId);
        Assert.Equal("Proposal", first.StageLabel);
        Assert.Equal("Renewal", first.SourceLabel);
        Assert.Equal(45000m, first.Amount);
        Assert.Equal(65, first.ProbabilityPercent);
        var second = overview.OpenPipelinePreview[1];
        Assert.Null(second.Amount);
        Assert.Equal("Unknown account", second.AccountDisplayName);
        Assert.Equal("Unknown owner", second.OwnerDisplayName);
    }

    [Fact]
    public void Overview_copies_the_supplied_collections()
    {
        var directory = new List<CrmHrHomePartyPreviewModel> { Party("A") };
        var snapshot = new CrmHrHomeSnapshotModel(1, 0, 0, 0, 0, 0, directory, [], []);

        var overview = CrmHrHomePresentationMapper.ToOverview(snapshot);
        directory.Add(Party("B"));

        Assert.Single(overview.DirectoryPreview);
    }

    private static CrmHrHomePartyPreviewModel Party(string name, bool isSensitive = false, string? summary = null)
        => new(Guid.NewGuid(), name, PartyType.Person, PartyLifecycleStatus.Active, isSensitive, summary ?? $"{name} summary", DateTimeOffset.UnixEpoch);

    private static OpportunitySummaryModel Opportunity(
        string title,
        decimal? amount,
        OpportunityStage stage = OpportunityStage.Qualified,
        OpportunitySource source = OpportunitySource.Direct,
        int probability = 50)
        => new(Guid.NewGuid(), title, stage, Guid.NewGuid(), Guid.NewGuid(), "Account", "Owner", source, amount, probability);
}
